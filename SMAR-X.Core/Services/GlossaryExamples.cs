using System.Collections.Generic;
using System.Linq;
using SMARX.Models;
using SMARX.Services.Windows;

namespace SMARX.Services
{
    /// <summary>
    /// Les exemples de l'aide-mémoire, sur les serveurs d'exemple de SMAR-X (SRV-FIC, domaine CORP ; srv-fic) : icacls,
    /// PowerShell sans module, le module NTFSSecurity avec l'équivalent icacls de chaque commande, puis les commandes
    /// Linux. Chaque commande se copie d'un clic dans la fenêtre, et part dans le PDF.
    /// </summary>
    public static partial class Glossary
    {
        private const char Lf = (char)10;

        private static string Lines(params string[] lines) => string.Join(Lf, lines);

        private static IEnumerable<GlossarySection> WindowsExamples(string culture)
        {
            string T(string fr, string en) => Text(culture, fr, en);
            yield return new(T("Exemples : icacls", "Examples: icacls"),
                T("Sur le serveur d'exemple, dans une invite de commandes ou PowerShell, en administrateur. Un nom qui contient un espace se met entre guillemets.",
                  "On the sample server, in a command prompt or PowerShell, as an administrator. A name with a space goes in quotes."),
                [],
                [
                    new(@"icacls D:\Partages\RH",
                        T("Afficher les ACE, dans l'ordre où elles sont stockées ; (I) marque une ACE héritée.",
                          "Show the ACEs, in the order they are stored; (I) marks an inherited ACE.")),
                    new(@"icacls D:\Partages\RH /grant ""CORP\DL_RH_LM:(OI)(CI)M""",
                        T("Donner Modifier au groupe de domaine local, sur le dossier, ses sous-dossiers et ses fichiers. Les droits s'ajoutent à ceux que le groupe a déjà.",
                          "Give Modify to the domain local group, on the folder, its subfolders and its files. The rights are added to those the group already has.")),
                    new(@"icacls D:\Partages\RH /grant:r ""CORP\DL_RH_L:(OI)(CI)RX""",
                        T("Avec :r, les droits donnés remplacent les droits explicites que ce groupe avait, au lieu de s'y ajouter.",
                          "With :r, the rights given replace the explicit rights the group had, instead of being added to them.")),
                    new(@"icacls D:\Partages\Projets /deny ""CORP\DL_Projets_Refuser:(OI)(CI)(WD,AD,WA,WEA)""",
                        T("Refuser l'écriture comme le fait l'onglet Sécurité. Refuser W refuserait aussi S (Synchroniser), donc presque toute ouverture, lecture comprise.",
                          "Deny writing the way the Security tab does. Denying W would also deny S (Synchronize), so almost every open, reading included.")),
                    new(@"icacls D:\Partages\RH /remove:g ""CORP\G_RH""",
                        T("Retirer les autorisations accordées à ce compte (:g) ; /remove:d retire ses refus, /remove les deux.",
                          "Remove the permissions granted to this account (:g); /remove:d removes its denies, /remove both.")),
                    new(@"icacls D:\Partages\RH /inheritance:d",
                        T("Désactiver l'héritage en copiant les ACE héritées ; /inheritance:r les retire, /inheritance:e réactive l'héritage.",
                          "Disable inheritance, copying the inherited ACEs; /inheritance:r removes them, /inheritance:e enables inheritance again.")),
                    new(@"icacls D:\Partages\RH /setowner ""BUILTIN\Administrators"" /T /C",
                        T("Changer le propriétaire du dossier et de tout son contenu (/T), en continuant malgré les erreurs (/C).",
                          "Change the owner of the folder and everything in it (/T), carrying on despite errors (/C).")),
                    new(@"icacls D:\Partages\RH\* /reset /T",
                        T("Remplacer les ACL du contenu par les seules ACE héritées : les ACE explicites disparaissent.",
                          "Replace the ACLs of the content with the inherited ACEs only: the explicit ACEs disappear.")),
                    new(@"icacls D:\Partages /findsid ""CORP\G_Stagiaires"" /T",
                        T("Trouver les fichiers et dossiers dont l'ACL nomme ce compte.", "Find the files and folders whose ACL names this account.")),
                    new(Lines(@"icacls D:\Partages\RH\* /save rh.acl /T", @"icacls D:\Partages\RH\ /restore rh.acl"),
                        T("Sauvegarder les ACL du contenu dans un fichier, puis les restaurer : /restore s'applique au dossier qui contenait ce qui a été sauvegardé.",
                          "Save the ACLs of the content to a file, then restore them: /restore applies to the folder that held what was saved."))
                ]);

            yield return new(T("Exemples : PowerShell", "Examples: PowerShell"),
                T("Sans module à installer : Get-Acl et Set-Acl pour NTFS, le module SmbShare (fourni avec Windows) pour les partages, le module ActiveDirectory (RSAT) pour les groupes du domaine.",
                  "No module to install: Get-Acl and Set-Acl for NTFS, the SmbShare module (shipped with Windows) for shares, the ActiveDirectory module (RSAT) for domain groups."),
                [],
                [
                    new(@"Get-Acl D:\Partages\RH | Format-List Owner, AccessToString",
                        T("Le propriétaire et les ACE, une par ligne. L'ordre affiché n'est pas toujours celui qui est stocké : pour l'ordre réel, icacls.",
                          "The owner and the ACEs, one per line. The order shown is not always the stored one: for the real order, icacls.")),
                    new(Lines(@"$acl = Get-Acl D:\Partages\RH",
                              @"$ace = [System.Security.AccessControl.FileSystemAccessRule]::new('CORP\DL_RH_LM', 'Modify', 'ContainerInherit, ObjectInherit', 'None', 'Allow')",
                              @"$acl.AddAccessRule($ace)",
                              @"Set-Acl -Path D:\Partages\RH -AclObject $acl"),
                        T("Ajouter une ACE, comme /grant \"CORP\\DL_RH_LM:(OI)(CI)M\". ContainerInherit est (CI), ObjectInherit (OI) ; en propagation, InheritOnly est (IO) et NoPropagateInherit (NP).",
                          "Add an ACE, like /grant \"CORP\\DL_RH_LM:(OI)(CI)M\". ContainerInherit is (CI), ObjectInherit (OI); as propagation, InheritOnly is (IO) and NoPropagateInherit (NP).")),
                    new(Lines(@"$acl = Get-Acl D:\Partages\RH",
                              @"$acl.SetAccessRuleProtection($true, $true)",
                              @"Set-Acl -Path D:\Partages\RH -AclObject $acl"),
                        T("Désactiver l'héritage en copiant les ACE héritées, comme /inheritance:d ; ($true, $false) les retire, comme /inheritance:r.",
                          "Disable inheritance, copying the inherited ACEs, like /inheritance:d; ($true, $false) removes them, like /inheritance:r.")),
                    new(@"New-SmbShare -Name RH -Path D:\Partages\RH -FullAccess 'BUILTIN\Administrators' -ChangeAccess 'NT AUTHORITY\Authenticated Users' -FolderEnumerationMode AccessBased",
                        T("Un partage large (Utilisateurs authentifiés : Modifier), que NTFS restreint, avec l'énumération basée sur l'accès.",
                          "A broad share (Authenticated Users: Change), narrowed down by NTFS, with access-based enumeration.")),
                    new(@"Get-SmbShareAccess -Name RH",
                        T("Les autorisations du partage.", "The share permissions.")),
                    new(@"Grant-SmbShareAccess -Name RH -AccountName 'CORP\G_RH' -AccessRight Change -Force",
                        T("Ajouter une autorisation au partage ; Revoke-SmbShareAccess la retire, Block-SmbShareAccess pose un refus.",
                          "Add a share permission; Revoke-SmbShareAccess removes it, Block-SmbShareAccess sets a deny.")),
                    new(@"Add-ADGroupMember -Identity DL_RH_LM -Members G_RH",
                        T("AGDLP : le groupe global entre dans le groupe de domaine local qui porte le droit.",
                          "AGDLP: the global group joins the domain local group that holds the right.")),
                    new(@"whoami /groups",
                        T("Les groupes du jeton de la session ouverte. Un changement de groupe ne compte qu'à la session suivante.",
                          "The groups of the open session's token. A group change only counts from the next session."))
                ]);

            yield return new(T("Module NTFSSecurity : les équivalents d'icacls", "NTFSSecurity module: the icacls equivalents"),
                T("Un module de la PowerShell Gallery (Raimund Andree, version 4.2.6), pour Windows PowerShell 5.1 et PowerShell 7 sous Windows. Ses commandes prennent des noms de droits plutôt que des abréviations, et se scriptent mieux qu'icacls : elles renvoient des objets. Son auteur annonce son archivage, au profit du module WindowsAccessControl.",
                  "A PowerShell Gallery module (Raimund Andree, version 4.2.6), for Windows PowerShell 5.1 and PowerShell 7 on Windows. Its commands take right names rather than abbreviations, and script better than icacls: they return objects. Its author announces it will be archived, in favour of the WindowsAccessControl module."),
                [],
                [
                    new(@"Install-Module -Name NTFSSecurity",
                        T("Installer le module, une fois (en administrateur, ou avec -Scope CurrentUser).", "Install the module, once (as an administrator, or with -Scope CurrentUser).")),
                    new(@"Get-NTFSAccess -Path D:\Partages\RH",
                        T("Lire les ACE ; -ExcludeInherited ne garde que les explicites, -Account celles d'un compte.",
                          "Read the ACEs; -ExcludeInherited keeps the explicit ones only, -Account those of one account."),
                        @"icacls D:\Partages\RH"),
                    new(@"Add-NTFSAccess -Path D:\Partages\RH -Account 'CORP\DL_RH_LM' -AccessRights Modify",
                        T("Sans -AppliesTo, l'ACE vaut pour ThisFolderSubfoldersAndFiles : (OI)(CI).",
                          "Without -AppliesTo, the ACE applies to ThisFolderSubfoldersAndFiles: (OI)(CI)."),
                        @"icacls D:\Partages\RH /grant ""CORP\DL_RH_LM:(OI)(CI)M"""),
                    new(@"Add-NTFSAccess -Path D:\Partages -Account 'NT AUTHORITY\Authenticated Users' -AccessRights ReadAndExecute -AppliesTo ThisFolderOnly",
                        T("Une ACE sur le dossier seul, sans héritage.", "An ACE on the folder only, without inheritance."),
                        @"icacls D:\Partages /grant ""NT AUTHORITY\Authenticated Users:RX"""),
                    new(@"Get-NTFSAccess -Path D:\Partages\RH -Account 'CORP\G_RH' -ExcludeInherited | Remove-NTFSAccess",
                        T("Retirer les ACE explicites d'un compte, autorisations et refus.", "Remove an account's explicit ACEs, permissions and denies."),
                        @"icacls D:\Partages\RH /remove ""CORP\G_RH"""),
                    new(@"Disable-NTFSAccessInheritance -Path D:\Partages\RH",
                        T("Désactiver l'héritage en copiant les ACE héritées ; -RemoveInheritedAccessRules les retire (/inheritance:r).",
                          "Disable inheritance, copying the inherited ACEs; -RemoveInheritedAccessRules removes them (/inheritance:r)."),
                        @"icacls D:\Partages\RH /inheritance:d"),
                    new(@"Enable-NTFSAccessInheritance -Path D:\Partages\RH",
                        T("Réactiver l'héritage en gardant les ACE explicites ; -RemoveExplicitAccessRules les retire.",
                          "Enable inheritance again, keeping the explicit ACEs; -RemoveExplicitAccessRules removes them."),
                        @"icacls D:\Partages\RH /inheritance:e"),
                    new(@"Set-NTFSOwner -Path D:\Partages\RH -Account 'BUILTIN\Administrators'",
                        T("Changer le propriétaire.", "Change the owner."),
                        @"icacls D:\Partages\RH /setowner ""BUILTIN\Administrators"""),
                    new(@"Get-ChildItem2 -Path D:\Partages -Recurse | Get-NTFSAccess -Account 'CORP\G_Stagiaires'",
                        T("Trouver où un compte a des ACE ; Get-ChildItem2 lit aussi les chemins de plus de 260 caractères.",
                          "Find where an account has ACEs; Get-ChildItem2 also reads paths longer than 260 characters."),
                        @"icacls D:\Partages /findsid ""CORP\G_Stagiaires"" /T"),
                    new(Lines(@"Get-ChildItem2 -Path D:\Partages\RH -Recurse | Get-NTFSAccess -ExcludeInherited | Export-Csv rh-acl.csv -NoTypeInformation",
                              @"Import-Csv rh-acl.csv | Add-NTFSAccess"),
                        T("Sauvegarder les ACE explicites dans un fichier CSV, puis les remettre.", "Save the explicit ACEs to a CSV file, then put them back."),
                        Lines(@"icacls D:\Partages\RH\* /save rh.acl /T", @"icacls D:\Partages\RH\ /restore rh.acl")),
                    new(@"Get-NTFSEffectiveAccess -Path D:\Partages\RH -Account 'CORP\bob'",
                        T("L'accès effectif d'un compte, comme l'onglet Accès effectif de la fenêtre Avancé ; icacls n'a pas d'équivalent.",
                          "An account's effective access, like the Effective Access tab of the Advanced window; icacls has no equivalent."))
                ]);

            yield return new(T("NTFSSecurity : -AppliesTo et les drapeaux d'icacls", "NTFSSecurity: -AppliesTo and the icacls flags"),
                T("Où vaut l'ACE posée sur un dossier. Sans -AppliesTo, Add-NTFSAccess prend ThisFolderSubfoldersAndFiles.",
                  "Where the ACE set on a folder applies. Without -AppliesTo, Add-NTFSAccess takes ThisFolderSubfoldersAndFiles."),
                AppliesToRows(culture));

            yield return new(T("NTFSSecurity : -AccessRights et les abréviations d'icacls", "NTFSSecurity: -AccessRights and the icacls abbreviations"),
                T("Le nom à donner à -AccessRights pour chaque abréviation ; plusieurs droits se séparent par des virgules (-AccessRights Delete, DeleteSubdirectoriesAndFiles).",
                  "The name to give -AccessRights for each abbreviation; several rights are separated by commas (-AccessRights Delete, DeleteSubdirectoriesAndFiles)."),
                AccessRightsRows(culture));
        }

        /// <summary>Les valeurs de -AppliesTo, en face des drapeaux d'héritage d'icacls.</summary>
        private static IReadOnlyList<GlossaryRow> AppliesToRows(string culture)
        {
            string T(string fr, string en) => Text(culture, fr, en);
            return
            [
                new("(OI)(CI)", "ThisFolderSubfoldersAndFiles", T("Ce dossier, les sous-dossiers et les fichiers ; la valeur par défaut.", "This folder, subfolders and files; the default.")),
                new(T("(aucun)", "(none)"), "ThisFolderOnly", T("Ce dossier seulement.", "This folder only.")),
                new("(CI)", "ThisFolderAndSubfolders", T("Ce dossier et les sous-dossiers.", "This folder and subfolders.")),
                new("(OI)", "ThisFolderAndFiles", T("Ce dossier et les fichiers.", "This folder and files.")),
                new("(OI)(CI)(IO)", "SubfoldersAndFilesOnly", T("Les sous-dossiers et les fichiers seulement.", "Subfolders and files only.")),
                new("(CI)(IO)", "SubfoldersOnly", T("Les sous-dossiers seulement.", "Subfolders only.")),
                new("(OI)(IO)", "FilesOnly", T("Les fichiers seulement.", "Files only.")),
                new("(NP)", "…OneLevel", T("Ajouté à une valeur (ThisFolderAndSubfoldersOneLevel…) : seuls les enfants directs héritent.",
                                          "Added to a value (ThisFolderAndSubfoldersOneLevel…): only direct children inherit."))
            ];
        }

        /// <summary>Le nom de -AccessRights pour chaque abréviation d'icacls (chaque droit du moteur, voir <see cref="Ntfs"/>), avec son nom dans l'onglet Sécurité.</summary>
        private static IReadOnlyList<GlossaryRow> AccessRightsRows(string culture) =>
            Ntfs.Simple.Select(s => new GlossaryRow(s.Code, AccessRightName(s.Rights), WindowsExplainer.Rights(s.Rights, culture)))
                .Concat(Ntfs.Special.Select(s => new GlossaryRow(s.Code, AccessRightName(s.Right), WindowsExplainer.Rights(s.Right, culture))))
                .ToList();

        /// <summary>Le nom que NTFSSecurity (et .NET) donne à un droit ; les deux noms quand il en a deux.</summary>
        public static string AccessRightName(NtfsRights rights) => rights switch
        {
            NtfsRights.FullControl => "FullControl",
            NtfsRights.Modify => "Modify",
            NtfsRights.ReadAndExecute => "ReadAndExecute",
            NtfsRights.Read => "Read",
            NtfsRights.Write => "Write",
            NtfsRights.ReadData => "ListDirectory, ReadData",
            NtfsRights.WriteData => "CreateFiles, WriteData",
            NtfsRights.AppendData => "CreateDirectories, AppendData",
            NtfsRights.ExecuteFile => "Traverse, ExecuteFile",
            NtfsRights.ReadExtendedAttributes => "ReadExtendedAttributes",
            NtfsRights.WriteExtendedAttributes => "WriteExtendedAttributes",
            NtfsRights.ReadAttributes => "ReadAttributes",
            NtfsRights.WriteAttributes => "WriteAttributes",
            NtfsRights.DeleteSubdirectoriesAndFiles => "DeleteSubdirectoriesAndFiles",
            NtfsRights.Delete => "Delete",
            NtfsRights.ReadPermissions => "ReadPermissions",
            NtfsRights.ChangePermissions => "ChangePermissions",
            NtfsRights.TakeOwnership => "TakeOwnership",
            NtfsRights.Synchronize => "Synchronize",
            _ => ""
        };

        private static IEnumerable<GlossarySection> LinuxExamples(string culture)
        {
            string T(string fr, string en) => Text(culture, fr, en);
            yield return new(T("Exemples : droits et ACL", "Examples: permissions and ACLs"),
                T("Sur le serveur d'exemple, en root (ou avec sudo).", "On the sample server, as root (or with sudo)."),
                [],
                [
                    new("ls -ld /srv/compta",
                        T("Le mode, le propriétaire et le groupe du dossier lui-même ; sans -d, ceux de son contenu.",
                          "The mode, owner and group of the directory itself; without -d, those of its content.")),
                    new("stat -c '%A %a %U:%G %n' /srv/compta/*",
                        T("Le mode en lettres et en octal, le propriétaire et le groupe de chaque fichier.",
                          "The mode in letters and in octal, the owner and the group of each file.")),
                    new("chown alice:compta /srv/compta/budget.ods",
                        T("Changer le propriétaire et le groupe ; seul root change le propriétaire.",
                          "Change the owner and the group; only root changes the owner.")),
                    new("chmod 2770 /srv/compta",
                        T("setgid, rwx au propriétaire et au groupe, rien aux autres : ce qui est créé dans le dossier reçoit le groupe compta.",
                          "setgid, rwx for the owner and the group, nothing for others: what is created in the directory gets the compta group.")),
                    new("chmod u=rw,g=r,o= /srv/compta/budget.ods",
                        T("Le même réglage en lettres : rw-r----- (640).", "The same setting in letters: rw-r----- (640).")),
                    new("chmod -R g+rX /srv/projets",
                        T("Récursif. X donne x aux dossiers (et aux fichiers déjà exécutables) seulement : le groupe lit tout et traverse les dossiers.",
                          "Recursive. X gives x to directories (and to files already executable) only: the group reads everything and traverses the directories.")),
                    new("chmod 1777 /srv/depot",
                        T("sticky : chacun crée des fichiers, seul leur propriétaire (ou celui du dossier, ou root) les supprime, comme dans /tmp.",
                          "sticky: everyone creates files, only their owner (or the directory's owner, or root) deletes them, as in /tmp.")),
                    new("getfacl /srv/partage/doc.txt",
                        T("L'ACL POSIX : les entrées, le masque, et ce qui reste de chaque entrée (#effective).",
                          "The POSIX ACL: the entries, the mask, and what remains of each entry (#effective).")),
                    new("setfacl -m u:bob:rw /srv/partage/doc.txt",
                        T("Ajouter ou modifier l'entrée d'un compte ; le masque est recalculé.", "Add or change an account's entry; the mask is recalculated.")),
                    new("setfacl -m g:projet:rwx /srv/projets",
                        T("L'entrée d'un groupe.", "A group's entry.")),
                    new("setfacl -d -m g:compta:rwX /srv/compta",
                        T("Une entrée de l'ACL par défaut : ce qui sera créé dans le dossier la recevra.",
                          "A default ACL entry: what will be created in the directory receives it.")),
                    new("setfacl -R -m g:projet:rX /srv/projets",
                        T("Récursif, sur le dossier et tout son contenu.", "Recursive, on the directory and everything in it.")),
                    new("setfacl -x u:bob /srv/partage/doc.txt",
                        T("Retirer une entrée ; setfacl -b retire toute l'ACL étendue, setfacl -k l'ACL par défaut.",
                          "Remove an entry; setfacl -b removes the whole extended ACL, setfacl -k the default ACL.")),
                    new(Lines("getfacl -R /srv/compta > /root/compta.acl", "cd / && setfacl --restore=/root/compta.acl"),
                        T("Sauvegarder les ACL puis les restaurer, depuis / : getfacl enregistre les chemins sans le / du début.",
                          "Save the ACLs then restore them, from /: getfacl records the paths without the leading /."))
                ]);

            yield return new(T("Exemples : comptes, umask et Samba", "Examples: accounts, umask and Samba"), null,
                [],
                [
                    new("id bob",
                        T("L'uid, le groupe principal et les groupes de bob.", "bob's uid, primary group and groups.")),
                    new("usermod -aG compta bob",
                        T("Ajouter bob au groupe compta ; -a garde ses autres groupes. Cela ne compte qu'à sa prochaine connexion.",
                          "Add bob to the compta group; -a keeps his other groups. It only counts from his next login.")),
                    new("umask 027",
                        T("Pour la session : les fichiers créés naissent en 640, les dossiers en 750.", "For the session: files are created as 640, directories as 750.")),
                    new("testparm -s",
                        T("Vérifier smb.conf et l'afficher tel que Samba le lit.", "Check smb.conf and show it as Samba reads it.")),
                    new("smbpasswd -a bob",
                        T("Donner un mot de passe Samba au compte Unix bob : sans lui, il ne se connecte pas aux partages.",
                          "Give the Unix account bob a Samba password: without it, he cannot connect to the shares.")),
                    new("smbcontrol all reload-config",
                        T("Faire relire smb.conf aux processus Samba en cours.", "Make the running Samba processes reread smb.conf."))
                ]);
        }
    }
}
