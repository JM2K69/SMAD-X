# Modèles de cloisonnement

SMAD-X classe chaque objet dans un tier (voir [Modéliser](modelisation.md#le-modele-en-tiers)). Il peut aussi appliquer à un domaine un **modèle de cloisonnement publié**, comme le ferait son script de déploiement : les OU de chaque tier, les groupes et les comptes d'administration, les GPO et leurs liens, les délégations.

## Les deux modèles

| Modèle | Ce qu'il crée | Particularités |
|---|---|---|
| [Microsoft Active Directory Tier Model](https://github.com/microsoft/ActiveDirectoryTierModel) (v2.1.0) | 31 OU, 29 groupes, 3 comptes de service, 131 GPO, 87 délégations | une OU par tier et par rôle (serveurs membres, comptes, groupes, PAW, comptes de service), OU de staging et de quarantaine, GPO « Account Restrictions » qui interdisent l'ouverture de session d'un tier sur un autre |
| [HardenAD](https://github.com/LoicVeirman/HardenAD) (2.9.9) | 76 OU, 292 groupes, 10 comptes d'administration, 119 GPO, 58 délégations | branches `_Administration`, `Harden_T0`, `Harden_T12` (Tier 1 et Tier 2), `Harden_TL` (tiers Legacy) et `Provisioning` ; groupes en AGDLP (`G-S-…` vers `L-S-…`) ; GPO `HAD-LoginRestrictions`, `HAD-LocalAdmins` et `HAD-LocalRDU` par tier ; un groupe d'application et un groupe d'exclusion par GPO |

HardenAD ajoute deux tiers, **Tier 1 Legacy** et **Tier 2 Legacy**, pour les systèmes anciens isolés : SMAD-X les ajoute aux tiers du document.

## Appliquer un modèle

- **À la création d'un domaine** : **Fichier > Nouveau domaine...**, liste **Cloisonnement en tiers**. *Aucun* ne classe rien, *Étiquettes Tier 0, 1 et 2* classe les objets intégrés (le fonctionnement des versions précédentes), et chaque modèle crée en plus sa structure. La source et la licence du modèle choisi s'affichent sous la liste.
- **Sur le domaine affiché** : **Configuration > Appliquer un modèle de cloisonnement...** montre ce que le modèle crée, sa source et ce que SMAD-X n'en reproduit pas, puis l'applique.

L'application **ajoute** ce qui manque et ne déplace ni ne modifie aucun objet existant : un domaine importé garde ses OU et ses comptes là où ils sont. Elle peut être rejouée sans créer de doublon, et s'annule d'un seul coup (`Ctrl+Z`). Le domaine est vu une fois SDProp passé : un compte ou un groupe devenu membre d'un groupe protégé (les gestionnaires du Tier 0 de HardenAD sont membres d'Enterprise Admins) reçoit `adminCount` et les droits d'AdminSDHolder.

Le modèle appliqué et sa version s'affichent dans la section **Politique de mot de passe** du domaine.

## La conformité au modèle

Une fois un modèle appliqué, l'audit vérifie que le domaine le respecte :

| Règle | Ce qu'elle signale |
|---|---|
| **TIER-05** | un élément du modèle absent : OU, groupe, compte, GPO, lien de GPO ou délégation |
| **TIER-06** | un compte, un ordinateur ou un groupe rangé hors des OU classées du modèle (dans Users, Computers, une ancienne OU) |
| **TIER-07** | un objet resté dans une OU de transit (Staging, Quarantine, Provisioning) |
| **TIER-08** | un compte ou un groupe privilégié rangé dans une OU d'un tier moins sensible que le Tier 0 |

Les comptes et groupes intégrés à AD (Administrator, Domain Admins…) et les contrôleurs de domaine ne sont pas concernés : aucun modèle ne les déplace. Sans modèle appliqué, ces règles ne signalent rien.

Sur un domaine où l'on vient d'appliquer un modèle, TIER-06 liste donc tout ce qu'il reste à ranger : c'est le travail de migration vers le modèle.

## Ce que SMAD-X ne reproduit pas

- **Les paramètres des GPO** : SMAD-X reprend leur nom, leur rôle et leurs liens. Les GPO de restriction de connexion n'empêchent pas encore, dans les chemins d'attaque, l'étape « Session ouverte ».
- **Le modèle Microsoft** : ses composants optionnels (MSA, gMSA et dMSA, Windows LAPS, silos d'authentification) ne sont pas repris. Le préfixe « *- » des noms de GPO est retiré. Les liens que le modèle crée désactivés restent désactivés.
- **HardenAD** : toutes les tâches de sa séquence sont reprises, comme si elles étaient toutes activées (elles sont désactivées par défaut). Les comptes d'administration gardent les noms de la source (`T0M-AHARDEN`…). La redirection des conteneurs par défaut vers Provisioning n'est pas modélisée. Les PAW partagées par plusieurs tiers (`PawAccess`, `PawT12L`) et leurs groupes de connexion n'ont pas de tier.

## Fidélité

Le modèle HardenAD est comparé, dans les tests de SMAD-X, à un vrai domaine durci par HardenAD et exporté par le script d'import : les 76 OU, les 292 groupes (noms, emplacements, descriptions) et les 119 GPO sont identiques. Les noms des groupes de filtrage des GPO suivent la règle de HardenAD, abréviations comprises (`L-S-T0-GPO_HAD-LocAdmins-T1_APPLY`).

## Sources et licences

SMAD-X reproduit la **structure** de ces modèles (noms, emplacements, liens et droits), pas leur code. Chaque modèle cite sa source dans SMAD-X :

| Modèle | Dépôt | Version | Licence | Copyright |
|---|---|---|---|---|
| Microsoft Active Directory Tier Model | [microsoft/ActiveDirectoryTierModel](https://github.com/microsoft/ActiveDirectoryTierModel) | v2.1.0 | MIT | Copyright (c) Microsoft Corporation |
| HardenAD | [LoicVeirman/HardenAD](https://github.com/LoicVeirman/HardenAD) | 2.9.9 | MIT | Copyright (c) 2021 HardenAD |

Le texte des licences est dans le fichier `THIRD-PARTY-NOTICES.md` du dépôt de SMAD-X.

## Mettre un modèle à jour

Les modèles sont des fichiers de données (`SMAD-X.Core/TierModels/microsoft.json`, `hardenad.json`), produits à partir de la source officielle par l'outil `tools/TierModelGenerator`, pour une version publiée :

```bash
dotnet run --project tools/TierModelGenerator -- microsoft v2.1.0 SMAD-X.Core/TierModels
dotnet run --project tools/TierModelGenerator -- hardenad HardenAD-2.9.9v2 SMAD-X.Core/TierModels
```

L'outil télécharge les fichiers de la source au commit de la version (configuration JSON pour Microsoft, séquence de tâches XML pour HardenAD), traduit les droits de chaque délégation en catégories de SMAD-X et signale ceux qu'il ne reconnaît pas. Quand un modèle évolue : relancer l'outil avec la nouvelle version, relire le diff et ce rapport, puis lancer les tests (cohérence de chaque modèle, absence de chemin d'attaque hors du Tier 0 sur un domaine neuf, fidélité à un vrai domaine).
