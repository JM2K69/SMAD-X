using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SMADX.Models;
using SMADX.Services.Audit;
using static SMADX.Services.Audit.AuditContext;

namespace SMADX.Services.Labs
{
    /// <summary>
    /// Solution proposée par Studio : le domaine corrigé (aux dates du départ), le nombre de corrections faites et les
    /// règles d'audit qui restent en échec ; les objectifs non atteints se lisent dans la validation.
    /// </summary>
    public sealed record ProposedSolution(ADRootDocument Solution, int Fixes, IReadOnlyList<string> RemainingRules);

    /// <summary>
    /// Propose une solution à n'importe quel exercice : chaque constat de l'audit du domaine de départ reçoit la
    /// correction que décrit la fiche de sa règle, sans rien supprimer (on désactive, on retire une appartenance ou une
    /// délégation, on renouvelle un mot de passe).
    /// <para>
    /// Les corrections se font une à une, la plus grave d'abord, et un groupe avant ses membres : couper l'imbrication
    /// d'un groupe corrige tous ceux qui en héritent. L'audit est refait après chacune, car une correction en appelle
    /// parfois une autre (sorti d'un groupe protégé, le compte garde adminCount). Une correction qui ferait tomber une
    /// garde de l'exercice (l'appartenance ou la délégation légitime qu'il faut garder) est annulée. Ce que l'audit ne
    /// voit pas, ou ce qu'une garde interdit de corriger ainsi, reste au formateur, à faire dans SMAD-X.
    /// </para>
    /// </summary>
    public static class LabSolver
    {
        private const int MaxFixes = 300;

        public static ProposedSolution Propose(LabDefinition lab)
        {
            var options = ADDataService.SerializerOptions;
            ADRootDocument Copy(ADRootDocument source)
            {
                var copy = JsonSerializer.Deserialize<ADRootDocument>(JsonSerializer.Serialize(source, options), options)!;
                ADDataService.RestoreParentReferences(copy.Domain, null);
                return copy;
            }
            var document = Copy(lab.Start);
            // Aux dates du départ : la solution se décale ensuite comme lui (voir LabService.PrepareSolution)
            var now = lab.ReferenceDate;
            var start = Copy(lab.Start).Domain;
            var guards = new LabDefinition { Id = lab.Id, ReferenceDate = lab.ReferenceDate, Start = lab.Start, Objectives = lab.Objectives.Where(o => o.Guard).ToList() };
            bool GuardBroken(ADObject current) =>
                guards.Objectives.Count > 0 && new LabGrader().Grade(guards, start, current, now).Objectives.Any(o => !o.Passed);

            var rejected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var fixes = 0;
            while (fixes < MaxFixes)
            {
                var context = new AuditContext(document.Domain, now);
                var candidates = new ADSecurityAuditService().Run(document.Domain, now).Findings
                    .Where(f => !rejected.Contains(Key(f)))
                    .OrderBy(f => f.Severity)
                    .ThenBy(f => f.Object.Type == ADObjectType.Group ? 0 : 1);
                var applied = false;
                foreach (var finding in candidates)
                {
                    var before = JsonSerializer.Serialize(document, options);
                    if (!Fix(finding, context, document.Domain, now))
                    {
                        rejected.Add(Key(finding));
                        continue;
                    }
                    if (GuardBroken(document.Domain))
                    {
                        // Cette correction ferait tomber une garde : on revient en arrière
                        document = JsonSerializer.Deserialize<ADRootDocument>(before, options)!;
                        ADDataService.RestoreParentReferences(document.Domain, null);
                        rejected.Add(Key(finding));
                        continue;
                    }
                    fixes++;
                    applied = true;
                    break;
                }
                if (!applied) break;
            }

            // Puis ce que demandent les objectifs et que l'audit ne corrige pas : une délégation à donner, une appartenance, une option
            foreach (var objective in lab.Objectives.Where(o => !o.Guard))
            {
                var before = JsonSerializer.Serialize(document, options);
                if (!Satisfy(objective.Check, document.Domain, now)) continue;
                if (GuardBroken(document.Domain))
                {
                    document = JsonSerializer.Deserialize<ADRootDocument>(before, options)!;
                    ADDataService.RestoreParentReferences(document.Domain, null);
                    continue;
                }
                fixes++;
            }
            var remaining = new ADSecurityAuditService().Run(document.Domain, now).FailedRules.Select(r => r.Id).ToList();
            return new ProposedSolution(document, fixes, remaining);
        }

        /// <summary>Atteint directement un objectif déclaratif ; faux s'il est déjà atteint ou ne se fait pas ainsi.</summary>
        private static bool Satisfy(LabCheck check, ADObject domain, DateTime now)
        {
            var c = new AuditContext(domain, now);
            ADObject? Find(string? name, ADObjectType? type = null) => name == null ? null
                : c.Objects.FirstOrDefault(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase) && (type is null || o.Type == type));
            var o = Find(check.Object, check.ObjectType);
            switch (check.Type)
            {
                case LabCheckType.DelegationExists when Find(check.Target) is { } target && c.Principal(check.Trustee!) is { } trustee && check.RightCategory is { } category:
                    if (target.Delegations.Any(d => !d.IsInherited && d.RightCategory == category && string.Equals(d.TrusteeName, trustee.Name, StringComparison.OrdinalIgnoreCase)))
                        return false;
                    LabDomainBuilder.Delegate(target, trustee.Name, trustee.Type switch { ADObjectType.Group => TrusteeType.Group, ADObjectType.Computer => TrusteeType.Computer, _ => TrusteeType.User },
                        RightName(category), category, target.Tier ?? string.Empty);
                    return true;
                case LabCheckType.DelegationOnly:
                    var allowed = check.AllowedCategories ?? [];
                    var extra = c.Objects.SelectMany(x => x.Delegations.Where(d => !d.IsInherited && !allowed.Contains(d.RightCategory)
                                                                                   && string.Equals(d.TrusteeName, check.Trustee, StringComparison.OrdinalIgnoreCase)).Select(d => (x, d))).ToList();
                    foreach (var (x, d) in extra) x.Delegations.Remove(d);
                    return extra.Count > 0;
                case LabCheckType.MemberOf when o != null && c.Group(check.Group!) is { } group:
                    if (check.Effective ? c.EffectiveGroups(o).Contains(group) : o.MemberOf.Contains(group.Name, StringComparer.OrdinalIgnoreCase)) return false;
                    o.MemberOf.Add(group.Name);
                    return true;
                case LabCheckType.NotMemberOf when o != null:
                    var direct = o.MemberOf.FirstOrDefault(g => string.Equals(g, check.Group, StringComparison.OrdinalIgnoreCase));
                    return direct != null && o.MemberOf.Remove(direct);
                case LabCheckType.HasFlag when o != null && check.Flag is { } flag:
                    return SetFlag(o, flag);
                case LabCheckType.NotHasFlag when o != null && check.Flag is { } flag:
                    return ClearFlag(o, flag);
                case LabCheckType.TierIs when o != null && check.Tier != null && o.Tier != check.Tier:
                    o.Tier = check.Tier;
                    return true;
                // L'objectif demande que l'objet disparaisse : c'est la seule suppression que fait Studio
                case LabCheckType.ObjectAbsent when o?.Parent != null:
                    return o.Parent.Children.Remove(o);
                default:
                    return false;
            }
        }

        /// <summary>Nom du droit d'une délégation, comme l'import les nomme.</summary>
        private static string RightName(RightCategory category) => category switch
        {
            RightCategory.PasswordReset => "ResetPassword",
            RightCategory.AccountUnlock => "UnlockAccount",
            RightCategory.ComputerManagement => "CreateChild:computer",
            RightCategory.AttributeWrite => "WriteProperty",
            RightCategory.FullControl => "GenericAll",
            _ => "ExtendedRight"
        };

        private static string Key(AuditFinding finding) => $"{finding.Rule.Id}|{finding.Object.DistinguishedName}|{finding.Detail}";

        /// <summary>Applique la correction du constat ; faux si elle ne change rien.</summary>
        private static bool Fix(AuditFinding finding, AuditContext c, ADObject domain, DateTime now)
        {
            var o = finding.Object;
            switch (finding.Rule.Id)
            {
                // Un compte de service n'a pas à être privilégié ; le service passe à un gMSA
                case "KRB-01":
                    var removed = RemoveMemberships(o, g => c.ProtectedGroups.Contains(g) || c.IsMemberOfAny(g, c.ProtectedGroups), c);
                    o.AdminCount = false;
                    return MoveToGmsa(o, now) || removed;
                case "KRB-02":
                    return MoveToGmsa(o, now);
                case "KRB-03" or "KRB-04":
                    return ClearFlag(o, ADAccountFlags.DontRequirePreauth);
                case "KRB-05" or "PWD-04":
                    return Renew(o, now.AddDays(-1));
                case "DLG-01":
                    return ClearFlag(o, ADAccountFlags.TrustedForDelegation);
                case "DLG-02":
                    return ClearFlag(o, ADAccountFlags.TrustedToAuthForDelegation) | RemoveDelegationToDc(o, c);
                case "DLG-03":
                    return RemoveDelegationToDc(o, c);
                case "DLG-04":
                    return SetFlag(o, ADAccountFlags.NotDelegated);
                case "PWD-01" or "PWD-02":
                    return ClearFlag(o, ADAccountFlags.PasswordNeverExpires);
                case "PWD-03":
                    Renew(o, now.AddDays(-1));
                    return ClearFlag(o, ADAccountFlags.PasswordNotRequired);
                case "PWD-05":
                    return Renew(o, now.AddDays(-5));
                case "PWD-07":
                    return Renew(o, now.AddDays(-10));
                // Hors d'usage : désactivé, il sera supprimé après une période d'observation
                case "PWD-06" or "ACC-01" or "ACC-02":
                    return SetFlag(o, ADAccountFlags.AccountDisabled);
                case "PWD-08":
                    if (domain.PSOMinPasswordLength >= 12) return false;
                    domain.PSOMinPasswordLength = 12;
                    return true;
                case "PWD-09":
                    return Strengthen(c.EffectivePasswordPolicy(o));
                case "ACC-03":
                    if (domain.MachineAccountQuota is 0) return false;
                    domain.MachineAccountQuota = 0;
                    return true;
                case "PRIV-01":
                    if (o.AdminCount) return false;
                    o.AdminCount = true;
                    return true;
                case "PRIV-02":
                    if (!o.AdminCount) return false;
                    o.AdminCount = false;
                    return true;
                // Groupes qui doivent rester vides : leurs membres en sortent (les droits utiles passent par délégation)
                case "PRIV-03":
                    var emptied = false;
                    foreach (var member in c.DirectMembers(o).ToList())
                        emptied |= member.MemberOf.Remove(o.Name);
                    return emptied;
                case "PRIV-04":
                    return o.MemberOf.Remove("DnsAdmins");
                case "PRIV-05":
                    var tier0 = TierZero();
                    if (o.Tier == tier0) return false;
                    o.Tier = tier0;
                    return true;
                case "ACL-01":
                    o.Owner = "Domain Admins";
                    return true;
                // Une appartenance qui fait monter de tier : on la retire
                case "TIER-01":
                    var tier = TierLevel(o);
                    return RemoveMemberships(o, g => TierLevel(g) < tier || c.EffectiveGroups(g).Any(h => TierLevel(h) < tier), c);
                // Une GPO d'un tier inférieur liée plus haut : on la délie de là
                case "TIER-02":
                    var unlinked = false;
                    foreach (var container in c.Objects.Where(x => x.Type is ADObjectType.OrganizationalUnit or ADObjectType.Domain && TierLevel(x) < TierLevel(o)))
                        unlinked |= container.LinkedGPOs.Remove(o.Name);
                    return unlinked;
                // Une délégation confiée à un tier inférieur : on la retire
                case "TIER-03":
                    var target = TierLevel(o);
                    var dangerous = o.Delegations.Where(d => !d.IsInherited && d.RightCategory is RightCategory.FullControl or RightCategory.AttributeWrite or RightCategory.PasswordReset
                                                             && c.Principal(d.TrusteeName) is { } trustee && TierLevel(trustee) > target).ToList();
                    foreach (var d in dangerous) o.Delegations.Remove(d);
                    return dangerous.Count > 0;
                // Une boucle d'imbrication : on retire une appartenance de la boucle
                case "GRP-01":
                    var back = o.MemberOf.FirstOrDefault(name => c.Group(name) is { } g && c.EffectiveGroups(g).Contains(o));
                    return back != null && o.MemberOf.Remove(back);
                default:
                    return false;
            }
        }

        private static bool ClearFlag(ADObject o, ADAccountFlags flag)
        {
            if (!o.AccountFlags.HasFlag(flag)) return false;
            o.AccountFlags &= ~flag;
            return true;
        }

        private static bool SetFlag(ADObject o, ADAccountFlags flag)
        {
            if (o.AccountFlags.HasFlag(flag)) return false;
            o.AccountFlags |= flag;
            return true;
        }

        private static bool Renew(ADObject o, DateTime date)
        {
            if (o.PwdLastSet >= date) return false;
            o.PwdLastSet = date;
            return true;
        }

        /// <summary>Retire les appartenances directes qui répondent au critère ; vrai si l'une a été retirée.</summary>
        private static bool RemoveMemberships(ADObject o, Func<ADObject, bool> matches, AuditContext c)
        {
            var groups = o.MemberOf.Where(name => c.Group(name) is { } g && matches(g)).ToList();
            foreach (var name in groups) o.MemberOf.Remove(name);
            return groups.Count > 0;
        }

        private static bool RemoveDelegationToDc(ADObject o, AuditContext c)
        {
            var dcs = c.DomainControllers.Select(dc => dc.Name).ToList();
            var toDc = o.AllowedToDelegateTo.Where(spn => dcs.Any(dc => spn.Contains(dc, StringComparison.OrdinalIgnoreCase))).ToList();
            foreach (var spn in toDc) o.AllowedToDelegateTo.Remove(spn);
            return toDc.Count > 0;
        }

        /// <summary>Politique d'au moins 12 caractères, renouvelée au moins tous les ans.</summary>
        private static bool Strengthen(ADObject policy)
        {
            var changed = false;
            if (policy.PSOMinPasswordLength is not >= 12) { policy.PSOMinPasswordLength = 12; changed = true; }
            if (policy.PSOMaxPasswordAgeDays is 0 or > AuditRules.PrivilegedPasswordRenewalMaxDays) { policy.PSOMaxPasswordAgeDays = 365; changed = true; }
            return changed;
        }

        /// <summary>
        /// Migration vers un gMSA : le service et ses SPN passent à un compte de service géré, l'ancien compte est
        /// désactivé (il sera supprimé quand l'application aura été vérifiée).
        /// </summary>
        private static bool MoveToGmsa(ADObject account, DateTime now)
        {
            if (account.ServicePrincipalNames.Count == 0 || account.Parent == null) return false;
            var gmsa = new ADObject("gmsa-" + account.Name, ADObjectType.GMSA)
            {
                Tier = account.Tier, Owner = "Domain Admins", Description = account.Description,
                PwdLastSet = now.AddDays(-1), LastLogonTimestamp = now, CreatedDate = now.AddDays(-2), ModifiedDate = now.AddDays(-1)
            };
            gmsa.Parent = account.Parent;
            account.Parent.Children.Add(gmsa);
            gmsa.UpdateDistinguishedNameTree();
            foreach (var spn in account.ServicePrincipalNames)
                gmsa.ServicePrincipalNames.Add(spn);
            account.ServicePrincipalNames.Clear();
            account.AccountFlags |= ADAccountFlags.AccountDisabled;
            return true;
        }

        private static string TierZero() =>
            TierConfigurationService.Instance.Tiers.FirstOrDefault(t => t.Level == 0)?.Name ?? "Tier 0";
    }
}
