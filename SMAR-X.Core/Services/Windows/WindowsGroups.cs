using System;
using System.Collections.Generic;
using System.Linq;
using SMARX.Models;

namespace SMARX.Services.Windows
{
    /// <summary>
    /// Les groupes d'un compte et les imbrications permises, comme Active Directory pour un serveur membre d'un domaine :
    /// <list type="bullet">
    /// <item>un groupe global contient des comptes du domaine et des groupes globaux ;</item>
    /// <item>un groupe de domaine local contient aussi des groupes de domaine local, mais pas un groupe du serveur ;</item>
    /// <item>un groupe local du serveur contient des comptes (du serveur ou du domaine), des groupes globaux et de domaine
    /// local, mais pas un autre groupe local ;</item>
    /// <item>un compte local du serveur n'entre que dans un groupe local.</item>
    /// </list>
    /// Le jeton d'un compte porte ses groupes et, de proche en proche, les groupes de ces groupes.
    /// </summary>
    public static class WindowsGroups
    {
        /// <summary>Les groupes du jeton : directs, puis imbriqués, dans l'ordre où on les rencontre, sans doublon.</summary>
        public static IReadOnlyList<string> Effective(WindowsHost host, WindowsUser user) =>
            EffectiveWithOrigin(host, user).Select(o => o.Group).ToList();

        /// <summary>
        /// Les groupes du jeton, chacun avec le groupe par lequel le compte le reçoit (null : il en est membre directement).
        /// Le premier chemin trouvé, le plus court.
        /// </summary>
        public static IReadOnlyList<(string Group, string? Via)> EffectiveWithOrigin(WindowsHost host, WindowsUser user)
        {
            var seen = new List<(string Group, string? Via)>();
            var queue = new Queue<(string Group, string? Via)>(user.Groups.Select(g => (g, (string?)null)));
            while (queue.Count > 0)
            {
                var (name, via) = queue.Dequeue();
                if (seen.Any(s => string.Equals(s.Group, name, StringComparison.OrdinalIgnoreCase))) continue;
                seen.Add((name, via));
                foreach (var parent in host.Group(name)?.Groups ?? []) queue.Enqueue((parent, name));
            }
            return seen;
        }

        /// <summary>
        /// Les comptes et les groupes du serveur, copiés : ce que Windows a lu à l'ouverture d'une session. Un changement de
        /// groupe ne compte qu'à la session suivante.
        /// </summary>
        public static WindowsHost Snapshot(WindowsHost host) => new()
        {
            Name = host.Name,
            Domain = host.Domain,
            Users = host.Users.Select(u => new WindowsUser { Name = u.Name, Domain = u.Domain, Groups = [.. u.Groups] }).ToList(),
            Groups = host.Groups.Select(g => new WindowsGroup { Name = g.Name, Description = g.Description, Scope = g.Scope, Groups = [.. g.Groups] }).ToList()
        };

        /// <summary>Les groupes obtenus par imbrication seulement (pas directement).</summary>
        public static IReadOnlyList<string> Nested(WindowsHost host, WindowsUser user) =>
            Effective(host, user).Where(g => !user.Groups.Contains(g, StringComparer.OrdinalIgnoreCase)).ToList();

        /// <summary>Membre d'Administrators, directement ou par un groupe imbriqué.</summary>
        public static bool IsAdministrator(WindowsHost host, WindowsUser user) =>
            Effective(host, user).Contains(WellKnown.Administrators, StringComparer.OrdinalIgnoreCase);

        public static WindowsGroupScope ScopeOf(WindowsHost host, string group) => host.Group(group)?.Scope ?? WindowsGroupScope.Local;

        /// <summary>
        /// Pourquoi un compte ou un groupe ne peut pas devenir membre d'un groupe, ou null s'il le peut. Users ne se
        /// choisit pas : tous les comptes en sont membres.
        /// </summary>
        public static string? CannotJoin(WindowsHost host, string member, string group, string culture)
        {
            var target = host.Group(group);
            if (target == null) return T(culture, $"Le groupe {group} n'existe pas.", $"Group {group} does not exist.");
            if (string.Equals(group, WellKnown.Users, StringComparison.OrdinalIgnoreCase))
                return T(culture, "Tous les comptes sont membres d'Utilisateurs.", "Every account is a member of Users.");

            if (host.User(member) is { } user)
                return user.Domain || target.Scope == WindowsGroupScope.Local ? null
                    : T(culture, $"{member} est un compte local du serveur : il n'entre pas dans un groupe du domaine.",
                                 $"{member} is a local account of the server: it cannot join a domain group.");

            var source = host.Group(member);
            if (source == null) return T(culture, $"{member} n'existe pas.", $"{member} does not exist.");
            // Les portées d'abord : c'est la règle d'Active Directory ; puis une imbrication qui tournerait en rond
            var scope = (target.Scope, source.Scope) switch
            {
                (_, WindowsGroupScope.Local) => T(culture, $"{member} est un groupe local du serveur : il ne peut être membre d'aucun groupe.",
                                                           $"{member} is a local group of the server: it cannot be a member of any group."),
                (WindowsGroupScope.Global, WindowsGroupScope.DomainLocal) =>
                    T(culture, $"Un groupe de domaine local ({member}) n'entre pas dans un groupe global ({group}).",
                               $"A domain local group ({member}) cannot join a global group ({group})."),
                _ => null
            };
            if (scope != null) return scope;
            return string.Equals(member, group, StringComparison.OrdinalIgnoreCase) || Contains(host, member, group)
                ? T(culture, $"{group} est déjà membre de {member} : l'imbrication tournerait en rond.",
                             $"{group} is already a member of {member}: the nesting would loop.")
                : null;
        }

        /// <summary>Le groupe <paramref name="ancestor"/> contient <paramref name="group"/>, directement ou par imbrication.</summary>
        private static bool Contains(WindowsHost host, string ancestor, string group)
        {
            var queue = new Queue<string>(host.Group(group)?.Groups ?? []);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (queue.Count > 0)
            {
                var name = queue.Dequeue();
                if (!seen.Add(name)) continue;
                if (string.Equals(name, ancestor, StringComparison.OrdinalIgnoreCase)) return true;
                foreach (var parent in host.Group(name)?.Groups ?? []) queue.Enqueue(parent);
            }
            return false;
        }

        /// <summary>Les membres directs d'un groupe : ses comptes, puis ses groupes.</summary>
        public static IReadOnlyList<string> Members(WindowsHost host, string group) =>
            host.Users.Where(u => u.Groups.Contains(group, StringComparer.OrdinalIgnoreCase)).Select(u => u.Name)
                .Concat(host.Groups.Where(g => g.Groups.Contains(group, StringComparer.OrdinalIgnoreCase)).Select(g => g.Name))
                .ToList();

        /// <summary>« global », « domaine local », « local » : l'étiquette d'une portée.</summary>
        public static string ScopeLabel(WindowsGroupScope scope, string culture) => scope switch
        {
            WindowsGroupScope.Global => T(culture, "global", "global"),
            WindowsGroupScope.DomainLocal => T(culture, "domaine local", "domain local"),
            _ => T(culture, "local", "local")
        };

        /// <summary>
        /// La commande qui ajoute un membre à un groupe : Add-ADGroupMember pour un groupe du domaine, net localgroup pour
        /// un groupe du serveur (un membre du domaine y est nommé avec son domaine).
        /// </summary>
        public static string AddMemberCommand(WindowsHost host, string member, string group) =>
            host.IsDomain(group)
                ? $"Add-ADGroupMember -Identity {group} -Members {member}"
                : $"net localgroup \"{group}\" {(host.IsDomain(member) ? host.Qualified(member) : member)} /add";

        /// <summary>La commande qui crée un groupe : New-ADGroup pour le domaine, net localgroup pour le serveur.</summary>
        public static string CreateCommand(WindowsHost host, WindowsGroup group) => group.Scope switch
        {
            WindowsGroupScope.Global => $"New-ADGroup -Name {group.Name} -GroupScope Global -GroupCategory Security",
            WindowsGroupScope.DomainLocal => $"New-ADGroup -Name {group.Name} -GroupScope DomainLocal -GroupCategory Security",
            _ => $"net localgroup \"{group.Name}\" /add"
        };

        private static string T(string culture, string fr, string en) =>
            culture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? en : fr;
    }
}
