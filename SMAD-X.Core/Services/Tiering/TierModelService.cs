using System;
using System.Collections.Generic;
using System.Linq;
using SMADX.Models;
using SMADX.Services.Audit;

namespace SMADX.Services.Tiering
{
    /// <summary>Ce qu'une application de modèle a créé ; les objets déjà présents sont gardés tels quels.</summary>
    public sealed record TierModelReport(int OrganizationalUnits, int Groups, int Accounts, int GroupPolicies, int Links, int Delegations, int Memberships)
    {
        public int Total => OrganizationalUnits + Groups + Accounts + GroupPolicies + Links + Delegations + Memberships;
    }

    /// <summary>
    /// Applique un modèle de cloisonnement à un domaine, comme son script de déploiement : OU, groupes, comptes
    /// d'administration, GPO et leurs liens, délégations, ajouts aux groupes existants. L'application ne déplace ni ne
    /// supprime rien, et peut être rejouée : ce qui existe déjà est gardé. Le domaine est vu une fois SDProp passé.
    /// </summary>
    public static class TierModelService
    {
        /// <summary>Propriétaire des objets créés : le script de déploiement tourne sous un administrateur du domaine.</summary>
        private const string Owner = "Domain Admins";

        public static TierModelReport Apply(ADObject domain, TierModelDefinition model, DateTime? now = null)
        {
            var date = now ?? DateTime.Now;
            var all = Flatten(domain).ToList();
            var principals = all.Where(o => o.IsAccount || o.Type == ADObjectType.Group)
                .GroupBy(o => o.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            int ous = 0, groups = 0, accounts = 0, gpos = 0, links = 0, delegations = 0, memberships = 0;

            foreach (var tier in model.Tiers.Where(t => TierConfigurationService.Instance.Tiers.All(c => c.Name != t.Name)))
                TierConfigurationService.Instance.AddTier(new TierConfiguration(tier.Name, tier.Color, tier.Description, tier.Level));

            // OU, du parent vers l'enfant
            foreach (var ou in model.OrganizationalUnits.OrderBy(o => o.Path.Count(c => c == '/')))
            {
                var parts = ou.Path.Split('/');
                var parent = parts.Length == 1 ? domain : Ou(domain, string.Join('/', parts[..^1]));
                if (parent == null) continue;
                if (Child(parent, parts[^1]) is { } existing)
                {
                    existing.Tier ??= ou.Tier;
                    continue;
                }
                Add(parent, new ADObject(parts[^1], ADObjectType.OrganizationalUnit)
                {
                    Tier = ou.Tier, Description = ou.Description, Owner = Owner, CreatedDate = date, ModifiedDate = date
                });
                ous++;
            }

            foreach (var g in model.Groups)
            {
                if (principals.ContainsKey(g.Name) || Ou(domain, g.Path) is not { } parent) continue;
                principals[g.Name] = Add(parent, new ADObject(g.Name, ADObjectType.Group)
                {
                    Tier = g.Tier, Description = g.Description, GroupScope = g.Scope, GroupCategory = ADGroupCategory.Security,
                    Owner = Owner, CreatedDate = date, ModifiedDate = date
                });
                groups++;
            }

            foreach (var a in model.Accounts)
            {
                if (principals.ContainsKey(a.Name) || Ou(domain, a.Path) is not { } parent) continue;
                principals[a.Name] = Add(parent, new ADObject(a.Name, ADObjectType.User)
                {
                    Tier = a.Tier, Description = a.Description, Owner = Owner,
                    AccountFlags = a.Enabled ? ADAccountFlags.None : ADAccountFlags.AccountDisabled,
                    PwdLastSet = date, CreatedDate = date, ModifiedDate = date
                });
                accounts++;
            }

            // Appartenances : celles des objets du modèle, puis les ajouts aux groupes qui existaient déjà
            var memberOf = model.Groups.Select(g => (g.Name, g.MemberOf)).Concat(model.Accounts.Select(a => (a.Name, a.MemberOf)))
                .SelectMany(x => x.MemberOf.Select(group => (Member: x.Name, Group: group)))
                .Concat(model.DefaultMembers.SelectMany(d => d.Members.Select(m => (Member: m, d.Group))));
            foreach (var (member, group) in memberOf)
            {
                if (!principals.TryGetValue(member, out var obj) || !principals.ContainsKey(group)
                    || obj.MemberOf.Contains(group, StringComparer.OrdinalIgnoreCase)) continue;
                obj.MemberOf.Add(group);
                memberships++;
            }

            var policies = all.FirstOrDefault(ADValidationService.IsPoliciesContainer);
            var existingGpos = all.Where(o => o.Type == ADObjectType.Policy).Select(o => o.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var gpo in model.GroupPolicies)
            {
                if (policies != null && existingGpos.Add(gpo.Name))
                {
                    // Une GPO se modifie depuis le Tier 0 : ce sont ses administrateurs qui la contrôlent
                    Add(policies, new ADObject(gpo.Name, ADObjectType.Policy)
                    {
                        Tier = "Tier 0", Owner = Owner, CreatedDate = date, ModifiedDate = date,
                        GpoDenyInteractiveLogon = gpo.DenyInteractiveLogon is { } local ? new(local) : null,
                        GpoDenyRemoteInteractiveLogon = gpo.DenyRemoteInteractiveLogon is { } remote ? new(remote) : null,
                        GpoLocalAdmins = gpo.LocalAdmins is { } admins ? new(admins) : null,
                        GpoReplaceLocalAdmins = gpo.ReplaceLocalAdmins,
                        GpoComputerFilter = gpo.ComputerFilter
                    });
                    gpos++;
                }
                foreach (var link in gpo.Links.Where(l => l.Enabled))
                {
                    var target = link.Target.Length == 0 ? domain : Ou(domain, link.Target);
                    if (target == null || target.LinkedGPOs.Contains(gpo.Name, StringComparer.OrdinalIgnoreCase)) continue;
                    target.LinkedGPOs.Add(gpo.Name);
                    links++;
                }
            }

            foreach (var d in model.Delegations)
            {
                if (Ou(domain, d.Target) is not { } target || !principals.TryGetValue(d.Trustee, out var trustee)
                    || target.Delegations.Any(x => !x.IsInherited && string.Equals(x.TrusteeName, d.Trustee, StringComparison.OrdinalIgnoreCase)
                                                   && string.Equals(x.Right, d.Right, StringComparison.OrdinalIgnoreCase))) continue;
                target.Delegations.Add(new ADDelegation
                {
                    TrusteeName = trustee.Name, TrusteeType = trustee.Type == ADObjectType.Group ? TrusteeType.Group : TrusteeType.User,
                    TargetDN = target.DistinguishedName, Right = d.Right, RightCategory = d.Category, Tier = target.Tier
                });
                delegations++;
            }

            ApplySdProp(domain);
            domain.TierModel = model.Id;
            domain.TierModelVersion = model.Source.Version;
            return new TierModelReport(ous, groups, accounts, gpos, links, delegations, memberships);
        }

        /// <summary>OU au chemin donné depuis la racine (« Tier Model Administration/Tier 0 ») ; la casse est ignorée, comme dans AD.</summary>
        public static ADObject? Ou(ADObject domain, string path)
        {
            var current = domain;
            foreach (var part in path.Split('/'))
                if ((current = Child(current, part)) == null) return null;
            return current;
        }

        private static ADObject? Child(ADObject parent, string name) =>
            parent.Children.FirstOrDefault(c => c.Type == ADObjectType.OrganizationalUnit && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

        private static ADObject Add(ADObject parent, ADObject child)
        {
            child.Parent = parent;
            parent.Children.Add(child);
            child.UpdateDistinguishedNameTree();
            return child;
        }

        /// <summary>
        /// SDProp passe dans l'heure : un compte ou un groupe devenu membre d'un groupe protégé (G-S-T0_Managers dans
        /// Enterprise Admins pour HardenAD) reçoit adminCount, Domain Admins pour propriétaire et les droits d'AdminSDHolder.
        /// </summary>
        private static void ApplySdProp(ADObject domain)
        {
            var context = new AuditContext(domain, DateTime.Now);
            foreach (var o in context.Objects.Where(o => o.Type is ADObjectType.User or ADObjectType.Group && !o.AdminCount
                                                         && context.IsMemberOfAny(o, context.ProtectedGroups)))
            {
                o.AdminCount = true;
                o.Owner = Owner;
                Allow(o, "Domain Admins", "WriteDacl");
                Allow(o, "Enterprise Admins", "WriteDacl");
                if (o.Type == ADObjectType.Group) Allow(o, "Everyone", "User-Change-Password", RightCategory.Other);
            }
        }

        private static void Allow(ADObject target, string trustee, string right, RightCategory category = RightCategory.WriteDacl)
        {
            if (target.Delegations.Any(d => d.TrusteeName == trustee && d.Right == right)) return;
            target.Delegations.Add(new ADDelegation
            {
                TrusteeName = trustee, TrusteeType = TrusteeType.Group, TargetDN = target.DistinguishedName,
                Right = right, RightCategory = category, Tier = target.Tier
            });
        }

        private static IEnumerable<ADObject> Flatten(ADObject root) =>
            new[] { root }.Concat(root.Children.SelectMany(Flatten));
    }
}
