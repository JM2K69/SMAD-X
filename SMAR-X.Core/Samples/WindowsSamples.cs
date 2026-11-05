using SMARX.Models;
using static SMARX.Models.AceInheritance;
using static SMARX.Models.NtfsAce;
using static SMARX.Models.NtfsNode;

namespace SMARX.Samples;

/// <summary>
/// Serveur de fichiers Windows d'exemple, qui montre chaque règle de NTFS : partages à héritage désactivé, refus
/// explicite, autorisation explicite plus forte qu'un refus hérité, dossier commun avec CREATOR OWNER, propriétaire
/// qui garde la main sur les autorisations, fichier lisible dans un dossier fermé (pas de vérification de parcours),
/// dossier d'administration fermé tant que la session n'est pas élevée (UAC). alice et bob sont à la comptabilité,
/// bob aux ressources humaines, carol administratrice, dave stagiaire.
/// </summary>
public static class WindowsSamples
{
    public static WindowsHost FileServer()
    {
        var host = new WindowsHost { Name = "SRV-FIC" };
        foreach (var (name, description) in new[]
                 {
                     (WellKnown.Administrators, "Administrateurs du serveur"), (WellKnown.Users, "Tous les comptes locaux"),
                     ("GL_Compta", "Comptabilité"), ("GL_RH", "Ressources humaines"), ("GL_Stagiaires", "Stagiaires")
                 })
            host.Groups.Add(new WindowsGroup { Name = name, Description = description });
        host.Users.Add(new WindowsUser { Name = "Administrator", Groups = [WellKnown.Administrators] });
        host.Users.Add(new WindowsUser { Name = "alice", Groups = ["GL_Compta"] });
        host.Users.Add(new WindowsUser { Name = "bob", Groups = ["GL_Compta", "GL_RH"] });
        host.Users.Add(new WindowsUser { Name = "carol", Groups = [WellKnown.Administrators] });
        host.Users.Add(new WindowsUser { Name = "dave", Groups = ["GL_Stagiaires"] });

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
                        Directory("Compta", Admins, Allow(Admins, "F"), Allow("GL_Compta", "M")).Protected().With(
                            File("budget.xlsx", "alice"),
                            Directory("Archives", Admins, Deny("GL_Compta", Ntfs.DenyWrite)).With(
                                File("bilan-2025.xlsx", "alice"))),
                        Directory("RH", Admins, Allow(Admins, "F"), Allow("GL_RH", "M")).Protected().With(
                            File("salaires.xlsx", "bob"),
                            // Lisible par tous, dans un dossier où ils ne peuvent rien lister
                            File("annonce-rh.txt", "bob", Allow(WellKnown.AuthenticatedUsers, "R", None))),
                        Directory("Projets", Admins, Allow(Admins, "F"), Allow(WellKnown.AuthenticatedUsers, "M"), Deny("GL_Stagiaires", Ntfs.DenyWrite))
                            .Protected().With(
                                File("notes.txt", "alice"),
                                // L'autorisation explicite de dave est lue avant le refus hérité des stagiaires
                                File("planning.xlsx", "alice", Allow("dave", "M", None))),
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
        return host;
    }
}
