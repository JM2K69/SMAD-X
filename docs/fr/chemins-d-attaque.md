# Chemins d'attaque

L'audit liste des faiblesses une par une. Un attaquant, lui, les **enchaîne**. **Analyse > Chemins d'attaque** (`Ctrl+Shift+K`) répond à la question qui compte : *qui peut prendre le contrôle du domaine, et par quelles étapes ?*

![Fenêtre des chemins d'attaque](assets/screens/attack-paths.png)

*Capture : le domaine d'exemple, avec une faiblesse ajoutée pour la documentation. Le helpdesk est resté propriétaire du groupe Schema Admins.*

## Lire la fenêtre

- **À gauche, les sources** : chaque compte, groupe ou machine qui peut atteindre le contrôle du domaine, avec le nombre d'étapes. **N'importe quel utilisateur du domaine** représente un attaquant qui n'a qu'un compte ordinaire : un chemin depuis cette source est le plus grave, et il apparaît en tête.
- **À droite, les étapes** du chemin le plus court, chacune avec sa technique et son explication.
- **Inclure les comptes de Tier 0** : ces comptes contrôlent le domaine par définition, ils sont donc masqués par défaut. Ce qui reste est ce qu'il faut corriger.
- **Afficher dans l'arbre** sélectionne l'objet de l'étape ; **Afficher dans le graphe** surligne tout le chemin.

![Chemin surligné dans le graphe](assets/screens/graph-attack-path.png)

## Comment le graphe d'attaque est construit

SMAD-X construit un graphe « *X peut prendre le contrôle de Y* », à la manière de BloodHound en beaucoup plus simple, puis cherche le plus court chemin vers le domaine par un parcours en largeur.

| Technique | Arête | Pourquoi |
|---|---|---|
| **Membre de** | compte ou groupe → groupe | le membre hérite des droits du groupe |
| **Groupe privilégié** | groupe → domaine | Domain Admins, Enterprise Admins, Administrators, Schema Admins, Account / Server / Backup / Print Operators, DnsAdmins, Key Admins, Enterprise Key Admins, Domain Controllers |
| **Contrôleur de domaine** | DC → domaine | compromettre un DC, c'est compromettre le domaine (DCSync, NTDS.dit) |
| **Propriétaire** | propriétaire → objet | un propriétaire peut toujours réécrire les permissions |
| **Contrôle total** | mandataire → OU et ses objets | délégation GenericAll |
| **Écriture de tous les attributs** | mandataire → objet et ce qu'il contient | GenericWrite : SPN, msDS-KeyCredentialLink, membres, délégation |
| **Modification des permissions**, **Changement de propriétaire** | mandataire → objet | WriteDacl, WriteOwner : on s'accorde ensuite le contrôle total |
| **Réinitialisation du mot de passe** | mandataire → utilisateurs de l'OU | il suffit de changer le mot de passe ; tous les droits étendus le permettent aussi |
| **Modification des membres** | mandataire → groupes de l'OU | droit d'écrire l'attribut `member` |
| **DCSync** | mandataire → domaine | droits de réplication sur la racine du domaine (ou tous les droits étendus) : les secrets de tous les comptes |
| **Shadow Credentials** | mandataire → utilisateurs et ordinateurs | écrire `msDS-KeyCredentialLink`, y ajouter sa clé et s'authentifier comme le compte |
| **Délégation basée sur la ressource (RBCD)** | mandataire → ordinateurs | écrire `msDS-AllowedToActOnBehalfOfOtherIdentity` : un ticket vers la machine au nom de n'importe qui |
| **Kerberoasting ciblé** | mandataire → utilisateurs | ajouter un SPN au compte, puis casser son mot de passe hors ligne |
| **Lecture du mot de passe LAPS** | mandataire → ordinateurs gérés par LAPS | le mot de passe de l'administrateur local |
| **Lecture du mot de passe gMSA** | mandataire → compte de service géré | le mot de passe du compte, donc ses droits |
| **Administrateur local** | compte ou groupe → ordinateur | il contrôle la machine et ce qui s'y trouve en mémoire |
| **Session ouverte** | ordinateur → compte | un administrateur de la machine y vole les identifiants du compte connecté |
| **GPO liée** | GPO → comptes de l'OU | qui contrôle la GPO exécute du code chez eux |
| **Liaison de GPO**, **GPO liée à l'OU** | mandataire → OU → ses comptes | qui contrôle une OU (ou peut écrire `gPLink`) y lie une GPO, qui atteint même les comptes protégés |
| **Contenu du conteneur** | conteneur → ses comptes et groupes | qui contrôle le conteneur s'accorde un droit hérité par ce qu'il contient |
| **AdminSDHolder** | AdminSDHolder → comptes et groupes protégés | SDProp recopie ses permissions sur eux toutes les heures |
| **Kerberoasting** | tout utilisateur → compte avec SPN | ticket chiffré avec le mot de passe, cassé hors ligne |
| **AS-REP roasting** | tout utilisateur → compte sans préauthentification | réponse chiffrée avec le mot de passe |
| **Mot de passe vide** | tout utilisateur → compte PASSWD_NOTREQD | un mot de passe vide peut être accepté |
| **Délégation non contrainte** | serveur → domaine | un DC forcé à s'authentifier y laisse son TGT |
| **Délégation contrainte vers un DC** | compte → DC | usurpation d'un administrateur auprès d'un service du DC |

Les délégations accordées à **Domain Users**, **Authenticated Users** ou **Everyone** partent de « n'importe quel utilisateur ». Un compte désactivé n'est pas un point d'entrée.

!!! info "Ce que AdminSDHolder empêche"
    Une délégation posée sur une OU ne s'applique pas aux objets protégés (`adminCount = 1`) : SDProp a désactivé leur héritage. Un mandataire qui peut réinitialiser les mots de passe d'une OU ne peut donc pas réinitialiser celui d'un membre de Domain Admins rangé dans cette OU. Mais qui contrôle l'OU peut y lier une GPO, qui atteint aussi les comptes protégés ; et qui contrôle AdminSDHolder contrôle tous les objets protégés. Tous les groupes sensibles ne sont pas protégés pour autant : vérifiez la liste des groupes protégés de votre version de Windows Server. Les contrôleurs de domaine ne le sont pas non plus : une délégation posée sur la racine du domaine ou sur l'OU Domain Controllers les atteint.

Les administrateurs locaux et les sessions ne sont pas dans l'annuaire : un ordinateur joint a Domain Admins pour administrateur local, et les sessions se posent à la main (section **Sécurité** de l'ordinateur, voir [Modéliser](modelisation.md)), pour montrer où un administrateur se connecte.

## S'entraîner

Les [exercices de la série « Chemins d'attaque »](exercices/index.md) proposent quinze situations réalistes, en français et en anglais, à résoudre avec cette fenêtre. Leurs solutions ne sont pas publiées : c'est à vous de trouver le chemin et la bonne correction.
