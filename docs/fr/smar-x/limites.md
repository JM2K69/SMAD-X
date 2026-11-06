# Limites

SMAR-X simule les droits d'accès aux fichiers, et ses moteurs sont comparés à de vrais systèmes (voir [Fidèle aux vrais systèmes](index.md#fidele-aux-vrais-systemes)). Ce qui suit, il ne le simule pas, ou seulement en partie. Un exercice ne doit pas reposer dessus.

## Ce qui est mesuré, et sur quoi

- Les comparaisons ont été faites sur un noyau Linux 6.19 (Debian 13, `setfacl` 2.3.2), Samba 4.22, Windows Server 2025 et Windows 11 en anglais. D'autres versions peuvent différer sur un détail.
- Seuls les serveurs d'exemple ont été mesurés. Un serveur construit par le formateur suit les mêmes règles, mais ses cas particuliers n'ont pas été vérifiés un par un.
- L'onglet Sécurité a été relevé sur un Windows 11 en anglais ; ses libellés français sont ceux de Windows en français, sans relevé.
- Les commandes de l'aide-mémoire (`icacls`, PowerShell, NTFSSecurity, `chmod`, `setfacl`, Samba) viennent de la documentation de chaque outil. Contrairement aux moteurs, elles n'ont pas été rejouées sur le labo.

## Ce que SMAR-X représente

- **Un serveur Linux et un serveur Windows** par fichier `.smar-x.json`, chacun avec un seul volume. Pas de réseau entre plusieurs serveurs, pas de pare-feu, pas de DFS.
- **Depuis le réseau**, l'accès passe par un partage SMB (Windows) ou Samba (Linux). Le poste client n'est pas simulé : ni sa version de SMB, ni la signature ou le chiffrement, ni son cache, dont la mesure a dû se protéger (voir [Accès par un partage](partages.md#comment-cest-verifie)).
- **Les opérations** sont celles du testeur : lire, écrire, exécuter, lister, entrer, créer, supprimer, changer les autorisations, le propriétaire ou le groupe. Renommer ou déplacer n'est pas une opération à part.

## Linux

- Les droits sont ceux du noyau : propriétaire, groupe, bits `rwx`, setuid, setgid, sticky, `umask`, ACL POSIX et ACL par défaut, masque, et `root`.
- **Pas simulés** : SELinux et AppArmor, les capacités (`CAP_DAC_OVERRIDE`…) autres que celles de `root`, `sudo`, les attributs `chattr` (`+i`, `+a`), les options de montage (`ro`, `noexec`, `nosuid`), NFS, les annuaires (LDAP, SSSD).
- **Samba** : les paramètres de la fenêtre Partage Samba seulement (voir [Partage Samba](samba.md)). Pas de `admin users`, `guest ok`, `hosts allow`, `inherit permissions`, `inherit acls`, ni d'ACL NT stockées par Samba (`vfs_acl_xattr`).

## Windows

- Les droits sont ceux de NTFS et d'AccessCheck : DACL dans l'ordre, héritage, propriétaire, OWNER RIGHTS, CREATOR OWNER, les 14 droits spéciaux, ce que le dossier parent accorde, le contrôle de compte d'utilisateur, les partages et l'énumération basée sur l'accès (voir [Règles de Windows vérifiées](regles-windows.md)).
- **Un seul domaine**, sans approbation ni forêt, avec des groupes globaux, de domaine local et locaux au serveur. Pas de groupes universels, pas de groupes du domaine prédéfinis (Utilisateurs du domaine, Admins du domaine…).
- **Les identités prédéfinies** sont Tout le monde, Utilisateurs authentifiés, INTERACTIF, RÉSEAU, Utilisateurs, Administrateurs, Système, CRÉATEUR PROPRIÉTAIRE et DROITS DU PROPRIÉTAIRE. Pas d'Anonyme, d'Invité ni de session nulle.
- **Les privilèges** : Prendre possession (administrateur en session élevée) et Contourner la vérification de parcours (tout le monde). Pas de Sauvegarde ni de Restauration, qui passent outre les ACL.
- **Pas simulés** : la SACL et l'audit, le contrôle d'intégrité sur les fichiers (le niveau d'intégrité n'est montré que dans whoami), le contrôle d'accès dynamique (revendications, stratégies d'accès centralisées), l'attribut Lecture seule, EFS, les flux de données alternatifs, les liens et points de jonction, les versions précédentes.
- **whoami** montre les identités que SMAR-X simule, pas `LOCAL`, `CONSOLE LOGON`, `This Organization` ni `Domain Users`. Le texte est celui d'un Windows en anglais.
- **Les groupes d'une session** : Windows les lit à l'ouverture de session, mais le testeur et la matrice calculent toujours avec les groupes du moment, comme après une reconnexion. La fenêtre whoami montre l'écart (voir [whoami](regles-windows.md#whoami-le-jeton-dun-compte)).

## Les recettes de Studio

- Les **règles de gestion** sont fixes : chaque service sur son dossier, la Direction partout, le deuxième service en lecture sur le premier. Elles ne se choisissent pas encore une à une.
- Chaque service a **deux comptes**, aux prénoms tirés d'une liste ; un service a un seul dossier et un seul fichier.
- Les recettes n'écrivent **pas de questions** ; le formateur peut en ajouter dans Studio.
- Certaines recettes n'existent que sur un serveur : `SMX-NOMINATIF` et `SMX-PARTAGE` sous Windows, `SMX-AUDIT` sous Linux.

## Exercices et copies

- Un exercice ne vérifie que ses objectifs : les accès, les règles et les gardes que le formateur a choisis. « Exactement les accès nécessaires » ne porte que sur les comptes, les chemins et les opérations qu'il nomme.
- La **copie scellée** repère une copie retouchée à la main, la copie d'un autre exercice ou celle d'un camarade. Le format est public et SMAR-X tourne sur le poste de l'apprenant : le sceau n'arrête pas un faussaire déterminé qui réécrirait tout le fichier de façon cohérente.
- Le **mode examen** cache les indices, le score et l'explication des objectifs. Ce n'est pas une surveillance : l'apprenant garde le reste de SMAR-X, le testeur compris.
