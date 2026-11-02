using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SMADX.Models;
using SMADX.Services.AttackPaths;
using SMADX.Services.Audit;

namespace SMADX.Services.Tiering
{
    public enum TierMigrationAction
    {
        /// <summary>Ranger l'objet dans l'OU du modèle qui correspond à son rôle et à son tier.</summary>
        Move,
        /// <summary>Ajouter un compte ou un groupe à un groupe du modèle, pour qu'il garde un droit qu'il avait.</summary>
        AddMember
    }

    /// <summary>Étape du plan de migration. <paramref name="To"/> est une OU (déplacement) ou un groupe (adhésion).</summary>
    public sealed record TierMigrationStep(TierMigrationAction Action, string ObjectName, ADObjectType ObjectType, string From, string To, string Reason);

    /// <summary>Ce que la migration ne règle pas seule : un objet sans OU dans le modèle, un droit sans équivalent.</summary>
    public sealed record TierMigrationIssue(string ObjectName, string Reason);

    public sealed record TierMigrationResult(TierModelReport Applied, IReadOnlyList<TierMigrationStep> Steps, IReadOnlyList<TierMigrationIssue> Issues)
    {
        public int Moves => Steps.Count(s => s.Action == TierMigrationAction.Move);
        public int Memberships => Steps.Count(s => s.Action == TierMigrationAction.AddMember);
    }

    /// <summary>
    /// Migration d'un domaine vers un modèle de cloisonnement : le modèle est appliqué, puis chaque compte, ordinateur ou
    /// groupe est rangé dans l'OU du modèle qui correspond à son rôle et à son tier. Les droits légitimes sont gardés : un
    /// administrateur local que les groupes restreints d'une GPO remplacent, ou le mandataire d'une délégation posée sur
    /// une ancienne OU, devient membre du groupe du modèle qui détient ce droit. Ce que le plan ne peut pas régler est
    /// signalé. Le tier d'un objet est son étiquette ; sans étiquette, le Tier 0 pour un compte ou un groupe privilégié,
    /// sinon un tier supposé (Tier 1 pour un serveur, Tier 2 pour le reste).
    /// </summary>
    public static class TierMigrationService
    {
        private static LocalizationService Loc => LocalizationService.Instance;

        /// <summary>Groupes qui ont le contrôle total du domaine : rien à garder pour eux.</summary>
        private static readonly IReadOnlySet<string> DomainOwners = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Domain Admins", "Enterprise Admins", "Administrators"
        };

        public static TierMigrationResult Migrate(ADObject domain, TierModelDefinition model, DateTime? now = null, bool registerTiers = true)
        {
            var date = now ?? DateTime.Now;
            var applied = TierModelService.Apply(domain, model, date, registerTiers);
            var before = new AuditContext(domain, date);
            var modelGroups = model.Groups.Select(g => g.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            // Nombre de délégations de chaque groupe du modèle : le plus spécifique garde un droit, pas le plus large
            var breadth = model.Delegations.GroupBy(d => d.Trustee, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
            var ous = model.OrganizationalUnits.ToDictionary(o => o.Path, StringComparer.OrdinalIgnoreCase);
            var steps = new List<TierMigrationStep>();
            var issues = new List<TierMigrationIssue>();

            bool Kept(string trustee) =>
                !AttackPathService.AnyUserTrustees.Contains(trustee) && !DomainOwners.Contains(trustee)
                && !modelGroups.Contains(trustee) && !TierModelCompliance.IsDefaultPrincipal(trustee) && before.Principal(trustee) != null;

            // Ce qu'il faut ranger, et qui en a la charge avant la migration (délégations, administration locale)
            var movable = TierModelCompliance.Movable(before)
                .Where(o => TierModelCompliance.ModelOu(o, ous) is not { } place || place.Tier == null)
                .ToList();
            var movableSet = movable.ToHashSet();
            var reached = before.Objects
                .Where(h => h.Type is ADObjectType.OrganizationalUnit or ADObjectType.Container or ADObjectType.Domain)
                .SelectMany(h => h.Delegations.Where(d => !d.IsInherited && d.Effective != RightCategory.Other && Kept(d.TrusteeName))
                    .SelectMany(d => Descendants(h).Where(t => movableSet.Contains(t) && Concerns(d.Effective, t)).Select(t => (d.TrusteeName, Category: d.Effective, Target: t))))
                .ToList();
            var oldAdmins = movable.Where(o => o.Type == ADObjectType.Computer)
                .ToDictionary(o => o, o => before.Policies.LocalAdministrators(o).Select(a => a.Name).ToList());
            var administrators = reached.Select(r => r.TrusteeName)
                .Concat(oldAdmins.Values.SelectMany(a => a))
                .Where(Kept).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 1. Déplacements : groupes, comptes, puis ordinateurs
            foreach (var o in movable.OrderBy(o => o.Type switch { ADObjectType.Group => 0, ADObjectType.Computer => 2, _ => 1 })
                                     .ThenBy(o => o.Name, StringComparer.OrdinalIgnoreCase))
            {
                var (role, tier, level, source) = Classify(o, before, administrators);
                var reason = string.Format(Loc["Tiering.Reason"], Loc[$"Tiering.Role.{role}"], tier ?? $"Tier {level}", Loc[$"Tiering.Source.{source}"]);
                if (Target(model, role, tier, level, AuditContext.IsEnabled(o) || !o.IsAccount) is not { } target
                    || TierModelService.Ou(domain, target.Path) is not { } ou)
                {
                    issues.Add(new TierMigrationIssue(o.Name, string.Format(Loc["Tiering.Issue.NoOu"], reason)));
                    continue;
                }
                steps.Add(new TierMigrationStep(TierMigrationAction.Move, o.Name, o.Type, TierModelCompliance.Location(o), target.Path, reason));
                Move(o, ou);
                o.Tier ??= target.Tier;
            }

            // 2. Droits à garder, une fois chaque objet à sa place
            var after = new AuditContext(domain, date);
            var added = new HashSet<(string, string)>(TupleComparer);
            var lost = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void AddMember(string member, string group, string reason)
            {
                if (!added.Add((member, group)) || after.Principal(member) is not { } obj) return;
                if (!obj.MemberOf.Contains(group, StringComparer.OrdinalIgnoreCase)) obj.MemberOf.Add(group);
                steps.Add(new TierMigrationStep(TierMigrationAction.AddMember, obj.Name, obj.Type, string.Empty, group, reason));
            }
            void Lose(string trustee, string reason)
            {
                if (lost.Add($"{trustee}|{reason}")) issues.Add(new TierMigrationIssue(trustee, reason));
            }

            // Un groupe du modèle ne se rejoint que depuis son tier ou un tier plus sensible : jamais d'escalade
            var groupLevels = model.Groups.ToDictionary(g => g.Name, g => LevelOf(model, g.Tier) ?? 0, StringComparer.OrdinalIgnoreCase);
            bool MayJoin(string member, string group) =>
                groupLevels.TryGetValue(group, out var level) && level >= (after.Principal(member) is { } m ? AuditContext.TierLevel(m) ?? 2 : 2);

            foreach (var (computer, admins) in oldAdmins)
            {
                if (after.Policies.Applied(computer).FirstOrDefault(g => g.GpoReplaceLocalAdmins && g.GpoLocalAdmins != null) is not { } gpo) continue;
                var current = after.Policies.LocalAdministrators(computer);
                foreach (var admin in admins.Where(a => Kept(a) && !current.Any(n => string.Equals(n.Name, a, StringComparison.OrdinalIgnoreCase))))
                    if (gpo.GpoLocalAdmins!.FirstOrDefault(g => modelGroups.Contains(g) && MayJoin(admin, g)) is { } group)
                        AddMember(admin, group, string.Format(Loc["Tiering.Reason.LocalAdmin"], computer.Name, gpo.Name));
                    else
                        Lose(admin, string.Format(Loc["Tiering.Issue.LostLocalAdmin"], computer.Name, gpo.Name));
            }

            // Délégations : pour chaque mandataire et chaque nouvelle OU, les droits qu'il n'a plus, gardés par le moins de
            // groupes du modèle possible (celui qui en couvre le plus, puis le plus spécifique)
            foreach (var need in reached
                         .Where(r => !Ancestors(r.Target).Any(a => a.Delegations.Any(d => !d.IsInherited && d.Effective == r.Category
                                                                                        && string.Equals(d.TrusteeName, r.TrusteeName, StringComparison.OrdinalIgnoreCase))))
                         .GroupBy(r => (r.TrusteeName, Place: r.Target.Parent!), new TrusteePlaceComparer()))
            {
                var (trustee, place) = need.Key;
                var missing = need.Select(r => r.Category).Distinct().ToHashSet();
                var offers = new[] { place }.Concat(Ancestors(place)).SelectMany(a => a.Delegations)
                    .Where(d => !d.IsInherited && missing.Contains(d.Effective) && modelGroups.Contains(d.TrusteeName) && MayJoin(trustee, d.TrusteeName))
                    .GroupBy(d => d.TrusteeName, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Select(d => d.Effective).ToHashSet(), StringComparer.OrdinalIgnoreCase);
                var location = TierModelCompliance.OuPath(place) ?? place.Name;
                while (missing.Count > 0 && offers
                           .OrderByDescending(o => o.Value.Count(missing.Contains))
                           .ThenBy(o => breadth.GetValueOrDefault(o.Key, int.MaxValue))
                           .FirstOrDefault(o => o.Value.Any(missing.Contains)) is { Key: not null } best)
                {
                    var kept = missing.Where(best.Value.Contains).ToList();
                    AddMember(trustee, best.Key, string.Format(Loc["Tiering.Reason.Delegation"], Rights(kept), location));
                    missing.ExceptWith(kept);
                }
                if (missing.Count > 0)
                    Lose(trustee, string.Format(Loc["Tiering.Issue.LostRight"], Rights(missing), location));
            }

            TierModelService.ApplySdProp(domain);
            return new TierMigrationResult(applied, steps, issues);
        }

        /// <summary>Rôle et tier d'un objet à ranger, et d'où vient son tier (étiquette, privilège, supposé).</summary>
        private static (TierModelOuRole Role, string? Tier, int Level, string Source) Classify(ADObject o, AuditContext c, IReadOnlySet<string> administrators)
        {
            var (tier, level, source) = AuditContext.TierLevel(o) is { } labelled ? (o.Tier, labelled, "Label")
                : c.IsPrivilegedPrincipal(o) ? (null, 0, "Privileged")
                : o.Type == ADObjectType.Computer && ServerHint(o) == true ? (null, 1, "Assumed")
                : ((string?)null, 2, "Assumed");
            var administers = administrators.Contains(o.Name) || c.EffectiveGroups(o).Any(g => administrators.Contains(g.Name));
            var role = o.Type switch
            {
                ADObjectType.Computer => (ServerHint(o) ?? level <= 1) ? TierModelOuRole.Servers
                    : level >= 2 ? TierModelOuRole.Workstations : TierModelOuRole.AdminWorkstations,
                ADObjectType.GMSA => TierModelOuRole.ServiceAccounts,
                ADObjectType.User when o.ServicePrincipalNames.Count > 0 => TierModelOuRole.ServiceAccounts,
                ADObjectType.User => level <= 1 || administers ? TierModelOuRole.AdminAccounts : TierModelOuRole.Users,
                _ => level <= 1 || administers ? TierModelOuRole.AdminGroups : TierModelOuRole.Groups
            };
            return (role, tier, level, source);
        }

        /// <summary>Une délégation de cette catégorie vise-t-elle ce type d'objet (réinitialiser un mot de passe : un utilisateur…) ?</summary>
        private static bool Concerns(RightCategory category, ADObject target) => category switch
        {
            RightCategory.PasswordReset or RightCategory.AccountUnlock or RightCategory.WriteSpn => target.Type == ADObjectType.User,
            RightCategory.ComputerManagement or RightCategory.ReadLapsPassword or RightCategory.WriteAllowedToAct => target.Type == ADObjectType.Computer,
            RightCategory.WriteMembers => target.Type == ADObjectType.Group,
            RightCategory.ReadGmsaPassword => target.Type == ADObjectType.GMSA,
            RightCategory.WriteKeyCredentialLink => target.Type is ADObjectType.User or ADObjectType.Computer,
            _ => true
        };

        /// <summary>Serveur ou poste d'après le nom de l'ordinateur et de ses OU (SRV, Server, Serveur ; WKS, PC, Poste…) ; null si rien ne le dit.</summary>
        private static bool? ServerHint(ADObject computer)
        {
            var names = new[] { computer.Name }.Concat(Ancestors(computer).Where(a => a.Type != ADObjectType.Domain).Select(a => a.Name)).ToList();
            if (names.Any(n => Regex.IsMatch(n, @"(^|[^a-z])(srv|server|servers|serveur|serveurs)([^a-z]|$)", RegexOptions.IgnoreCase))) return true;
            if (names.Any(n => Regex.IsMatch(n, @"(^|[^a-z])(wks|workstation|workstations|pc|poste|postes|laptop|desktop)([^a-z]|$)", RegexOptions.IgnoreCase))) return false;
            return null;
        }

        /// <summary>
        /// OU du modèle pour ce rôle et ce tier : celle du même tier, sinon du même niveau ; la moins profonde. Un compte
        /// désactivé va dans l'OU Disabled qu'elle contient, quand le modèle en prévoit une.
        /// </summary>
        private static TierModelOu? Target(TierModelDefinition model, TierModelOuRole role, string? tier, int level, bool enabled)
        {
            var candidates = model.OrganizationalUnits.Where(o => o.Role == role).ToList();
            var target = candidates.Where(o => o.Tier == tier && tier != null).Concat(candidates.Where(o => LevelOf(model, o.Tier) == level))
                .OrderBy(o => o.Tier == tier ? 0 : 1).ThenBy(o => o.Path.Count(ch => ch == '/'))
                .FirstOrDefault();
            if (target == null || enabled) return target;
            return model.OrganizationalUnits.FirstOrDefault(o => o.Role == TierModelOuRole.Disabled && o.Path.StartsWith(target.Path + "/", StringComparison.OrdinalIgnoreCase))
                   ?? target;
        }

        /// <summary>Niveau d'un tier du modèle (Tier 1 Legacy : 1) ; null pour un objet sans tier.</summary>
        private static int? LevelOf(TierModelDefinition model, string? tier) =>
            tier == null ? null
            : model.Tiers.FirstOrDefault(t => t.Name == tier)?.Level ?? (Regex.Match(tier, @"\d+") is { Success: true } m ? int.Parse(m.Value) : null);

        /// <summary>Noms des droits, dans l'ordre des catégories.</summary>
        private static string Rights(IEnumerable<RightCategory> categories) =>
            string.Join(", ", categories.Order().Select(c => Loc[$"Delegation.Category.{c}"]));

        private static void Move(ADObject o, ADObject target)
        {
            o.Parent?.Children.Remove(o);
            o.Parent = target;
            target.Children.Add(o);
            o.UpdateDistinguishedNameTree();
        }

        private static IEnumerable<ADObject> Ancestors(ADObject o)
        {
            for (var p = o.Parent; p != null; p = p.Parent) yield return p;
        }

        private static IEnumerable<ADObject> Descendants(ADObject root) => root.Children.SelectMany(c => new[] { c }.Concat(Descendants(c)));

        private static readonly IEqualityComparer<(string, string)> TupleComparer = new CaseInsensitivePair();

        /// <summary>Mandataire (casse ignorée) et OU (même objet).</summary>
        private sealed class TrusteePlaceComparer : IEqualityComparer<(string TrusteeName, ADObject Place)>
        {
            public bool Equals((string TrusteeName, ADObject Place) x, (string TrusteeName, ADObject Place) y) =>
                StringComparer.OrdinalIgnoreCase.Equals(x.TrusteeName, y.TrusteeName) && ReferenceEquals(x.Place, y.Place);

            public int GetHashCode((string TrusteeName, ADObject Place) obj) =>
                HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.TrusteeName), System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Place));
        }

        private sealed class CaseInsensitivePair : IEqualityComparer<(string, string)>
        {
            public bool Equals((string, string) x, (string, string) y) =>
                StringComparer.OrdinalIgnoreCase.Equals(x.Item1, y.Item1) && StringComparer.OrdinalIgnoreCase.Equals(x.Item2, y.Item2);

            public int GetHashCode((string, string) obj) =>
                HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item1), StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item2));
        }
    }
}
