using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SMADX.Models;

namespace SMADX.Services.Audit
{
    /// <summary>
    /// Vue indexée d'un domaine pour les règles d'audit : objets par nom, appartenance effective
    /// (imbrication des groupes suivie récursivement), comptes privilégiés, niveaux de tier.
    /// </summary>
    public sealed class AuditContext
    {
        private readonly Dictionary<string, ADObject> _groupsByName;
        private readonly Dictionary<ADObject, IReadOnlySet<ADObject>> _effectiveGroups = new();

        public ADObject Domain { get; }
        public IReadOnlyList<ADObject> Objects { get; }
        public DateTime Now { get; }

        /// <summary>Groupes protégés par AdminSDHolder présents dans le domaine.</summary>
        public IReadOnlySet<ADObject> ProtectedGroups { get; }

        /// <summary>Comptes (utilisateurs, ordinateurs, gMSA) membres effectifs d'un groupe protégé.</summary>
        public IReadOnlySet<ADObject> PrivilegedAccounts { get; }

        public IReadOnlyList<ADObject> DomainControllers { get; }

        public AuditContext(ADObject domain, DateTime now)
        {
            Domain = domain;
            Now = now;
            Objects = Flatten(domain).ToList();
            _groupsByName = Objects.Where(o => o.Type == ADObjectType.Group)
                .GroupBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            ProtectedGroups = BuiltInSecurityDefaults.ProtectedGroups
                .Select(name => _groupsByName.GetValueOrDefault(name))
                .OfType<ADObject>()
                .ToHashSet();
            PrivilegedAccounts = Objects.Where(o => o.IsAccount && IsMemberOfAny(o, ProtectedGroups)).ToHashSet();
            DomainControllers = Objects.Where(IsDomainController).ToList();
        }

        public static bool IsDomainController(ADObject o) =>
            o.Type == ADObjectType.Computer && o.Parent is { Type: ADObjectType.OrganizationalUnit, Name: "Domain Controllers" };

        public static bool IsEnabled(ADObject o) => !o.AccountFlags.HasFlag(ADAccountFlags.AccountDisabled);

        public bool IsPrivileged(ADObject o) => PrivilegedAccounts.Contains(o);

        public ADObject? Group(string name) => _groupsByName.GetValueOrDefault(name);

        /// <summary>Groupes dont l'objet est membre, directement ou par imbrication (sans boucle infinie).</summary>
        public IReadOnlySet<ADObject> EffectiveGroups(ADObject obj)
        {
            if (_effectiveGroups.TryGetValue(obj, out var cached))
                return cached;

            var result = new HashSet<ADObject>();
            var pending = new Stack<ADObject>(DirectGroups(obj));
            while (pending.Count > 0)
            {
                var group = pending.Pop();
                if (group == obj || !result.Add(group)) continue;
                foreach (var parent in DirectGroups(group))
                    pending.Push(parent);
            }
            return _effectiveGroups[obj] = result;
        }

        public bool IsMemberOfAny(ADObject obj, IEnumerable<ADObject> groups) => EffectiveGroups(obj).Overlaps(groups);

        /// <summary>Membres directs d'un groupe (objets dont MemberOf le cite).</summary>
        public IEnumerable<ADObject> DirectMembers(ADObject group) =>
            Objects.Where(o => o.MemberOf.Contains(group.Name, StringComparer.OrdinalIgnoreCase));

        private IEnumerable<ADObject> DirectGroups(ADObject obj) =>
            obj.MemberOf.Select(name => _groupsByName.GetValueOrDefault(name)).OfType<ADObject>();

        /// <summary>
        /// Niveau du tier de l'objet (0 = le plus sensible), d'après la configuration des tiers,
        /// ou le nombre dans « Tier N ». Null si l'objet n'a pas de tier.
        /// </summary>
        public static int? TierLevel(ADObject o)
        {
            if (string.IsNullOrWhiteSpace(o.Tier)) return null;
            var configured = TierConfigurationService.Instance.Tiers.FirstOrDefault(t => t.Name == o.Tier);
            if (configured != null) return configured.Level;
            var match = Regex.Match(o.Tier, @"\d+");
            return match.Success ? int.Parse(match.Value) : null;
        }

        /// <summary>Objet dont le DN est donné, ou null.</summary>
        public ADObject? ByDistinguishedName(string dn) =>
            Objects.FirstOrDefault(o => string.Equals(o.DistinguishedName, dn, StringComparison.OrdinalIgnoreCase));

        /// <summary>Compte ou groupe portant ce nom (SAMAccountName dans le simulateur).</summary>
        public ADObject? Principal(string name) =>
            Objects.FirstOrDefault(o => (o.IsAccount || o.Type == ADObjectType.Group) && string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>Âge en jours d'une date (null si inconnue).</summary>
        public int? AgeInDays(DateTime? date) => date is { } d ? (int)(Now - d).TotalDays : null;

        private static IEnumerable<ADObject> Flatten(ADObject root) =>
            new[] { root }.Concat(root.Children.SelectMany(Flatten));
    }
}
