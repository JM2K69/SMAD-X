using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using SMADX.Models;
using SMARX.Models;
using SMARX.Services.Linux;
using SMARX.Services.Windows;
using static SMARX.Models.NtfsAce;

namespace SMARX.Labs
{
    /// <summary>Un exercice généré : l'exercice, les serveurs corrigés (la solution) et les notes du formateur.</summary>
    public sealed record SmarxRecipeResult(SmarxLab Lab, SmarxDocument Solution, LocalizedText TeacherNotes);

    /// <summary>
    /// Une recette : un scénario que Studio génère pour une entreprise, ses services et sa plateforme. Les scénarios
    /// « à construire » partent de dossiers encore fermés ; les autres partent du serveur correct, abîmé d'une façon
    /// précise, et l'apprenant diagnostique puis corrige.
    /// </summary>
    public sealed record SmarxRecipe(string Id, LocalizedText Title, LocalizedText Situation, LabLevel Level, int Minutes, IReadOnlyList<SmarxPlatform> Platforms)
    {
        public bool Supports(SmarxPlatform platform) => Platforms.Contains(platform);
    }

    /// <summary>
    /// Les recettes de SMAR-X : l'arborescence de l'entreprise, le mauvais groupe, les droits nominatifs, le dossier
    /// ouvert à tous, le partage qui bloque, l'audit d'un serveur Linux. Chaque exercice généré échoue au départ, ses
    /// gardes tiennent, et sa solution (le serveur correct) atteint tous les objectifs : les tests le vérifient pour
    /// chaque recette, chaque plateforme et plusieurs entreprises.
    /// </summary>
    public static class SmarxRecipes
    {
        private static readonly SmarxPlatform[] All3 = [SmarxPlatform.Windows, SmarxPlatform.Linux, SmarxPlatform.Both];

        public static IReadOnlyList<SmarxRecipe> All { get; } =
        [
            new("SMX-ARBO", L("L'arborescence de l'entreprise", "The company's folder tree"),
                L("Ouvrir le dossier de chaque service selon les règles de gestion (AGDLP sous Windows, groupes et ACL sous Linux).",
                  "Open each department's folder following the business rules (AGDLP on Windows, groups and ACLs on Linux)."),
                LabLevel.Intermediate, 45, All3),
            new("SMX-GROUPE", L("Le mauvais groupe", "The wrong group"),
                L("Un compte est dans le groupe d'un autre service : le trouver et le remettre à sa place.",
                  "An account is in another department's group: find it and put it back in place."),
                LabLevel.Beginner, 20, All3),
            new("SMX-NOMINATIF", L("Des droits au nom des comptes", "Permissions granted to accounts"),
                L("Des ACL nomment des comptes et des groupes globaux : passer en AGDLP sans changer qui accède à quoi.",
                  "ACLs name accounts and global groups: move to AGDLP without changing who accesses what."),
                LabLevel.Intermediate, 30, [SmarxPlatform.Windows]),
            new("SMX-OUVERT", L("Le dossier ouvert à tous", "The folder open to everyone"),
                L("Un dossier de service est modifiable par tous : le refermer sans rien retirer à ceux qui en ont besoin.",
                  "A department's folder is writable by everyone: close it without taking anything from those who need it."),
                LabLevel.Beginner, 20, All3),
            new("SMX-PARTAGE", L("Le partage qui bloque", "The share that blocks"),
                L("NTFS accorde, mais par le partage personne ne modifie rien : le plus restrictif des deux l'emporte.",
                  "NTFS grants, but through the share nobody can change anything: the more restrictive of the two wins."),
                LabLevel.Beginner, 20, [SmarxPlatform.Windows]),
            new("SMX-AUDIT", L("L'audit d'un serveur Linux", "A Linux server audit"),
                L("Un dossier en 777, un setuid oublié, un dépôt sans sticky bit, un masque d'ACL qui coupe un droit.",
                  "A 777 folder, a forgotten setuid, a drop folder without sticky bit, an ACL mask that cuts a right."),
                LabLevel.Intermediate, 30, [SmarxPlatform.Linux])
        ];

        public static SmarxRecipe? Find(string id) => All.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));

        /// <summary>Génère l'exercice d'une recette pour une entreprise ; null avec la raison si la recette ne s'y prête pas.</summary>
        public static SmarxRecipeResult Create(SmarxRecipe recipe, SmarxOrganization org, string culture = "fr-FR")
        {
            if (!recipe.Supports(org.Platform))
                throw new ArgumentException(T(culture, $"La recette {recipe.Id} ne se fait pas sur cette plateforme.", $"Recipe {recipe.Id} does not run on this platform."));
            if (org.Services.Count < SmarxOrganization.MinServices)
                throw new ArgumentException(T(culture, $"Il faut au moins {SmarxOrganization.MinServices} services.", $"At least {SmarxOrganization.MinServices} departments are needed."));

            var d = new Draft(org);
            switch (recipe.Id)
            {
                case "SMX-ARBO": Tree(d); break;
                case "SMX-GROUPE": WrongGroup(d); break;
                case "SMX-NOMINATIF": Nominative(d); break;
                case "SMX-OUVERT": Open(d); break;
                case "SMX-PARTAGE": Share(d); break;
                case "SMX-AUDIT": Audit(d); break;
                default: throw new ArgumentException(recipe.Id);
            }
            return d.Build(recipe);
        }

        // ─── L'arborescence de l'entreprise ──────────────────────────────────────

        private static void Tree(Draft d)
        {
            var org = d.Org;
            d.Title = L($"L'arborescence de {org.Company}", $"{org.Company}'s folder tree");
            if (org.HasWindows)
            {
                var start = d.StartWindows!;
                foreach (var g in start.Groups.Where(g => g.Scope == WindowsGroupScope.Global)) g.Groups.Clear();
                foreach (var s in org.Services)
                    start.Find(s.WindowsFolder)!.Aces.RemoveAll(a => a.Trustee.StartsWith("DL_", StringComparison.Ordinal));
                start.Shares.Clear();
                d.Context(
                    $"Sur `{SmarxOrganization.WindowsServer}`, chaque service a son dossier sous `{SmarxOrganization.WindowsShareRoot}`. Les comptes sont dans les groupes globaux `G_<service>` ; les groupes de domaine local `DL_<service>_L` (lecture) et `DL_<service>_LM` (lecture et modification) existent, sans membres. Aucun dossier n'est encore ouvert aux services, et rien n'est partagé.",
                    $"On `{SmarxOrganization.WindowsServer}`, each department has its folder under `{SmarxOrganization.WindowsShareRoot}`. The accounts are in the global groups `G_<department>`; the domain local groups `DL_<department>_L` (read) and `DL_<department>_LM` (read and modify) exist, without members. No folder is open to the departments yet, and nothing is shared.");
                d.Need($"par `\\\\{SmarxOrganization.WindowsServer}\\{SmarxOrganization.WindowsShare}`, chaque compte a exactement les accès de la table, ni plus ni moins ;",
                       $"through `\\\\{SmarxOrganization.WindowsServer}\\{SmarxOrganization.WindowsShare}`, each account has exactly the accesses of the table, no more, no less;");
                d.Need("le partage suit la pratique conseillée par Microsoft : un partage large, dont NTFS fait le tri ;",
                       "the share follows the practice Microsoft recommends: a broad share, which NTFS narrows down;");
                d.Need("les autorisations NTFS suivent AGDLP : seuls les groupes de domaine local reçoivent des droits ;",
                       "the NTFS permissions follow AGDLP: only domain local groups get permissions;");
                d.Objective(6, L("Par le partage, chaque compte a exactement les accès de la table", "Through the share, each account has exactly the accesses of the table"),
                    L("Imbriquez chaque groupe global dans les groupes de domaine local que lui donne la table, puis donnez les droits NTFS aux groupes de domaine local.",
                      "Nest each global group in the domain local groups the table gives it, then give the NTFS permissions to the domain local groups."),
                    WindowsExact(org, org.Users, org.Services, SmarxOrganization.WindowsShare), partial: true);
                d.Objective(2, L("Le partage Partages suit la pratique du serveur", "The Partages share follows the server's practice"),
                    L("Un partage large, dont NTFS fait le tri : Utilisateurs authentifiés en Modifier, Contrôle total aux seuls Administrateurs.",
                      "A broad share, which NTFS narrows down: Authenticated Users with Change, Full Control for Administrators only."),
                    new SmarxCheck { Type = SmarxCheckType.Hygiene, System = SmarxSystem.Windows, Rule = SmarxHygieneRule.ShareStandard, Share = SmarxOrganization.WindowsShare });
                d.Guard(L("Les autorisations NTFS suivent AGDLP", "The NTFS permissions follow AGDLP"),
                    new SmarxCheck { Type = SmarxCheckType.Hygiene, System = SmarxSystem.Windows, Rule = SmarxHygieneRule.DomainLocalOnly, Path = SmarxOrganization.WindowsShareRoot });
                d.Guard(L("Les comptes ne changent pas", "The accounts do not change"),
                    new SmarxCheck { Type = SmarxCheckType.AccountUnchanged, System = SmarxSystem.Windows, Account = "*" });
            }
            if (org.HasLinux)
            {
                var start = d.StartLinux!;
                foreach (var s in org.Services)
                {
                    var folder = start.Find(s.LinuxFolder)!;
                    (folder.Group, folder.Mode, folder.Acl, folder.DefaultAcl) = ("root", "0755", null, null);
                    var file = start.Find(s.LinuxFile)!;
                    (file.Group, file.Mode, file.Acl) = (file.Owner, "0644", null);
                }
                var first = org.Services[0];
                d.Context(
                    $"Sur `{SmarxOrganization.LinuxServer}`, chaque service a son dossier sous `/srv`, encore à `root` et lisible par tous. Chaque compte est dans le groupe de son service.",
                    $"On `{SmarxOrganization.LinuxServer}`, each department has its folder under `/srv`, still owned by `root` and readable by everyone. Each account is in its department's group.");
                d.Need("sur le serveur Linux, chaque compte a exactement les accès de la table, ni plus ni moins ;",
                       "on the Linux server, each account has exactly the accesses of the table, no more, no less;");
                d.Need($"ce qu'un membre de {first.Display} crée dans son dossier, ses collègues peuvent le modifier ;",
                       $"what a member of {first.Display} creates in their folder, their colleagues can modify;");
                d.Objective(6, L("Sur le serveur Linux, chaque compte a exactement les accès de la table", "On the Linux server, each account has exactly the accesses of the table"),
                    L("Le groupe du service sur son dossier, setgid ; une entrée d'ACL pour chaque autre service qui y a droit.",
                      "The department's group on its folder, setgid; an ACL entry for each other department that has a right to it."),
                    LinuxExact(org, org.Users, org.Services), partial: true);
                d.Objective(2, L($"Ce que crée {first.Users[0]} dans {first.LinuxFolder}, {first.Users[1]} peut le modifier", $"What {first.Users[0]} creates in {first.LinuxFolder}, {first.Users[1]} can modify"),
                    L("Le bit setgid donne le groupe du dossier ; l'ACL par défaut, les droits de ce qui sera créé.",
                      "The setgid bit gives the folder's group; the default ACL, the permissions of what will be created."),
                    new SmarxCheck { Type = SmarxCheckType.CreatedAllows, System = SmarxSystem.Linux, Account = first.Users[0], Path = first.LinuxFolder, Other = first.Users[1], Operation = "Write" });
                d.Guard(L("Rien sous /srv n'est modifiable par les autres", "Nothing under /srv is writable by others"),
                    new SmarxCheck { Type = SmarxCheckType.Hygiene, System = SmarxSystem.Linux, Rule = SmarxHygieneRule.NoWorldWritable, Path = "/srv" });
                d.Guard(L("Les comptes Linux ne changent pas", "The Linux accounts do not change"),
                    new SmarxCheck { Type = SmarxCheckType.AccountUnchanged, System = SmarxSystem.Linux, Account = "*" });
            }
            d.Need("les comptes ne changent pas.", "the accounts do not change.");
        }

        // ─── Le mauvais groupe ───────────────────────────────────────────────────

        private static void WrongGroup(Draft d)
        {
            var org = d.Org;
            // Un service A et un service B qui n'a pas le droit de modifier le dossier de A
            var (a, b) = org.Services.SelectMany(x => org.Services.Where(y => y != x && org.Right(y, x) != true).Select(y => (x, y))).First();
            var who = a.Users[1];
            d.Title = L($"{who} n'est pas dans le bon groupe", $"{who} is not in the right group");
            d.Context(
                $"{who} vient d'arriver au service {a.Display} de {org.Company}. Pourtant, ce compte n'accède pas aux fichiers de son service, et voit ceux du service {b.Display}.",
                $"{who} has just joined the {a.Display} department of {org.Company}. Yet this account cannot reach the department's files, and sees those of the {b.Display} department.");
            var accounts = new[] { who, a.Users[0], b.Users[0] };
            if (org.HasWindows)
            {
                d.StartWindows!.User(who)!.Groups = [b.Global];
                d.Need($"par `\\\\{SmarxOrganization.WindowsServer}\\{SmarxOrganization.WindowsShare}`, {who} a les accès des membres de {a.Display}, et plus ceux de {b.Display} ;",
                       $"through `\\\\{SmarxOrganization.WindowsServer}\\{SmarxOrganization.WindowsShare}`, {who} has the accesses of the {a.Display} members, and no longer those of {b.Display};");
                d.Objective(3, L($"{who} modifie {a.FileName} par le partage", $"{who} modifies {a.FileName} through the share"),
                    L("Ouvrez « whoami de ce compte… » : ses groupes disent d'où viennent ses droits.", "Open “whoami of this account…”: its groups tell where its rights come from."),
                    new SmarxCheck { Type = SmarxCheckType.AccessAllowed, System = SmarxSystem.Windows, Account = who, Operation = "Write", Path = a.WindowsFile, Share = SmarxOrganization.WindowsShare });
                d.Objective(3, L($"Par le partage, exactement les accès nécessaires sur {a.Display} et {b.Display}", $"Through the share, exactly the accesses needed on {a.Display} and {b.Display}"),
                    L("Un compte entre dans un groupe global ; ce sont les groupes globaux qui sont dans les groupes de domaine local.", "An account joins a global group; global groups are the ones in domain local groups."),
                    WindowsExact(org, accounts, [a, b], SmarxOrganization.WindowsShare));
                d.Guard(L($"Les autorisations NTFS de {a.WindowsFolder} ne changent pas", $"The NTFS permissions of {a.WindowsFolder} do not change"),
                    new SmarxCheck { Type = SmarxCheckType.PathUnchanged, System = SmarxSystem.Windows, Path = a.WindowsFolder });
                d.Guard(L($"Les autorisations NTFS de {b.WindowsFolder} ne changent pas", $"The NTFS permissions of {b.WindowsFolder} do not change"),
                    new SmarxCheck { Type = SmarxCheckType.PathUnchanged, System = SmarxSystem.Windows, Path = b.WindowsFolder });
                foreach (var other in accounts.Skip(1))
                    d.Guard(L($"Le compte {other} ne change pas", $"The account {other} does not change"),
                        new SmarxCheck { Type = SmarxCheckType.AccountUnchanged, System = SmarxSystem.Windows, Account = other });
            }
            if (org.HasLinux)
            {
                d.StartLinux!.Users.Single(u => u.Name == who).Groups = [b.LinuxName];
                d.Need($"sur le serveur Linux, {who} a les accès des membres de {a.Display}, et plus ceux de {b.Display} ;",
                       $"on the Linux server, {who} has the accesses of the {a.Display} members, and no longer those of {b.Display};");
                d.Objective(3, L($"{who} modifie {a.LinuxFile}", $"{who} modifies {a.LinuxFile}"),
                    L("La ligne id d'un compte, dans la carte Comptes, donne ses groupes.", "An account's id line, in the Accounts card, gives its groups."),
                    new SmarxCheck { Type = SmarxCheckType.AccessAllowed, System = SmarxSystem.Linux, Account = who, Operation = "Write", Path = a.LinuxFile });
                d.Objective(3, L($"Sur le serveur Linux, exactement les accès nécessaires sur {a.Display} et {b.Display}", $"On the Linux server, exactly the accesses needed on {a.Display} and {b.Display}"),
                    L("Les droits viennent du groupe du dossier : c'est le compte qui doit changer de groupe.", "The rights come from the folder's group: the account is what must change groups."),
                    LinuxExact(org, accounts, [a, b]));
                d.Guard(L($"Les droits de {a.LinuxFolder} ne changent pas", $"The permissions of {a.LinuxFolder} do not change"),
                    new SmarxCheck { Type = SmarxCheckType.PathUnchanged, System = SmarxSystem.Linux, Path = a.LinuxFolder });
                d.Guard(L($"Les droits de {b.LinuxFolder} ne changent pas", $"The permissions of {b.LinuxFolder} do not change"),
                    new SmarxCheck { Type = SmarxCheckType.PathUnchanged, System = SmarxSystem.Linux, Path = b.LinuxFolder });
                foreach (var other in accounts.Skip(1))
                    d.Guard(L($"Le compte Linux {other} ne change pas", $"The Linux account {other} does not change"),
                        new SmarxCheck { Type = SmarxCheckType.AccountUnchanged, System = SmarxSystem.Linux, Account = other });
            }
            d.Need("les autorisations des dossiers et les autres comptes ne changent pas.", "the folders' permissions and the other accounts do not change.");
        }

        // ─── Des droits au nom des comptes ───────────────────────────────────────

        private static void Nominative(Draft d)
        {
            var org = d.Org;
            var (a, b) = (org.Services[0], org.Services[1]);
            d.Title = L("Des droits au nom des comptes", "Permissions granted to accounts");
            var start = d.StartWindows!;
            // Le dossier de A nomme ses comptes ; celui de B, des groupes globaux ; les DL_ de A et B n'ont plus de membres
            foreach (var (service, byAccount) in new[] { (a, true), (b, false) })
            {
                var folder = start.Find(service.WindowsFolder)!;
                folder.Aces.RemoveAll(x => x.Trustee.StartsWith("DL_", StringComparison.Ordinal));
                foreach (var rule in org.On(service))
                {
                    var rights = rule.Modify ? "M" : "RX";
                    if (byAccount && rule.Who == service)
                        folder.Aces.AddRange(service.Users.Select(u => Allow(u, rights)));
                    else
                        folder.Aces.Add(Allow(rule.Who.Global, rights));
                }
                foreach (var g in start.Groups) g.Groups.RemoveAll(p => p == service.ModifyGroup || p == service.ReadGroup);
            }
            d.Context(
                $"Sur `{SmarxOrganization.WindowsServer}`, les dossiers {a.Display} et {b.Display} de {org.Company} ont été ouverts à la main : leurs ACL nomment des comptes et des groupes globaux. Leurs groupes de domaine local existent, sans membres.",
                $"On `{SmarxOrganization.WindowsServer}`, {org.Company}'s {a.Display} and {b.Display} folders were opened by hand: their ACLs name accounts and global groups. Their domain local groups exist, without members.");
            d.Need($"les autorisations NTFS de `{a.WindowsFolder}` et `{b.WindowsFolder}` suivent AGDLP ;",
                   $"the NTFS permissions of `{a.WindowsFolder}` and `{b.WindowsFolder}` follow AGDLP;");
            d.Need($"par `\\\\{SmarxOrganization.WindowsServer}\\{SmarxOrganization.WindowsShare}`, chacun garde exactement ses accès ;",
                   $"through `\\\\{SmarxOrganization.WindowsServer}\\{SmarxOrganization.WindowsShare}`, everyone keeps exactly their accesses;");
            d.Need("les comptes ne changent pas.", "the accounts do not change.");
            foreach (var s in new[] { a, b })
                d.Objective(2, L($"Les autorisations NTFS de {s.WindowsFolder} suivent AGDLP", $"The NTFS permissions of {s.WindowsFolder} follow AGDLP"),
                    L("Ni compte ni groupe global dans l'ACL : les droits vont aux groupes de domaine local, et les groupes globaux en deviennent membres.",
                      "Neither account nor global group in the ACL: the rights go to domain local groups, and the global groups become their members."),
                    new SmarxCheck { Type = SmarxCheckType.Hygiene, System = SmarxSystem.Windows, Rule = SmarxHygieneRule.DomainLocalOnly, Path = s.WindowsFolder });
            d.Guard(L("Chacun garde exactement ses accès", "Everyone keeps exactly their accesses"),
                WindowsExact(org, org.Users, [a, b], SmarxOrganization.WindowsShare));
            d.Guard(L("Les comptes ne changent pas", "The accounts do not change"),
                new SmarxCheck { Type = SmarxCheckType.AccountUnchanged, System = SmarxSystem.Windows, Account = "*" });
        }

        // ─── Le dossier ouvert à tous ────────────────────────────────────────────

        private static void Open(Draft d)
        {
            var org = d.Org;
            // Un dossier que quelqu'un n'a pas le droit de modifier : l'ouvrir à tous change alors quelque chose
            var a = org.Services.First(s => org.Services.Any(o => org.Right(o, s) != true));
            d.Title = L($"Le dossier {a.Display} ouvert à tous", $"The {a.Display} folder open to everyone");
            if (org.HasWindows)
            {
                var start = d.StartWindows!;
                start.Find(a.WindowsFolder)!.Aces.Add(Allow(WellKnown.AuthenticatedUsers, "M"));
                start.Share(SmarxOrganization.WindowsShare)!.Permissions = [ShareAce.Allow(WellKnown.Everyone, ShareRight.FullControl)];
                d.Context(
                    $"Pour dépanner, quelqu'un a ouvert `{a.WindowsFolder}` à tous les comptes, et le partage `{SmarxOrganization.WindowsShare}` à Tout le monde.",
                    $"To help out, someone opened `{a.WindowsFolder}` to every account, and the `{SmarxOrganization.WindowsShare}` share to Everyone.");
                d.Need($"par `\\\\{SmarxOrganization.WindowsServer}\\{SmarxOrganization.WindowsShare}`, seuls ceux qui en ont besoin accèdent à `{a.WindowsFolder}`, avec les droits de la table ;",
                       $"through `\\\\{SmarxOrganization.WindowsServer}\\{SmarxOrganization.WindowsShare}`, only those who need it reach `{a.WindowsFolder}`, with the rights of the table;");
                d.Need("aucune autorisation NTFS ne donne Modification à Tout le monde, aux Utilisateurs authentifiés ou aux Utilisateurs ;",
                       "no NTFS permission gives Modify to Everyone, Authenticated Users or Users;");
                d.Need("le partage suit la pratique du serveur : un partage large, Contrôle total aux seuls Administrateurs ;",
                       "the share follows the server's practice: a broad share, Full Control for Administrators only;");
                d.Objective(3, L($"Par le partage, exactement les accès nécessaires sur {a.Display}", $"Through the share, exactly the accesses needed on {a.Display}"),
                    L("Cherchez qui d'autre que les groupes de domaine local reçoit un droit sur le dossier.", "Look for who else than the domain local groups gets a right on the folder."),
                    WindowsExact(org, org.Users, [a], SmarxOrganization.WindowsShare));
                d.Objective(2, L("Rien n'est modifiable par tous sous D:\\Partages", "Nothing is writable by everyone under D:\\Partages"),
                    L("Modification ou Contrôle total pour un groupe aussi large ouvre le dossier à tous les comptes.", "Modify or Full Control for such a broad group opens the folder to every account."),
                    new SmarxCheck { Type = SmarxCheckType.Hygiene, System = SmarxSystem.Windows, Rule = SmarxHygieneRule.NoBroadWrite, Path = SmarxOrganization.WindowsShareRoot });
                d.Objective(2, L("Le partage Partages suit la pratique du serveur", "The Partages share follows the server's practice"),
                    L("Tout le monde compte aussi les invités : au plus Lecture. Contrôle total aux seuls Administrateurs.", "Everyone also counts guests: Read at most. Full Control for Administrators only."),
                    new SmarxCheck { Type = SmarxCheckType.Hygiene, System = SmarxSystem.Windows, Rule = SmarxHygieneRule.ShareStandard, Share = SmarxOrganization.WindowsShare });
                d.Guard(L("Les autorisations NTFS suivent AGDLP", "The NTFS permissions follow AGDLP"),
                    new SmarxCheck { Type = SmarxCheckType.Hygiene, System = SmarxSystem.Windows, Rule = SmarxHygieneRule.DomainLocalOnly, Path = SmarxOrganization.WindowsShareRoot });
                d.Guard(L("Les comptes ne changent pas", "The accounts do not change"),
                    new SmarxCheck { Type = SmarxCheckType.AccountUnchanged, System = SmarxSystem.Windows, Account = "*" });
            }
            if (org.HasLinux)
            {
                var start = d.StartLinux!;
                start.Find(a.LinuxFolder)!.Mode = "0777";
                start.Find(a.LinuxFile)!.Mode = "0666";
                d.Context($"Sur `{SmarxOrganization.LinuxServer}`, `{a.LinuxFolder}` et son fichier ont été passés en `777` et `666` pour dépanner.",
                          $"On `{SmarxOrganization.LinuxServer}`, `{a.LinuxFolder}` and its file were set to `777` and `666` to help out.");
                d.Need($"sur le serveur Linux, seuls ceux qui en ont besoin accèdent à `{a.LinuxFolder}`, avec les droits de la table ;",
                       $"on the Linux server, only those who need it reach `{a.LinuxFolder}`, with the rights of the table;");
                d.Need("rien sous `/srv` n'est modifiable par les autres ;", "nothing under `/srv` is writable by others;");
                d.Objective(3, L($"Sur le serveur Linux, exactement les accès nécessaires sur {a.Display}", $"On the Linux server, exactly the accesses needed on {a.Display}"),
                    L("Les autres (o) ne doivent rien avoir ; le groupe du service, si.", "Others (o) must get nothing; the department's group does."),
                    LinuxExact(org, org.Users, [a]));
                d.Objective(2, L("Rien sous /srv n'est modifiable par les autres", "Nothing under /srv is writable by others"),
                    L("Le dernier chiffre du mode, ce sont les autres.", "The last digit of the mode is the others."),
                    new SmarxCheck { Type = SmarxCheckType.Hygiene, System = SmarxSystem.Linux, Rule = SmarxHygieneRule.NoWorldWritable, Path = "/srv" });
                d.Guard(L("Les comptes Linux ne changent pas", "The Linux accounts do not change"),
                    new SmarxCheck { Type = SmarxCheckType.AccountUnchanged, System = SmarxSystem.Linux, Account = "*" });
            }
            d.Need("les comptes ne changent pas.", "the accounts do not change.");
        }

        // ─── Le partage qui bloque ───────────────────────────────────────────────

        private static void Share(Draft d)
        {
            var org = d.Org;
            var a = org.Services[0];
            d.Title = L("Le partage qui bloque", "The share that blocks");
            d.StartWindows!.Share(SmarxOrganization.WindowsShare)!.Permissions = [ShareAce.Allow(WellKnown.Everyone, ShareRight.Read)];
            d.Context(
                $"Les dossiers des services de {org.Company} ont les bonnes autorisations NTFS, en AGDLP. Pourtant, par `\\\\{SmarxOrganization.WindowsServer}\\{SmarxOrganization.WindowsShare}`, personne ne peut rien modifier.",
                $"{org.Company}'s department folders have the right NTFS permissions, following AGDLP. Yet, through `\\\\{SmarxOrganization.WindowsServer}\\{SmarxOrganization.WindowsShare}`, nobody can change anything.");
            d.Need($"{a.Users[0]} modifie `{a.FileName}` par `\\\\{SmarxOrganization.WindowsServer}\\{SmarxOrganization.WindowsShare}` ;",
                   $"{a.Users[0]} modifies `{a.FileName}` through `\\\\{SmarxOrganization.WindowsServer}\\{SmarxOrganization.WindowsShare}`;");
            d.Need("par ce partage, chaque compte a exactement les accès de la table ;", "through this share, each account has exactly the accesses of the table;");
            d.Need("le partage suit la pratique du serveur ; les comptes ne changent pas.", "the share follows the server's practice; the accounts do not change.");
            d.Objective(3, L($"{a.Users[0]} modifie {a.FileName} par le partage", $"{a.Users[0]} modifies {a.FileName} through the share"),
                L("Par le réseau, il faut que le partage ET NTFS accordent : comparez avec le même accès « Sur le serveur ».",
                  "Over the network, both the share AND NTFS must grant: compare with the same access “On the server”."),
                new SmarxCheck { Type = SmarxCheckType.AccessAllowed, System = SmarxSystem.Windows, Account = a.Users[0], Operation = "Write", Path = a.WindowsFile, Share = SmarxOrganization.WindowsShare });
            d.Objective(3, L("Par le partage, chaque compte a exactement les accès de la table", "Through the share, each account has exactly the accesses of the table"),
                L("NTFS fait déjà le tri : le partage peut être large.", "NTFS already narrows things down: the share can be broad."),
                WindowsExact(org, org.Users, org.Services, SmarxOrganization.WindowsShare));
            d.Guard(L("Le partage Partages suit la pratique du serveur", "The Partages share follows the server's practice"),
                new SmarxCheck { Type = SmarxCheckType.Hygiene, System = SmarxSystem.Windows, Rule = SmarxHygieneRule.ShareStandard, Share = SmarxOrganization.WindowsShare });
            d.Guard(L("Les autorisations NTFS suivent AGDLP", "The NTFS permissions follow AGDLP"),
                new SmarxCheck { Type = SmarxCheckType.Hygiene, System = SmarxSystem.Windows, Rule = SmarxHygieneRule.DomainLocalOnly, Path = SmarxOrganization.WindowsShareRoot });
            d.Guard(L("Les comptes ne changent pas", "The accounts do not change"),
                new SmarxCheck { Type = SmarxCheckType.AccountUnchanged, System = SmarxSystem.Windows, Account = "*" });
        }

        // ─── L'audit d'un serveur Linux ──────────────────────────────────────────

        private const string DropFolder = "/srv/depot";
        private const string Tool = "/usr/local/bin/sauvegarde";

        private static void Audit(Draft d)
        {
            var org = d.Org;
            var (first, second) = (org.Services[0], org.Services[1]);
            d.Title = L($"L'audit de {SmarxOrganization.LinuxServer}", $"The {SmarxOrganization.LinuxServer} audit");
            var dropped = first.Users[0] + ".txt";
            foreach (var (host, fixedModes) in new[] { (d.StartLinux!, false), (d.SolutionLinux!, true) })
            {
                host.Find("/srv")!.Children.Add(LinuxNode.Directory("depot", "root", "root", fixedModes ? "1777" : "0777",
                    LinuxNode.File(dropped, first.Users[0], first.Users[0], "0644")));
                host.Root.Children.Add(LinuxNode.Directory("usr", "root", "root", "0755",
                    LinuxNode.Directory("local", "root", "root", "0755",
                        LinuxNode.Directory("bin", "root", "root", "0755",
                            LinuxNode.File("sauvegarde", "root", "root", fixedModes ? "0755" : "4755")))));
            }
            var start = d.StartLinux!;
            start.Find(second.LinuxFolder)!.Mode = "0777";
            start.Find(second.LinuxFile)!.Mode = "0666";
            start.Find(first.LinuxFolder)!.Mode = "2750";

            d.Context(
                $"Un audit de `{SmarxOrganization.LinuxServer}`, chez {org.Company}, a relevé quatre problèmes : le dossier {second.Display} est modifiable par tous ; `{Tool}` s'exécute avec les droits de `root` ; dans `{DropFolder}`, chacun peut supprimer les fichiers des autres ; les membres de {first.Display} ne peuvent plus rien créer dans leur dossier.",
                $"An audit of `{SmarxOrganization.LinuxServer}`, at {org.Company}, found four problems: the {second.Display} folder is writable by everyone; `{Tool}` runs with `root`'s rights; in `{DropFolder}`, anyone can delete the others' files; the {first.Display} members can no longer create anything in their folder.");
            d.Need("rien sous `/srv` n'est modifiable par les autres, sauf un dépôt où chacun ne supprime que ses fichiers ;",
                   "nothing under `/srv` is writable by others, except a drop folder where each one deletes only their own files;");
            d.Need("aucun programme ne s'exécute avec les droits de son propriétaire ou de son groupe ;",
                   "no program runs with its owner's or its group's rights;");
            d.Need($"les membres de {first.Display} créent des fichiers dans leur dossier ; ceux de {second.Display} ont exactement les accès de la table ;",
                   $"the {first.Display} members create files in their folder; the {second.Display} ones have exactly the accesses of the table;");
            d.Need("les comptes ne changent pas.", "the accounts do not change.");
            d.Objective(2, L("Rien sous /srv n'est modifiable par les autres", "Nothing under /srv is writable by others"),
                L("Un dossier où tous écrivent doit avoir le sticky bit, comme /tmp.", "A folder where everyone writes must have the sticky bit, like /tmp."),
                new SmarxCheck { Type = SmarxCheckType.Hygiene, System = SmarxSystem.Linux, Rule = SmarxHygieneRule.NoWorldWritable, Path = "/srv" });
            d.Objective(2, L("Aucun programme setuid ou setgid sous /usr", "No setuid or setgid program under /usr"),
                L("Le s à la place du x du propriétaire, dans ls -l.", "The s instead of the owner's x, in ls -l."),
                new SmarxCheck { Type = SmarxCheckType.Hygiene, System = SmarxSystem.Linux, Rule = SmarxHygieneRule.NoSetuid, Path = "/usr" });
            d.Objective(2, L($"{second.Users[0]} ne supprime pas le fichier déposé par {first.Users[0]} dans {DropFolder}", $"{second.Users[0]} cannot delete {first.Users[0]}'s file in {DropFolder}"),
                L("Supprimer dépend du dossier, pas du fichier.", "Deleting depends on the folder, not on the file."),
                new SmarxCheck { Type = SmarxCheckType.AccessDenied, System = SmarxSystem.Linux, Account = second.Users[0], Operation = "Delete", Path = DropFolder + "/" + dropped });
            d.Objective(2, L($"{first.Users[0]} crée des fichiers dans {first.LinuxFolder}", $"{first.Users[0]} creates files in {first.LinuxFolder}"),
                L("Avec une ACL, les chiffres du groupe dans le mode sont le masque.", "With an ACL, the group digits of the mode are the mask."),
                new SmarxCheck { Type = SmarxCheckType.AccessAllowed, System = SmarxSystem.Linux, Account = first.Users[0], Operation = "Create", Path = first.LinuxFolder });
            d.Objective(2, L($"Exactement les accès nécessaires sur {second.Display}", $"Exactly the accesses needed on {second.Display}"),
                L("Le groupe du service, setgid ; rien pour les autres.", "The department's group, setgid; nothing for others."),
                LinuxExact(org, org.Users, [second]));
            // Fermer le dépôt n'est pas la solution : chacun doit pouvoir encore y déposer
            d.Guard(L($"Chacun dépose encore des fichiers dans {DropFolder}", $"Everyone can still drop files in {DropFolder}"),
                new SmarxCheck { Type = SmarxCheckType.AccessAllowed, System = SmarxSystem.Linux, Account = second.Users[0], Operation = "Create", Path = DropFolder });
            d.Guard(L("Les comptes ne changent pas", "The accounts do not change"),
                new SmarxCheck { Type = SmarxCheckType.AccountUnchanged, System = SmarxSystem.Linux, Account = "*" });
        }

        // ─── Le besoin, case par case ────────────────────────────────────────────

        /// <summary>
        /// « Exactement les accès nécessaires » sous Windows, par un partage : lister le dossier et lire son fichier pour qui
        /// a la lecture ; y créer des fichiers et modifier le fichier pour qui a la modification.
        /// </summary>
        private static SmarxCheck WindowsExact(SmarxOrganization org, IEnumerable<string> accounts, IEnumerable<SmarxService> folders, string share)
        {
            var accountList = accounts.ToList();
            var folderList = folders.ToList();
            var needs = new List<string>();
            foreach (var s in org.Services)
            foreach (var u in s.Users.Where(accountList.Contains))
            foreach (var f in folderList)
                if (org.Right(s, f) is { } modify)
                {
                    needs.Add($"{u} List {f.WindowsFolder}");
                    needs.Add($"{u} Read {f.WindowsFile}");
                    if (modify)
                    {
                        needs.Add($"{u} CreateFile {f.WindowsFolder}");
                        needs.Add($"{u} Write {f.WindowsFile}");
                    }
                }
            return new SmarxCheck
            {
                Type = SmarxCheckType.ExactAccess, System = SmarxSystem.Windows, Share = share, Accounts = accountList,
                Paths = folderList.SelectMany(f => new[] { f.WindowsFolder, f.WindowsFile }).ToList(),
                Operations = ["List", "CreateFile", "Read", "Write"], Needs = needs
            };
        }

        /// <summary>Le même besoin sous Linux, en local : lister et entrer, lire ; créer et modifier pour la modification.</summary>
        private static SmarxCheck LinuxExact(SmarxOrganization org, IEnumerable<string> accounts, IEnumerable<SmarxService> folders)
        {
            var accountList = accounts.ToList();
            var folderList = folders.ToList();
            var needs = new List<string>();
            foreach (var s in org.Services)
            foreach (var u in s.Users.Where(accountList.Contains))
            foreach (var f in folderList)
                if (org.Right(s, f) is { } modify)
                {
                    needs.Add($"{u} List {f.LinuxFolder}");
                    needs.Add($"{u} Enter {f.LinuxFolder}");
                    needs.Add($"{u} Read {f.LinuxFile}");
                    if (modify)
                    {
                        needs.Add($"{u} Create {f.LinuxFolder}");
                        needs.Add($"{u} Write {f.LinuxFile}");
                    }
                }
            return new SmarxCheck
            {
                Type = SmarxCheckType.ExactAccess, System = SmarxSystem.Linux, Accounts = accountList,
                Paths = folderList.SelectMany(f => new[] { f.LinuxFolder, f.LinuxFile }).ToList(),
                Operations = ["List", "Enter", "Create", "Read", "Write"], Needs = needs
            };
        }

        // ─── L'exercice en construction ──────────────────────────────────────────

        private sealed class Draft
        {
            private readonly List<(string Fr, string En)> _context = [];
            private readonly List<(string Fr, string En)> _needs = [];
            private readonly List<SmarxObjective> _objectives = [];

            public Draft(SmarxOrganization org)
            {
                Org = org;
                if (org.HasWindows)
                {
                    SolutionWindows = org.BuildWindows();
                    StartWindows = org.BuildWindows();
                }
                if (org.HasLinux)
                {
                    SolutionLinux = org.BuildLinux();
                    StartLinux = org.BuildLinux();
                }
            }

            public SmarxOrganization Org { get; }
            public WindowsHost? StartWindows { get; }
            public WindowsHost? SolutionWindows { get; }
            public LinuxHost? StartLinux { get; }
            public LinuxHost? SolutionLinux { get; }
            public LocalizedText Title { get; set; } = new();

            public void Context(string fr, string en) => _context.Add((fr, en));
            public void Need(string fr, string en) => _needs.Add((fr, en));

            public void Objective(int points, LocalizedText title, LocalizedText hint, SmarxCheck check, bool partial = false) =>
                _objectives.Add(new SmarxObjective { Points = points, Title = title, Hint = hint, Check = check, Partial = partial });

            public void Guard(LocalizedText title, SmarxCheck check) =>
                _objectives.Add(new SmarxObjective { Points = 0, Title = title, Check = check, Guard = true });

            public SmarxRecipeResult Build(SmarxRecipe recipe)
            {
                var scored = _objectives.Where(o => !o.Guard).ToList();
                var guards = _objectives.Where(o => o.Guard).ToList();
                for (var i = 0; i < scored.Count; i++) scored[i].Id = $"o{i + 1}";
                for (var i = 0; i < guards.Count; i++) guards[i].Id = $"g{i + 1}";

                var start = new SmarxDocument
                {
                    Windows = StartWindows == null ? [] : [StartWindows],
                    Linux = StartLinux == null ? [] : [StartLinux]
                };
                var solution = new SmarxDocument
                {
                    Windows = SolutionWindows == null ? [] : [SolutionWindows],
                    Linux = SolutionLinux == null ? [] : [SolutionLinux]
                };
                var platform = Org.Platform switch { SmarxPlatform.Windows => "windows", SmarxPlatform.Linux => "linux", _ => "mixte" };
                var lab = new SmarxLab
                {
                    Id = $"{recipe.Id.ToLowerInvariant()}-{Org.Domain.ToLowerInvariant()}-{platform}",
                    Title = Title,
                    Level = recipe.Level,
                    DurationMinutes = recipe.Minutes * (Org.Platform == SmarxPlatform.Both ? 2 : 1),
                    Statement = LocalizedText.Of(Statement("fr-FR"), Statement("en-US")),
                    Start = start,
                    Objectives = scored.Concat(guards).ToList()
                };
                var notes = LocalizedText.Of(SmarxTeacherNotes.Write(lab, solution, "fr-FR"), SmarxTeacherNotes.Write(lab, solution, "en-US"));
                return new SmarxRecipeResult(lab, solution, notes);
            }

            private string Statement(string culture)
            {
                var fr = culture == "fr-FR";
                var s = new StringBuilder();
                s.Append("# ").Append(Title.Resolve(culture)).Append("\n\n");
                s.Append(fr
                    ? $"{Org.Company} (domaine `{Org.DnsDomain}`) a {Org.Services.Count} services : {string.Join(", ", Org.Services.Select(x => x.Display))}. Chaque service a deux comptes."
                    : $"{Org.Company} (domain `{Org.DnsDomain}`) has {Org.Services.Count} departments: {string.Join(", ", Org.Services.Select(x => x.Display))}. Each department has two accounts.").Append("\n\n");
                foreach (var (cf, ce) in _context) s.Append(fr ? cf : ce).Append("\n\n");

                s.Append(fr ? "## Les règles de gestion\n\n" : "## The business rules\n\n");
                s.Append(fr ? "| Service | Comptes | Lecture et modification | Lecture |\n|---|---|---|---|\n"
                            : "| Department | Accounts | Read and modify | Read |\n|---|---|---|---|\n");
                foreach (var service in Org.Services)
                {
                    var modify = Org.Rules.Where(r => r.Who == service && r.Modify).Select(r => r.Folder.Display);
                    var read = Org.Rules.Where(r => r.Who == service && !r.Modify).Select(r => r.Folder.Display);
                    s.Append($"| {service.Display} | {string.Join(", ", service.Users)} | {Join(modify)} | {Join(read)} |\n");
                }
                s.Append('\n');

                s.Append(fr ? "## Le besoin\n\n" : "## The need\n\n");
                foreach (var (nf, ne) in _needs) s.Append("- ").Append(fr ? nf : ne).Append('\n');
                s.Append('\n');
                if (Org.HasWindows)
                    s.Append(fr ? "Sous Windows, les groupes s'imbriquent dans la carte **Comptes**, les droits NTFS se donnent dans l'onglet **Sécurité**, le partage se règle avec **Partage avancé…**.\n"
                                : "On Windows, groups are nested in the **Accounts** card, NTFS permissions are given in the **Security** tab, the share is set with **Advanced Sharing…**.\n");
                if (Org.HasLinux)
                    s.Append(fr ? "Sous Linux, les droits se règlent dans la carte des autorisations (propriétaire, groupe, mode, ACL, ACL par défaut), les groupes d'un compte dans la carte **Comptes**.\n"
                                : "On Linux, permissions are set in the permissions card (owner, group, mode, ACL, default ACL), an account's groups in the **Accounts** card.\n");
                return s.ToString();

                string Join(IEnumerable<string> names) => names.Any() ? string.Join(", ", names) : "—";
            }
        }

        private static LocalizedText L(string fr, string en) => LocalizedText.Of(fr, en);

        private static string T(string culture, string fr, string en) =>
            culture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? en : fr;
    }

    /// <summary>
    /// Les notes du formateur d'un exercice généré : les commandes qui mènent des serveurs de départ à la solution,
    /// calculées en comparant les deux (droits NTFS et partages, groupes et comptes, modes et ACL).
    /// </summary>
    public static class SmarxTeacherNotes
    {
        public static string Write(SmarxLab lab, SmarxDocument solution, string culture)
        {
            var fr = !culture.StartsWith("en", StringComparison.OrdinalIgnoreCase);
            var s = new StringBuilder();
            s.Append(fr ? "# Notes du formateur : " : "# Trainer notes: ").Append(lab.Title.Resolve(culture)).Append("\n\n");
            s.Append(fr ? "Ce fichier ne se distribue pas : il contient une solution.\n\n" : "Do not hand this file out: it holds a solution.\n\n");
            s.Append(fr ? "## Les objectifs\n\n" : "## The objectives\n\n");
            foreach (var o in lab.Objectives)
                s.Append($"- **{o.Id}** ({(o.Guard ? (fr ? "garde" : "guard") : $"{o.Points} pt")}) : {o.Title.Resolve(culture)}\n");
            s.Append('\n').Append(fr ? "## Une solution\n\n" : "## A solution\n\n");
            s.Append(fr ? "Les commandes qui mènent des serveurs de départ aux serveurs corrigés :\n\n"
                        : "The commands that take the starting servers to the fixed ones:\n\n");
            if (lab.Start.Windows.FirstOrDefault() is { } ws && solution.Windows.FirstOrDefault() is { } wf)
                Block(s, $"Windows ({wf.Name})", Windows(ws, wf), "powershell");
            if (lab.Start.Linux.FirstOrDefault() is { } ls && solution.Linux.FirstOrDefault() is { } lf)
                Block(s, $"Linux ({lf.Name})", Linux(ls, lf), "bash");
            return s.ToString();
        }

        private static void Block(StringBuilder s, string title, IReadOnlyList<string> lines, string language)
        {
            s.Append("### ").Append(title).Append("\n\n```").Append(language).Append('\n');
            foreach (var line in lines) s.Append(line).Append('\n');
            s.Append("```\n\n");
        }

        private static IReadOnlyList<string> Windows(WindowsHost start, WindowsHost fixedHost)
        {
            var lines = new List<string>();
            foreach (var g in fixedHost.Groups)
                lines.AddRange(Membership(fixedHost, g.Name, start.Group(g.Name)?.Groups ?? [], g.Groups));
            foreach (var u in fixedHost.Users)
                lines.AddRange(Membership(fixedHost, u.Name, start.User(u.Name)?.Groups ?? [], u.Groups));
            foreach (var (node, path) in Walk(fixedHost.Root, fixedHost.Root.Name + "\\"))
                if (Rights(start.Find(path)) != Rights(node))
                    lines.AddRange(WindowsListing.Commands(fixedHost, node, path));
            foreach (var share in fixedHost.Shares)
                if (Json(start.Share(share.Name)) != Json(share))
                    lines.AddRange(SharePermissions.Commands(fixedHost, share));
            return lines;
        }

        private static IEnumerable<string> Membership(WindowsHost host, string member, List<string> before, List<string> after)
        {
            foreach (var g in after.Where(g => !before.Contains(g, StringComparer.OrdinalIgnoreCase)))
                yield return WindowsGroups.AddMemberCommand(host, member, g);
            foreach (var g in before.Where(g => !after.Contains(g, StringComparer.OrdinalIgnoreCase)))
                yield return host.IsDomain(g) ? $"Remove-ADGroupMember -Identity {g} -Members {member} -Confirm:$false" : $"net localgroup \"{g}\" {member} /delete";
        }

        private static IReadOnlyList<string> Linux(LinuxHost start, LinuxHost fixedHost)
        {
            var lines = new List<string>();
            foreach (var u in fixedHost.Users)
            {
                var before = start.Users.FirstOrDefault(x => x.Name == u.Name)?.Groups ?? [];
                if (!before.OrderBy(x => x).SequenceEqual(u.Groups.OrderBy(x => x)))
                    lines.Add($"usermod -G {string.Join(',', u.Groups)} {u.Name}");
            }
            foreach (var (node, path) in WalkLinux(fixedHost.Root, "/"))
            {
                var before = start.Find(path);
                if (before == null || Json(new { before.Owner, before.Group, before.Mode, before.Acl, before.DefaultAcl })
                    != Json(new { node.Owner, node.Group, node.Mode, node.Acl, node.DefaultAcl }))
                    lines.AddRange(LinuxScripts.Commands(node, path));
            }
            return lines;
        }

        private static string Rights(NtfsNode? node) =>
            node == null ? "" : Json(new { node.Owner, node.InheritanceEnabled, node.Aces });

        private static string Json(object? value) => value == null ? "" : JsonSerializer.Serialize(value, SmarxDocument.SerializerOptions);

        private static IEnumerable<(NtfsNode Node, string Path)> Walk(NtfsNode node, string path)
        {
            yield return (node, path);
            foreach (var child in node.Children)
            foreach (var item in Walk(child, WindowsHost.Combine(path, child.Name)))
                yield return item;
        }

        private static IEnumerable<(LinuxNode Node, string Path)> WalkLinux(LinuxNode node, string path)
        {
            yield return (node, path);
            foreach (var child in node.Children)
            foreach (var item in WalkLinux(child, path == "/" ? "/" + child.Name : path + "/" + child.Name))
                yield return item;
        }
    }
}
