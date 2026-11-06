using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SMARX.Models;

namespace SMARX.Services.Windows
{
    /// <summary>D'où vient une identité du jeton : ce qui l'explique, dans la fenêtre whoami.</summary>
    public enum WhoamiOrigin { Everyone, AllAccounts, Session, Authenticated, Direct, Nested, Label }

    /// <summary>
    /// Une ligne de « whoami /groups » : le nom tel que Windows l'écrit (« CORP\G_RH », « BUILTIN\Users »), le type
    /// (Well-known group, Alias, Group, Label), le SID et les attributs. <see cref="Key"/> est le nom dans SMAR-X ;
    /// <see cref="Via"/>, pour un groupe imbriqué, le groupe par lequel le compte le reçoit.
    /// </summary>
    public sealed record WhoamiEntry(string Key, string Name, string Type, string Sid, string Attributes, WhoamiOrigin Origin, string? Via, bool DenyOnly);

    /// <summary>Le jeton d'une session, tel que l'affiche « whoami /user /groups ».</summary>
    public sealed record WhoamiToken(string User, string UserSid, IReadOnlyList<WhoamiEntry> Groups, bool Network, bool EffectiveAdministrator)
    {
        /// <summary>Le texte de « whoami /user /groups » sur un Windows en anglais : colonnes alignées, soulignées de « = ».</summary>
        public string Text()
        {
            var s = new StringBuilder();
            Lines(s, "USER INFORMATION", "----------------", "");
            Table(s, ["User Name", "SID"], [[User, UserSid]]);
            Lines(s, "", "", "GROUP INFORMATION", "-----------------", "");
            Table(s, ["Group Name", "Type", "SID", "Attributes"], Groups.Select(g => new[] { g.Name, g.Type, g.Sid, g.Attributes }).ToList());
            return s.ToString();
        }

        /// <summary>Des lignes terminées par un saut de ligne simple (LF), comme le reste du texte.</summary>
        private static void Lines(StringBuilder s, params string[] lines)
        {
            foreach (var line in lines) s.Append(line).Append((char)10);
        }

        private static void Table(StringBuilder s, string[] header, IReadOnlyList<string[]> rows)
        {
            var widths = header.Select((h, i) => Math.Max(h.Length, rows.Select(r => r[i].Length).DefaultIfEmpty(0).Max())).ToArray();
            string Line(IEnumerable<string> cells) => string.Join(" ", cells.Select((c, i) => c.PadRight(widths[i]))).TrimEnd();
            Lines(s, Line(header), Line(widths.Select(w => new string('=', w))));
            Lines(s, rows.Select(Line).ToArray());
        }
    }

    /// <summary>
    /// « whoami /user /groups » d'un compte, construit à partir du jeton que SMAR-X donne à AccessCheck
    /// (<see cref="WindowsToken"/>) : le compte, Tout le monde, Utilisateurs, la session (INTERACTIVE ou NETWORK),
    /// Utilisateurs authentifiés, ses groupes et ceux de ses groupes, Administrators (en « refus seulement » sans élévation)
    /// et le niveau d'intégrité. Un vrai jeton porte quelques identités de plus (LOCAL, CONSOLE LOGON, This Organization,
    /// Domain Users…) : elles ne changent rien aux accès que SMAR-X simule, il ne les montre pas.
    /// </summary>
    public static class WindowsWhoami
    {
        private const string Standard = "Mandatory group, Enabled by default, Enabled group";

        public static WhoamiToken Of(WindowsHost host, WindowsUser user, bool network, bool elevated)
        {
            var token = network ? WindowsToken.Remote(host, user) : WindowsToken.Local(host, user, elevated);
            var origins = WindowsGroups.EffectiveWithOrigin(host, user).ToDictionary(o => o.Group, o => o.Via, StringComparer.OrdinalIgnoreCase);
            var entries = new List<WhoamiEntry>
            {
                WellKnownEntry(WellKnown.Everyone, "Everyone", "S-1-1-0", WhoamiOrigin.Everyone),
                new(WellKnown.Users, @"BUILTIN\Users", "Alias", "S-1-5-32-545", Standard, WhoamiOrigin.AllAccounts, null, false)
            };
            if (token.Sids.FirstOrDefault(s => Same(s.Name, WellKnown.Administrators)) is { } admins)
                entries.Add(new(WellKnown.Administrators, @"BUILTIN\Administrators", "Alias", "S-1-5-32-544",
                    admins.DenyOnly ? "Group used for deny only" : Standard + ", Group owner",
                    origins.TryGetValue(WellKnown.Administrators, out var adminVia) && adminVia != null ? WhoamiOrigin.Nested : WhoamiOrigin.Direct,
                    adminVia, admins.DenyOnly));

            // Les groupes créés : ceux du serveur, puis les globaux du domaine, puis ceux de domaine local (comme whoami)
            var groups = WindowsGroups.Effective(host, user)
                .Where(g => !Same(g, WellKnown.Administrators) && !Same(g, WellKnown.Users))
                .Select(g => (Name: g, Scope: WindowsGroups.ScopeOf(host, g)))
                .ToList();
            WhoamiEntry Group(string name, string type, string attributes) =>
                new(name, host.Qualified(name), type, NtfsSecurityTab.Sid(host, name), attributes,
                    origins.GetValueOrDefault(name) == null ? WhoamiOrigin.Direct : WhoamiOrigin.Nested, origins.GetValueOrDefault(name), false);
            entries.AddRange(groups.Where(g => g.Scope == WindowsGroupScope.Local).Select(g => Group(g.Name, "Alias", Standard)));
            entries.Add(network
                ? WellKnownEntry(WellKnown.Network, @"NT AUTHORITY\NETWORK", "S-1-5-2", WhoamiOrigin.Session)
                : WellKnownEntry(WellKnown.Interactive, @"NT AUTHORITY\INTERACTIVE", "S-1-5-4", WhoamiOrigin.Session));
            entries.Add(WellKnownEntry(WellKnown.AuthenticatedUsers, @"NT AUTHORITY\Authenticated Users", "S-1-5-11", WhoamiOrigin.Authenticated));
            entries.AddRange(groups.Where(g => g.Scope == WindowsGroupScope.Global).Select(g => Group(g.Name, "Group", Standard)));
            entries.AddRange(groups.Where(g => g.Scope == WindowsGroupScope.DomainLocal).Select(g => Group(g.Name, "Alias", Standard + ", Local Group")));
            entries.Add(token.Elevated
                ? new("Label", @"Mandatory Label\High Mandatory Level", "Label", "S-1-16-12288", "", WhoamiOrigin.Label, null, false)
                : new("Label", @"Mandatory Label\Medium Mandatory Level", "Label", "S-1-16-8192", "", WhoamiOrigin.Label, null, false));

            var qualified = host.Qualified(user.Name);
            return new WhoamiToken(qualified.ToLowerInvariant(), NtfsSecurityTab.Sid(host, user.Name), entries, network, token.Elevated);
        }

        private static WhoamiEntry WellKnownEntry(string key, string name, string sid, WhoamiOrigin origin) =>
            new(key, name, "Well-known group", sid, Standard, origin, null, false);

        private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
