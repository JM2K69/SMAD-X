# Permissions dangereuses sur le domaine, AdminSDHolder ou les contrôleurs de domaine

## Ce que l'audit a détecté
Un compte ou un groupe non privilégié (ou Everyone, Authenticated Users, Domain Users) détient un droit de contrôle sur la racine du domaine, sur l'objet AdminSDHolder, sur l'OU Domain Controllers ou sur un contrôleur de domaine : contrôle total, écriture générale, modification des permissions ou du propriétaire, réplication, tous les droits étendus, écriture de msDS-KeyCredentialLink ou de msDS-AllowedToActOnBehalfOfOtherIdentity, liaison de GPO, réinitialisation du mot de passe.

## Pourquoi c'est dangereux
Chacun de ces droits donne le domaine en une étape :
- la **réplication** sur le domaine permet d'en extraire les secrets de tous les comptes, krbtgt compris (DCSync) ;
- **AdminSDHolder** sert de modèle : SDProp recopie ses permissions toutes les heures sur tous les comptes et groupes protégés ;
- sur un **contrôleur de domaine**, écrire msDS-KeyCredentialLink (Shadow Credentials) ou msDS-AllowedToActOnBehalfOfOtherIdentity (RBCD) permet de s'authentifier comme lui, ou d'obtenir un ticket d'administrateur vers lui ;
- **lier une GPO** au domaine ou à l'OU Domain Controllers exécute du code sur les DC.

Sur un domaine neuf, seuls des groupes privilégiés détiennent ces droits : Enterprise Admins, Domain Admins, Domain Controllers, Key Admins, Enterprise Key Admins.

## Comment corriger
Retirer l'ACE. Un besoin réel (un outil de synchronisation qui réplique les mots de passe, un groupe de support des DC) se traite avec un compte dédié, classé et protégé comme un administrateur du Tier 0.
