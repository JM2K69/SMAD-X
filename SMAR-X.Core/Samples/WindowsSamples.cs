using SMARX.Models;
using static SMARX.Models.AceInheritance;
using static SMARX.Models.NtfsAce;
using static SMARX.Models.NtfsNode;

namespace SMARX.Samples;

/// <summary>
/// Serveur de fichiers Windows d'exemple, membre du domaine CORP, organisé selon AGDLP : les comptes du domaine sont dans
/// des groupes globaux (G_Compta, G_RH…), les groupes globaux dans des groupes de domaine local (DL_Compta_LM, DL_RH_LM…,
/// un par ressource et par droit : L lecture, LM lecture et modification, CT contrôle total, Refuser), et les autorisations
/// NTFS ne nomment que des groupes de domaine local et des identités intégrées.
/// Il montre chaque règle de NTFS : partages à héritage désactivé, refus explicite, autorisation explicite plus forte
/// qu'un refus hérité, dossier commun avec CREATOR OWNER, propriétaire qui garde la main sur les autorisations, fichier
/// lisible dans un dossier fermé (pas de vérification de parcours), dossier d'administration fermé tant que la session
/// n'est pas élevée (UAC). alice et bob sont à la comptabilité, bob aux ressources humaines, dave stagiaire : des comptes
/// du domaine ; carol, administratrice, et Administrator sont des comptes locaux du serveur.
/// Partages : comme le conseille Microsoft, Utilisateurs authentifiés : Modifier (et Contrôle total aux Administrateurs),
/// NTFS fait le tri ; Partages avec l'énumération basée sur l'accès. Un seul partage plus étroit, Projets, en Lecture :
/// le même fichier, deux résultats, le plus restrictif des deux l'emporte.
/// </summary>
public static class WindowsSamples
{
    public const string Domain = "CORP";

    public static WindowsHost FileServer()
    {
        var host = new WindowsHost { Name = "SRV-FIC", Domain = Domain };
        host.Groups.Add(new WindowsGroup { Name = WellKnown.Administrators, Description = "Administrateurs du serveur" });
        host.Groups.Add(new WindowsGroup { Name = WellKnown.Users, Description = "Tous les comptes" });
        // Les groupes globaux : qui est qui
        Global("G_Employes", "Tous les employés", "DL_Projets_LM");
        Global("G_Compta", "Comptabilité", "DL_Compta_LM", "DL_Archives_Refuser");
        Global("G_RH", "Ressources humaines", "DL_RH_LM");
        Global("G_Stagiaires", "Stagiaires", "DL_Projets_Refuser", "DL_Planning_LM");
        // Les groupes de domaine local : qui a quel droit sur quelle ressource
        DomainLocal("DL_Compta_LM", @"D:\Partages\Compta : lecture et modification");
        DomainLocal("DL_Archives_Refuser", @"D:\Partages\Compta\Archives : écriture refusée");
        DomainLocal("DL_RH_LM", @"D:\Partages\RH : lecture et modification");
        DomainLocal("DL_Projets_LM", @"D:\Partages\Projets : lecture et modification");
        DomainLocal("DL_Projets_Refuser", @"D:\Partages\Projets : écriture refusée");
        DomainLocal("DL_Planning_LM", @"D:\Partages\Projets\planning.xlsx : lecture et modification");

        host.Users.Add(new WindowsUser { Name = "Administrator", Groups = [WellKnown.Administrators] });
        host.Users.Add(new WindowsUser { Name = "alice", Domain = true, Groups = ["G_Employes", "G_Compta"] });
        host.Users.Add(new WindowsUser { Name = "bob", Domain = true, Groups = ["G_Employes", "G_Compta", "G_RH"] });
        host.Users.Add(new WindowsUser { Name = "carol", Groups = [WellKnown.Administrators] });
        host.Users.Add(new WindowsUser { Name = "dave", Domain = true, Groups = ["G_Employes", "G_Stagiaires"] });

        const string Admins = WellKnown.Administrators;
        host.Root = Directory("D:", Admins,
                Allow(Admins, "F"), Allow(WellKnown.System, "F"), Allow(WellKnown.Users, "RX"),
                Allow(WellKnown.CreatorOwner, "F", ThisFolderSubfoldersAndFiles | InheritOnly))
            .With(
                Directory("Partages", Admins,
                        Allow(Admins, "F"), Allow(WellKnown.System, "F"),
                        // Tout le monde liste les partages, sans rien voir dedans : ce dossier seulement
                        Allow(WellKnown.AuthenticatedUsers, "RX", None))
                    .Protected()
                    .With(
                        Directory("Compta", Admins, Allow(Admins, "F"), Allow("DL_Compta_LM", "M")).Protected().With(
                            File("budget.xlsx", "alice"),
                            Directory("Archives", Admins, Deny("DL_Archives_Refuser", Ntfs.DenyWrite)).With(
                                File("bilan-2025.xlsx", "alice"))),
                        Directory("RH", Admins, Allow(Admins, "F"), Allow("DL_RH_LM", "M")).Protected().With(
                            File("salaires.xlsx", "bob"),
                            // Lisible par tous, dans un dossier où ils ne peuvent rien lister
                            File("annonce-rh.txt", "bob", Allow(WellKnown.AuthenticatedUsers, "R", None))),
                        Directory("Projets", Admins, Allow(Admins, "F"), Allow("DL_Projets_LM", "M"), Deny("DL_Projets_Refuser", Ntfs.DenyWrite))
                            .Protected().With(
                                File("notes.txt", "alice"),
                                // L'autorisation explicite du planning est lue avant le refus hérité des stagiaires
                                File("planning.xlsx", "alice", Allow("DL_Planning_LM", "M", None))),
                        Directory("Commun", Admins,
                                Allow(Admins, "F"),
                                // Chacun liste et crée ses fichiers ici, et devient propriétaire de ce qu'il crée
                                Allow(WellKnown.Users, "(RD,X,WD,AD,RA,REA,RC,S)", None),
                                Allow(WellKnown.CreatorOwner, "F", ThisFolderSubfoldersAndFiles | InheritOnly))
                            .Protected().With(
                                File("rapport-alice.docx", "alice"))),
                Directory("Apps", Admins).With(
                    File("outil.exe", Admins)),
                Directory("Admin", Admins, Allow(Admins, "F"), Allow(WellKnown.System, "F")).Protected().With(
                    File("mots-de-passe.kdbx", Admins)));

        host.Shares.Add(Broad("Partages", @"D:\Partages", "Dossiers des services", abe: true));
        host.Shares.Add(new SmbShare
        {
            Name = "Projets", Path = @"D:\Partages\Projets", Description = "Projets, en lecture",
            Permissions = [ShareAce.Allow(WellKnown.Everyone, ShareRight.Read)]
        });
        host.Shares.Add(Broad("Compta$", @"D:\Partages\Compta", "Comptabilité, partage caché"));
        host.Shares.Add(Broad("Apps", @"D:\Apps", "Applications"));
        return host;

        void Global(string name, string description, params string[] memberOf) =>
            host.Groups.Add(new WindowsGroup { Name = name, Description = description, Scope = WindowsGroupScope.Global, Groups = [.. memberOf] });

        void DomainLocal(string name, string description) =>
            host.Groups.Add(new WindowsGroup { Name = name, Description = description, Scope = WindowsGroupScope.DomainLocal });
    }

    /// <summary>Un partage large, comme le conseille Microsoft : Utilisateurs authentifiés : Modifier, Contrôle total aux Administrateurs.</summary>
    private static SmbShare Broad(string name, string path, string description, bool abe = false) => new()
    {
        Name = name, Path = path, Description = description, AccessBasedEnumeration = abe,
        Permissions = [ShareAce.Allow(WellKnown.AuthenticatedUsers, ShareRight.Change), ShareAce.Allow(WellKnown.Administrators, ShareRight.FullControl)]
    };
}
