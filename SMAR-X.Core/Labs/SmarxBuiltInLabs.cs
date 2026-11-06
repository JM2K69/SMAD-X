using System.Collections.Generic;
using System.Linq;
using SMADX.Models;
using SMARX.Models;
using SMARX.Samples;

namespace SMARX.Labs
{
    /// <summary>
    /// Exercices fournis avec SMAR-X, sur les serveurs d'exemple : un dossier d'équipe sous Linux, un partage de
    /// service sous Windows, un diagnostic par un partage Samba. Leur solution n'est pas ici : les tests la vérifient.
    /// </summary>
    public static class SmarxBuiltInLabs
    {
        public static IReadOnlyList<SmarxLab> All() => [LinuxTeamFolder(), WindowsDepartmentShare(), SambaDiagnosis()];

        private static LocalizedText L(string fr, string en) => LocalizedText.Of(fr, en);

        private static SmarxObjective Objective(string id, int points, LocalizedText title, LocalizedText hint, SmarxCheck check, bool guard = false) =>
            new() { Id = id, Points = guard ? 0 : points, Title = title, Hint = hint, Check = check, Guard = guard };

        // ─── Linux : le dossier de l'équipe projet ───────────────────────────────

        public const string TeamFolder = "/srv/equipe";

        public static SmarxLab LinuxTeamFolder()
        {
            var host = LinuxSamples.FileServer();
            host.Find("/srv")!.Children.Add(LinuxNode.Directory("equipe", "root", "root", "0755",
                LinuxNode.File("plan.txt", "alice", "alice", "0644")));
            const string Plan = TeamFolder + "/plan.txt";
            var linux = SmarxSystem.Linux;
            return new SmarxLab
            {
                Id = "linux-dossier-equipe",
                Level = LabLevel.Beginner,
                DurationMinutes = 25,
                Title = L("Le dossier de l'équipe projet", "The project team's folder"),
                Statement = L(
                    """
                    # Le dossier de l'équipe projet

                    L'équipe projet (le groupe **projet** : alice et bob) va travailler dans `/srv/equipe`. Aujourd'hui, ce dossier appartient à root et tout le monde peut le lire ; `plan.txt` appartient à alice.

                    ## Le besoin

                    - alice et bob listent le dossier, y créent des fichiers et modifient les fichiers de l'autre, **y compris ceux qui seront créés plus tard** ;
                    - personne d'autre (carol, dave) n'entre dans le dossier ;
                    - les comptes ne changent pas : ni leurs groupes, ni leur umask.

                    Vérifiez votre travail avec le testeur et la matrice d'accès, puis cliquez sur **Vérifier**.
                    """,
                    """
                    # The project team's folder

                    The project team (the **projet** group: alice and bob) will work in `/srv/equipe`. Today this folder belongs to root and everyone can read it; `plan.txt` belongs to alice.

                    ## The need

                    - alice and bob list the folder, create files in it and modify each other's files, **including the ones that will be created later**;
                    - nobody else (carol, dave) gets into the folder;
                    - the accounts do not change: neither their groups nor their umask.

                    Check your work with the tester and the access matrix, then click **Check**.
                    """),
                Start = new SmarxDocument { Linux = [host] },
                Objectives =
                [
                    Objective("o1", 2, L("bob modifie plan.txt", "bob modifies plan.txt"),
                        L("plan.txt appartient à alice et au groupe alice : quel groupe doit-il avoir, et avec quels droits ?",
                          "plan.txt belongs to alice and to the alice group: which group should it have, and with which permissions?"),
                        new SmarxCheck { Type = SmarxCheckType.AccessAllowed, System = linux, Account = "bob", Operation = "Write", Path = Plan }),
                    Objective("o2", 2, L("alice crée des fichiers dans /srv/equipe", "alice creates files in /srv/equipe"),
                        L("Créer dans un dossier demande w et x sur ce dossier.", "Creating in a folder needs w and x on that folder."),
                        new SmarxCheck { Type = SmarxCheckType.AccessAllowed, System = linux, Account = "alice", Operation = "Create", Path = TeamFolder }),
                    Objective("o3", 3, L("bob modifie les fichiers qu'alice créera", "bob modifies the files alice will create"),
                        L("Deux réglages du dossier : le groupe des nouveaux fichiers, et leurs droits. Les comptes ne changent pas : l'umask non plus.",
                          "Two folder settings: the group of new files, and their permissions. The accounts do not change: neither does the umask."),
                        new SmarxCheck { Type = SmarxCheckType.CreatedAllows, System = linux, Account = "alice", Path = TeamFolder, Other = "bob", Operation = "Write" }),
                    Objective("o4", 2, L("carol et dave n'ont aucun accès", "carol and dave have no access"),
                        L("La matrice d'accès montre tout ce que chaque compte peut faire.", "The access matrix shows everything each account can do."),
                        new SmarxCheck
                        {
                            Type = SmarxCheckType.ExactAccess, System = linux, Accounts = ["carol", "dave"], Paths = [TeamFolder, Plan],
                            Operations = ["List", "Enter", "Create", "Read", "Write"], Needs = []
                        }),
                    Objective("g1", 0, L("Rien n'est modifiable par les autres", "Nothing is writable by others"), new LocalizedText(),
                        new SmarxCheck { Type = SmarxCheckType.Hygiene, System = linux, Rule = SmarxHygieneRule.NoWorldWritable, Path = TeamFolder }, guard: true),
                    Objective("g2", 0, L("Les comptes ne changent pas", "The accounts do not change"), new LocalizedText(),
                        new SmarxCheck { Type = SmarxCheckType.AccountUnchanged, System = linux, Account = "*" }, guard: true)
                ]
            };
        }

        // ─── Windows : le partage du service RH ──────────────────────────────────

        public const string RhFolder = @"D:\Partages\RH";

        public static SmarxLab WindowsDepartmentShare()
        {
            const string Salaries = RhFolder + @"\salaires.xlsx";
            var windows = SmarxSystem.Windows;
            return new SmarxLab
            {
                Id = "windows-partage-rh",
                Level = LabLevel.Intermediate,
                DurationMinutes = 30,
                Title = L("Le partage du service RH", "The HR department share"),
                Statement = L(
                    """
                    # Le partage du service RH

                    Les postes du service RH doivent ouvrir leurs fichiers par un partage dédié : `\\SRV-FIC\RH`, sur le dossier `D:\Partages\RH`. Les comptes du service sont dans le groupe global **G_RH** (dont bob) ; le groupe de domaine local **DL_RH_LM** a été créé pour ce dossier, mais personne ne s'en sert encore.

                    Le serveur suit AGDLP : les comptes dans des groupes globaux, les groupes globaux dans des groupes de domaine local, et les autorisations NTFS aux seuls groupes de domaine local. Les partages suivent la pratique conseillée par Microsoft : un partage large, dont NTFS fait le tri.

                    ## Le besoin

                    - par `\\SRV-FIC\RH`, bob lit et modifie `salaires.xlsx`, liste le dossier et y crée des fichiers ;
                    - par ce partage, alice et dave n'ont accès à rien ;
                    - le partage suit la pratique du serveur ;
                    - les autorisations NTFS de `D:\Partages\RH` suivent AGDLP ;
                    - les comptes ne changent pas.

                    Le partage se crée dans la carte **Partage** du dossier, avec **Partage avancé…** ; les groupes s'imbriquent dans la fenêtre **Comptes et groupes…** de la carte **Comptes**.
                    """,
                    """
                    # The HR department share

                    The HR workstations must open their files through a dedicated share: `\\SRV-FIC\RH`, on the `D:\Partages\RH` folder. The department's accounts are in the global group **G_RH** (including bob); the domain local group **DL_RH_LM** was created for this folder, but nothing uses it yet.

                    The server follows AGDLP: accounts in global groups, global groups in domain local groups, and NTFS permissions to domain local groups only. The shares follow the practice Microsoft recommends: a broad share, which NTFS narrows down.

                    ## The need

                    - through `\\SRV-FIC\RH`, bob reads and modifies `salaires.xlsx`, lists the folder and creates files in it;
                    - through this share, alice and dave get access to nothing;
                    - the share follows the server's practice;
                    - the NTFS permissions of `D:\Partages\RH` follow AGDLP;
                    - the accounts do not change.

                    The share is created in the folder's **Sharing** card, with **Advanced Sharing…**; groups are nested in the **Accounts and groups…** window of the **Accounts** card.
                    """),
                Start = new SmarxDocument { Windows = [DepartmentShareStart()] },
                Objectives =
                [
                    Objective("o1", 3, L(@"bob modifie salaires.xlsx par \\SRV-FIC\RH", @"bob modifies salaires.xlsx through \\SRV-FIC\RH"),
                        L("Par un partage, il faut que les autorisations du partage et celles de NTFS accordent toutes les deux.",
                          "Through a share, both the share permissions and NTFS must grant."),
                        new SmarxCheck { Type = SmarxCheckType.AccessAllowed, System = windows, Account = "bob", Operation = "Write", Path = Salaries, Share = "RH" }),
                    Objective("o2", 3, L("Par le partage RH, exactement les accès nécessaires", "Through the RH share, exactly the accesses needed"),
                        L("Choisissez le partage dans « D'où ? » et regardez la matrice d'accès.", "Pick the share under “From where?” and look at the access matrix."),
                        new SmarxCheck
                        {
                            Type = SmarxCheckType.ExactAccess, System = windows, Share = "RH", Accounts = ["alice", "bob", "dave"],
                            Paths = [RhFolder, Salaries], Operations = ["List", "CreateFile", "Read", "Write"],
                            Needs = [$"bob List {RhFolder}", $"bob CreateFile {RhFolder}", $"bob Read {Salaries}", $"bob Write {Salaries}"]
                        }),
                    Objective("o3", 2, L("Le partage RH suit la pratique du serveur", "The RH share follows the server's practice"),
                        L("Un partage large, dont NTFS fait le tri : Contrôle total aux seuls Administrateurs, et jamais Modifier pour Tout le monde, qui compte aussi les invités.",
                          "A broad share, which NTFS narrows down: Full Control for Administrators only, and never Change for Everyone, which also counts guests."),
                        new SmarxCheck { Type = SmarxCheckType.Hygiene, System = windows, Rule = SmarxHygieneRule.ShareStandard, Share = "RH" }),
                    Objective("o4", 2, L("Les autorisations NTFS de D:\\Partages\\RH suivent AGDLP", "The NTFS permissions of D:\\Partages\\RH follow AGDLP"),
                        L("Ni compte ni groupe global dans l'ACL : un groupe de domaine local reçoit le droit, et le groupe global du service en devient membre.",
                          "Neither account nor global group in the ACL: a domain local group gets the right, and the department's global group becomes its member."),
                        new SmarxCheck { Type = SmarxCheckType.Hygiene, System = windows, Rule = SmarxHygieneRule.DomainLocalOnly, Path = RhFolder }),
                    Objective("g1", 0, L("Les comptes ne changent pas", "The accounts do not change"), new LocalizedText(),
                        new SmarxCheck { Type = SmarxCheckType.AccountUnchanged, System = windows, Account = "*" }, guard: true)
                ]
            };
        }

        /// <summary>
        /// Le serveur d'exemple, avant qu'on s'occupe du service RH : le dossier donne Modifier directement au groupe global
        /// G_RH, et DL_RH_LM n'a encore aucun membre.
        /// </summary>
        private static WindowsHost DepartmentShareStart()
        {
            var host = WindowsSamples.FileServer();
            host.Group("G_RH")!.Groups.Remove("DL_RH_LM");
            host.Find(RhFolder)!.Aces.Single(a => a.Trustee == "DL_RH_LM").Trustee = "G_RH";
            return host;
        }

        // ─── Samba : pourquoi carol n'écrit-elle pas ? ───────────────────────────

        public const string SharedDoc = "/srv/partage/doc.txt";

        public static SmarxLab SambaDiagnosis()
        {
            var linux = SmarxSystem.Linux;
            return new SmarxLab
            {
                Id = "samba-carol-ecrit",
                Level = LabLevel.Intermediate,
                DurationMinutes = 20,
                Title = L("Pourquoi carol n'écrit-elle pas ?", "Why can't carol write?"),
                Statement = L(
                    """
                    # Pourquoi carol n'écrit-elle pas ?

                    carol (groupe **rh**) signale qu'elle ne peut pas modifier `doc.txt` depuis son poste, par `\\srv-fic\partage`.

                    1. Trouvez pourquoi, avec le testeur : choisissez carol, l'opération, `doc.txt`, puis le partage sous **D'où ?**. Répondez aux questions.
                    2. Faites ensuite que carol puisse lire et modifier `doc.txt` par le partage, **sans lui donner d'autre droit**, et sans toucher aux comptes.
                    """,
                    """
                    # Why can't carol write?

                    carol (**rh** group) reports that she cannot modify `doc.txt` from her workstation, through `\\srv-fic\partage`.

                    1. Find out why, with the tester: pick carol, the operation, `doc.txt`, then the share under **From where?**. Answer the questions.
                    2. Then make carol able to read and modify `doc.txt` through the share, **without giving her any other right**, and without touching the accounts.
                    """),
                Start = new SmarxDocument { Linux = [LinuxSamples.FileServer()] },
                Objectives =
                [
                    Objective("o1", 3, L(@"carol modifie doc.txt par \\srv-fic\partage", @"carol modifies doc.txt through \\srv-fic\partage"),
                        L("Les étapes du testeur montrent qui refuse : Samba d'abord, puis le noyau.", "The tester's steps show who denies: Samba first, then the kernel."),
                        new SmarxCheck { Type = SmarxCheckType.AccessAllowed, System = linux, Account = "carol", Operation = "Write", Path = SharedDoc, Share = "partage" }),
                    Objective("o2", 2, L("carol n'a pas d'autre droit par le partage", "carol has no other right through the share"),
                        L("Lire et modifier doc.txt, rien de plus : ni supprimer, ni créer dans le dossier.", "Read and modify doc.txt, nothing more: neither delete nor create in the folder."),
                        new SmarxCheck
                        {
                            Type = SmarxCheckType.ExactAccess, System = linux, Share = "partage", Accounts = ["carol"],
                            Paths = ["/srv/partage", SharedDoc], Operations = ["Create", "Read", "Write", "Delete"],
                            Needs = [$"carol Read {SharedDoc}", $"carol Write {SharedDoc}"]
                        }),
                    Objective("g1", 0, L("Rien n'est modifiable par les autres", "Nothing is writable by others"), new LocalizedText(),
                        new SmarxCheck { Type = SmarxCheckType.Hygiene, System = linux, Rule = SmarxHygieneRule.NoWorldWritable, Path = "/srv/partage" }, guard: true),
                    Objective("g2", 0, L("Les comptes ne changent pas", "The accounts do not change"), new LocalizedText(),
                        new SmarxCheck { Type = SmarxCheckType.AccountUnchanged, System = linux, Account = "*" }, guard: true)
                ],
                Questions =
                [
                    new LabQuestion
                    {
                        Id = "q1", Points = 1, Kind = LabQuestionKind.SingleChoice,
                        Title = L("Par le partage, qui refuse d'abord à carol de modifier doc.txt ?", "Through the share, who first denies carol modifying doc.txt?"),
                        Choices =
                        [
                            new LabChoice { Id = "a", Text = L("Samba", "Samba"), Correct = true },
                            new LabChoice { Id = "b", Text = L("Le noyau Linux", "The Linux kernel") },
                            new LabChoice { Id = "c", Text = L("Personne : seul le poste de carol est en cause", "Nobody: only carol's workstation is at fault") }
                        ]
                    },
                    new LabQuestion
                    {
                        Id = "q2", Points = 2, Kind = LabQuestionKind.SingleChoice,
                        Title = L("Sur le serveur, carol peut écrire dans doc.txt grâce au groupe rh (-w-). Par le partage, pourquoi cela ne suffit-il pas au noyau ?",
                                  "On the server, carol can write to doc.txt thanks to the rh group (-w-). Through the share, why is this not enough for the kernel?"),
                        Choices =
                        [
                            new LabChoice { Id = "a", Text = L("Le client SMB ouvre le fichier en lecture-écriture : une même entrée de l'ACL doit donner r et w",
                                                                "The SMB client opens the file read-write: one ACL entry must grant both r and w"), Correct = true },
                            new LabChoice { Id = "b", Text = L("Samba ne lit pas les ACL POSIX", "Samba does not read POSIX ACLs") },
                            new LabChoice { Id = "c", Text = L("Seul le propriétaire d'un fichier peut le modifier par un partage", "Only a file's owner can modify it through a share") }
                        ]
                    }
                ]
            };
        }
    }
}
