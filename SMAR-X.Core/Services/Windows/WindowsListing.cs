using System.Collections.Generic;
using System.Linq;
using SMARX.Models;

namespace SMARX.Services.Windows
{
    /// <summary>Les autorisations telles que les montre icacls, et les commandes icacls qui les recréent.</summary>
    public static class WindowsListing
    {
        /// <summary>Nom complet d'une identité, comme icacls : « BUILTIN\Administrators », « NT AUTHORITY\SYSTEM », « SRV-FIC\carol », « CORP\G_RH ».</summary>
        public static string Account(WindowsHost host, string name) => name switch
        {
            WellKnown.Administrators or WellKnown.Users => "BUILTIN\\" + name,
            WellKnown.System or WellKnown.AuthenticatedUsers or WellKnown.Interactive or WellKnown.Network => "NT AUTHORITY\\" + name,
            WellKnown.Everyone or WellKnown.CreatorOwner or WellKnown.OwnerRights => name,
            _ => host.Qualified(name)
        };

        /// <summary>« (I)(OI)(CI)(M) », « (DENY)(W) » : héritée, héritage, refus, droits.</summary>
        public static string Entry(EffectiveAce ace)
        {
            var flags = (ace.Inherited ? "(I)" : "")
                        + (ace.Inheritance.HasFlag(AceInheritance.ObjectInherit) ? "(OI)" : "")
                        + (ace.Inheritance.HasFlag(AceInheritance.ContainerInherit) ? "(CI)" : "")
                        + (ace.Inheritance.HasFlag(AceInheritance.InheritOnly) ? "(IO)" : "")
                        + (ace.Inheritance.HasFlag(AceInheritance.NoPropagate) ? "(NP)" : "")
                        + (ace.Type == AceType.Deny ? "(DENY)" : "");
            var rights = Ntfs.Format(ace.Mask);
            return flags + (rights.StartsWith('(') ? rights : $"({rights})");
        }

        /// <summary>Sortie d'icacls pour un chemin : une ligne par ACE de la DACL effective.</summary>
        public static IReadOnlyList<string> Icacls(WindowsHost host, string path)
        {
            var lines = new List<string>();
            var dacl = NtfsInheritance.Dacl(host, path);
            for (var i = 0; i < dacl.Count; i++)
                lines.Add($"{(i == 0 ? path : new string(' ', path.Length))} {Account(host, dacl[i].Trustee)}:{Entry(dacl[i])}");
            return lines;
        }

        /// <summary>
        /// Commandes qui donnent à un fichier ou un dossier existant son propriétaire, son héritage et ses ACE explicites :
        /// icacls /setowner, /inheritance:e (activé) ou :r (désactivé, sans les ACE héritées), /grant:r et /deny.
        /// </summary>
        public static IReadOnlyList<string> Commands(WindowsHost host, NtfsNode node, string path)
        {
            var quoted = $"\"{path}\"";
            var lines = new List<string>
            {
                $"icacls {quoted} /setowner \"{Account(host, node.Owner)}\"",
                $"icacls {quoted} /inheritance:{(node.InheritanceEnabled ? "e" : "r")}"
            };
            foreach (var ace in NtfsInheritance.Explicit(node))
                lines.Add($"icacls {quoted} {(ace.Type == AceType.Deny ? "/deny" : "/grant:r")} {Grant(host, ace)}");
            return lines;
        }

        /// <summary>« "SRV-FIC\dave:(OI)(CI)(M)" » : le compte et ce qu'une ACE explicite lui donne, pour /grant ou /deny.</summary>
        private static string Grant(WindowsHost host, EffectiveAce ace)
        {
            var flags = (ace.Inheritance.HasFlag(AceInheritance.ObjectInherit) ? "(OI)" : "")
                        + (ace.Inheritance.HasFlag(AceInheritance.ContainerInherit) ? "(CI)" : "")
                        + (ace.Inheritance.HasFlag(AceInheritance.InheritOnly) ? "(IO)" : "")
                        + (ace.Inheritance.HasFlag(AceInheritance.NoPropagate) ? "(NP)" : "");
            var rights = Ntfs.Format(ace.Mask);
            return $"\"{Account(host, ace.Trustee)}:{flags}{(rights.StartsWith('(') ? rights : $"({rights})")}\"";
        }

        /// <summary>
        /// Ce que fait la fenêtre Modifier de l'onglet Sécurité pour un compte, en commandes : ses autorisations explicites
        /// remplacées (/grant:r, ou /remove:g s'il n'en a plus), ses refus retirés (/remove:d) puis reposés (/deny).
        /// </summary>
        public static IReadOnlyList<string> TrusteeCommands(WindowsHost host, NtfsNode node, string path, string trustee)
        {
            var quoted = $"\"{path}\"";
            var account = $"\"{Account(host, trustee)}\"";
            var own = NtfsInheritance.Explicit(node).Where(a => string.Equals(a.Trustee, trustee, System.StringComparison.OrdinalIgnoreCase)).ToList();
            var allows = own.Where(a => a.Type == AceType.Allow).ToList();
            var lines = new List<string>();
            if (allows.Count == 0) lines.Add($"icacls {quoted} /remove:g {account}");
            for (var i = 0; i < allows.Count; i++)
                lines.Add($"icacls {quoted} {(i == 0 ? "/grant:r" : "/grant")} {Grant(host, allows[i])}");
            lines.Add($"icacls {quoted} /remove:d {account}");
            foreach (var deny in own.Where(a => a.Type == AceType.Deny))
                lines.Add($"icacls {quoted} /deny {Grant(host, deny)}");
            return lines;
        }
    }
}
