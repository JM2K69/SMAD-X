# Modéliser un domaine

SMAD-X ne se contente pas de dessiner un annuaire : il en modélise les **attributs de sécurité**, ceux-là mêmes qu'un attaquant exploite et que l'audit vérifie.

## Les objets

| Type | Rôle | Remarques |
|---|---|---|
| **Domaine** | racine, porte les délégations, les GPO liées et la politique de mot de passe par défaut | |
| **OU** | unité d'organisation : délégations, GPO, classement en tier | sous le domaine ou une autre OU |
| **Conteneur** | `CN=` comme Users, Computers, System | sous le domaine, une OU ou un conteneur ; ne contient jamais d'OU |
| **Utilisateur**, **Ordinateur**, **gMSA** | comptes | options de compte, SPN, délégation Kerberos |
| **Groupe** | portée (domaine local, global, universel) et type (sécurité, distribution) | imbrication selon la portée |
| **Stratégie (GPO)** | créée dans `CN=Policies,CN=System`, nulle part ailleurs | liée aux OU, au domaine ou aux sites |
| **PSO** | stratégie de mot de passe affinée, créée dans le Password Settings Container | appliquée à des utilisateurs ou des groupes |

Ces règles de placement sont celles du schéma d'AD : SMAD-X refuse une OU dans un conteneur, et les boutons **Stratégie** et **PSO** créent toujours l'objet dans son conteneur de System, quel que soit l'objet sélectionné.

## La politique de mot de passe

Sélectionnez le **domaine** : la section **Politique de mot de passe** du panneau donne la politique par défaut (Default Domain Policy), qui s'applique à tous les comptes sans PSO. Un **PSO** a la même section, avec sa priorité. Comme dans AD, la politique effective d'un compte est le PSO de plus petite priorité appliqué au compte lui-même, sinon à l'un de ses groupes, sinon celle du domaine.

Le domaine porte aussi **ms-DS-MachineAccountQuota** : le nombre d'ordinateurs que tout utilisateur peut joindre au domaine (10 par défaut, à mettre à 0).

Un domaine neuf garde les valeurs d'une installation (7 caractères, quota de 10) : l'audit le signale (règles PWD-08, PWD-09 et ACC-03).

## La section Sécurité

![Section Sécurité d'un compte de service](assets/screens/security-panel.png)

Elle reprend les attributs réels d'Active Directory :

| Champ | Attribut AD | Ce qu'il permet de montrer |
|---|---|---|
| Compte désactivé | `userAccountControl` (ACCOUNTDISABLE) | un compte désactivé n'est pas un point d'entrée |
| Mot de passe non requis | `userAccountControl` (PASSWD_NOTREQD) | un mot de passe vide accepté |
| Le mot de passe n'expire jamais | `userAccountControl` (DONT_EXPIRE_PASSWORD) | point ANSSI pour les comptes privilégiés |
| Pré-authentification Kerberos non requise | `userAccountControl` (DONT_REQ_PREAUTH) | AS-REP roasting |
| Délégation non contrainte | `userAccountControl` (TRUSTED_FOR_DELEGATION) | vol de TGT |
| Délégation contrainte avec transition de protocole | `userAccountControl` (TRUSTED_TO_AUTH_FOR_DELEGATION) | usurpation vers un service |
| Compte sensible, ne peut pas être délégué | `userAccountControl` (NOT_DELEGATED) | protège un administrateur |
| SPN | `servicePrincipalName` | Kerberoasting |
| Mot de passe changé le, dernière connexion | `pwdLastSet`, `lastLogonTimestamp` | comptes dormants, krbtgt ancien |
| Délégation contrainte vers | `msDS-AllowedToDelegateTo` | délégation vers un contrôleur de domaine |
| adminCount | `adminCount` | objet protégé par AdminSDHolder |
| Propriétaire | propriétaire du descripteur de sécurité | un propriétaire peut réécrire les permissions |

Les options utilisent les vrais bits de `userAccountControl` : l'import PowerShell les reprend tels quels.

!!! tip "AdminSDHolder"
    Les membres des groupes protégés (Domain Admins, Account Operators, Backup Operators…) ont `adminCount = 1` : le processus SDProp leur applique les permissions d'AdminSDHolder et **coupe l'héritage**. SMAD-X en tient compte : une délégation posée sur une OU ne s'applique pas à ces objets. C'est ce qui explique pourquoi certains chemins d'attaque n'existent pas.

## Appartenances et relations

**Affichage > Gérer les relations...** (ou le bouton **Relations**) ouvre quatre onglets :

| Onglet | Usage |
|---|---|
| Utilisateur → Groupe | appartenance des comptes |
| Groupe → Groupe | imbrication des groupes |
| GPO → OU | liens de GPO vers les OU et le domaine |
| PSO | comptes et groupes soumis à une PSO |

L'audit et les chemins d'attaque calculent l'**appartenance effective** : un compte membre d'un groupe lui-même membre de Domain Admins est administrateur du domaine.

Les imbrications suivent les portées d'AD (modèle AGDLP) : un groupe **global** ne contient que des comptes et des groupes globaux, un groupe **universel** accepte aussi des universels mais pas de domaine local, un groupe de **domaine local** accepte les trois. Une imbrication impossible est refusée avec son explication. Un gMSA peut être membre d'un groupe, comme un compte.

## Les délégations

![Fenêtre des délégations](assets/screens/delegations.png)

**Analyse > Délégations...** (`Ctrl+D`) liste les délégations : mandataire, droit, catégorie, DN cible, héritage, tier. On filtre par mandataire, cible ou catégorie, on masque les délégations héritées, et on exporte en CSV.

Pour **déléguer**, sélectionnez une OU à gauche, choisissez le mandataire et la catégorie (réinitialisation de mot de passe, gestion des ordinateurs, déverrouillage, écriture d'attribut, contrôle total), puis **Ajouter**. **Retirer la délégation sélectionnée** la supprime.

## Le modèle en tiers

Chaque objet est classé dans un tier, selon le modèle d'administration de Microsoft :

| Tier | Contenu |
|---|---|
| **Tier 0** | contrôleurs de domaine, comptes et groupes qui contrôlent l'annuaire |
| **Tier 1** | serveurs et applications |
| **Tier 2** | postes de travail et utilisateurs |

**Configuration > Gérer les tiers...** change les noms, les niveaux et les couleurs. L'audit vérifie le cloisonnement : un objet ne doit pas être membre d'un groupe plus sensible que lui (TIER-01), ni recevoir une délégation sur un objet plus sensible (TIER-03).
