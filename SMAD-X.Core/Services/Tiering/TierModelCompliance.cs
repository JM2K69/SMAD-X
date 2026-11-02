using System;
using System.Collections.Generic;
using System.Linq;
using SMADX.Models;
using SMADX.Services.Audit;

namespace SMADX.Services.Tiering
{
    /// <summary>
    /// Écarts d'un domaine au modèle de cloisonnement qui lui a été appliqué (règles TIER-05 à TIER-08) : ce qui
    /// manque au modèle, et les objets que le cloisonnement n'a pas encore rangés.
    /// </summary>
    public static class TierModelCompliance
    {
        private static LocalizationService Loc => LocalizationService.Instance;

        /// <summary>
        /// Comptes et groupes qu'AD crée à la promotion (Administrator, Domain Admins…), quelle que soit la version :
        /// ils restent dans Users ou Builtin, aucun modèle ne les déplace.
        /// </summary>
        private static readonly Lazy<HashSet<string>> DefaultPrincipals = new(() =>
            WindowsServerProfile.All
                .SelectMany(p => Flatten(new ADDataService().CreateFreshDomainStructure("defaults.local", false, p.Version)))
                .Where(o => o.IsAccount || o.Type == ADObjectType.Group)
                .Select(o => o.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase));

        public static TierModelDefinition? ModelOf(AuditContext c) => TierModelCatalog.Find(c.Domain.TierModel);

        /// <summary>TIER-05 : OU, groupe, compte, GPO, lien de GPO ou délégation du modèle absent du domaine.</summary>
        public static IEnumerable<(ADObject, object[])> Missing(AuditContext c)
        {
            if (ModelOf(c) is not { } model) yield break;
            var domain = c.Domain;
            var principals = c.Objects.Where(o => o.IsAccount || o.Type == ADObjectType.Group).Select(o => o.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var gpos = c.Objects.Where(o => o.Type == ADObjectType.Policy).Select(o => o.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            (ADObject, object[]) Absent(ADObject o, string kind, string what) => (o, [Loc[$"Tiering.Kind.{kind}"], model.Name, what]);

            foreach (var ou in model.OrganizationalUnits.Where(o => TierModelService.Ou(domain, o.Path) == null))
                yield return Absent(domain, "OrganizationalUnit", ou.Path);
            foreach (var g in model.Groups.Where(g => !principals.Contains(g.Name)))
                yield return Absent(domain, "Group", g.Name);
            foreach (var a in model.Accounts.Where(a => !principals.Contains(a.Name)))
                yield return Absent(domain, "Account", a.Name);
            foreach (var gpo in model.GroupPolicies)
            {
                if (!gpos.Contains(gpo.Name))
                {
                    yield return Absent(domain, "GroupPolicy", gpo.Name);
                    continue;
                }
                foreach (var link in gpo.Links.Where(l => l.Enabled))
                {
                    var target = link.Target.Length == 0 ? domain : TierModelService.Ou(domain, link.Target);
                    if (target != null && !target.LinkedGPOs.Contains(gpo.Name, StringComparer.OrdinalIgnoreCase))
                        yield return Absent(target, "GroupPolicyLink", gpo.Name);
                }
            }
            foreach (var d in model.Delegations)
            {
                var target = TierModelService.Ou(domain, d.Target);
                if (target == null || !principals.Contains(d.Trustee)) continue; // déjà signalé : l'OU ou le mandataire manque
                if (!target.Delegations.Any(x => !x.IsInherited && string.Equals(x.TrusteeName, d.Trustee, StringComparison.OrdinalIgnoreCase)
                                                 && string.Equals(x.Right, d.Right, StringComparison.OrdinalIgnoreCase)))
                    yield return Absent(target, "Delegation", $"{d.Trustee} : {d.Right}");
            }
        }

        /// <summary>TIER-06 : compte ou groupe rangé hors des OU classées du modèle (dans Users, Computers, une ancienne OU…).</summary>
        public static IEnumerable<(ADObject, object[])> Outside(AuditContext c)
        {
            if (ModelOf(c) is not { } model) yield break;
            var ous = OusOf(model);
            foreach (var o in Movable(c))
            {
                var place = ModelOu(o, ous);
                if (place == null || place.Tier == null && !IsTransit(place))
                    yield return (o, [Location(o), model.Name]);
            }
        }

        /// <summary>TIER-07 : compte ou groupe resté dans une OU de transit (Staging, Quarantine, Provisioning).</summary>
        public static IEnumerable<(ADObject, object[])> Waiting(AuditContext c)
        {
            if (ModelOf(c) is not { } model) yield break;
            var ous = OusOf(model);
            foreach (var o in Movable(c))
                if (ModelOu(o, ous) is { } place && IsTransit(place))
                    yield return (o, [place.Path]);
        }

        /// <summary>TIER-08 : compte ou groupe privilégié rangé dans une OU d'un tier moins sensible que le Tier 0.</summary>
        public static IEnumerable<(ADObject, object[])> PrivilegedOutsideTier0(AuditContext c)
        {
            if (ModelOf(c) is not { } model) yield break;
            var ous = OusOf(model);
            foreach (var o in Movable(c).Where(c.IsPrivilegedPrincipal))
                if (ModelOu(o, ous) is { Tier: { } tier } place && TierLevel(tier) > 0)
                    yield return (o, [place.Path, tier]);
        }

        private static Dictionary<string, TierModelOu> OusOf(TierModelDefinition model) =>
            model.OrganizationalUnits.ToDictionary(o => o.Path, StringComparer.OrdinalIgnoreCase);

        private static bool IsTransit(TierModelOu ou) => ou.Role is TierModelOuRole.Staging or TierModelOuRole.Quarantine or TierModelOuRole.Provisioning;

        /// <summary>Comptes et groupes que le cloisonnement doit ranger : ni intégrés à AD, ni contrôleurs de domaine.</summary>
        private static IEnumerable<ADObject> Movable(AuditContext c) =>
            c.Objects.Where(o => (o.IsAccount || o.Type == ADObjectType.Group)
                                 && !AuditContext.IsDomainController(o)
                                 && !DefaultPrincipals.Value.Contains(o.Name));

        /// <summary>OU du modèle qui contient directement l'objet, ou null s'il est ailleurs.</summary>
        private static TierModelOu? ModelOu(ADObject o, Dictionary<string, TierModelOu> ous)
        {
            var path = OuPath(o.Parent);
            return path != null && ous.TryGetValue(path, out var ou) ? ou : null;
        }

        /// <summary>Chemin d'une OU depuis la racine (« Harden_T0/Servers ») ; null si un conteneur s'intercale.</summary>
        private static string? OuPath(ADObject? container)
        {
            var parts = new List<string>();
            for (var p = container; p != null && p.Type != ADObjectType.Domain; p = p.Parent)
            {
                if (p.Type != ADObjectType.OrganizationalUnit) return null;
                parts.Insert(0, p.Name);
            }
            return parts.Count == 0 ? null : string.Join('/', parts);
        }

        private static string Location(ADObject o) =>
            o.Parent is { } parent && parent.Type != ADObjectType.Domain ? OuPath(parent) ?? parent.Name : o.Parent?.Name ?? string.Empty;

        private static int TierLevel(string tier) =>
            TierConfigurationService.Instance.Tiers.FirstOrDefault(t => t.Name == tier)?.Level
            ?? (System.Text.RegularExpressions.Regex.Match(tier, @"\d+") is { Success: true } m ? int.Parse(m.Value) : 0);

        private static IEnumerable<ADObject> Flatten(ADObject root) =>
            new[] { root }.Concat(root.Children.SelectMany(Flatten));
    }
}
