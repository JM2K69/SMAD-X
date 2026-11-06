---
title: Accueil
---

# La suite SMA-X

**Simuler, Modéliser, Auditer – eXpert** : trois applications pour **apprendre la sécurité des systèmes** sans risquer une vraie infrastructure. Chaque simulateur reproduit son sujet fidèlement, explique pourquoi un accès est accordé ou refusé, et sert d'énoncé à des exercices corrigés automatiquement, pour toute une classe.

Le nom de chaque simulateur ajoute une lettre à la racine **SMA** : **D** pour l'annuaire (*Directory*, Active Directory), **R** pour les droits (*Rights*).

[Télécharger](installation.md){ .md-button .md-button--primary }
[Découvrir SMAD-X](smad-x.md){ .md-button }
[Découvrir SMAR-X](smar-x/index.md){ .md-button }

!!! success "Des moteurs vérifiés sur de vrais systèmes"
    Les simulateurs ne sont pas écrits « de mémoire ». Leurs résultats ont été comparés, un à un, à ceux de **vrais Windows Server, Windows 11, noyaux Linux et serveurs Samba**, installés et mesurés pour l'occasion. Ces relevés sont figés dans les tests : si un moteur s'en écarte, les tests échouent et aucune version ne sort. Le détail est plus bas, dans [Testé sur de vrais systèmes](#teste-sur-de-vrais-systemes).

## Les trois outils

<div class="grid cards" markdown>

-   :material-domain: **SMAD-X**

    ---

    Le simulateur d'**Active Directory** : un domaine identique à une vraie installation, audité selon l'ANSSI, le NIST, le CIS et MITRE ATT&CK, ses chemins d'attaque, le modèle en tiers, et des exercices corrigés automatiquement.

    [:octicons-arrow-right-24: SMAD-X](smad-x.md)

-   :material-folder-key: **SMAR-X**

    ---

    Le simulateur des **droits d'accès aux fichiers**, sous Linux et sous Windows, sur le serveur et par un partage (SMB, Samba) : qui peut faire quoi, sur quoi, et pourquoi, étape par étape.

    [:octicons-arrow-right-24: SMAR-X](smar-x/index.md)

-   :material-flask: **SMAD-X Studio**

    ---

    L'outil du **formateur**, commun aux deux simulateurs : il crée les exercices (recommandations, recettes, domaine ou serveurs préparés), écrit le sujet et la fiche formateur, et corrige les copies de toute une classe.

    [:octicons-arrow-right-24: SMAD-X Studio](exercices/studio.md)

</div>

## Comment ils s'articulent

1. Le formateur prépare une situation dans **SMAD-X** (un domaine) ou dans **SMAR-X** (des serveurs), ou part d'une recette de **Studio**.
2. **Studio** en fait un exercice : le sujet à distribuer, la solution et la fiche formateur.
3. L'apprenant fait l'exercice dans **SMAD-X** ou **SMAR-X**, vérifie son travail (hors examen) et rend sa **copie**.
4. **Studio** corrige les copies de la classe : notes, réussite par objectif, exports pour Moodle et comptes rendus PDF.

## Testé sur de vrais systèmes

Chaque moteur a été confronté à la réalité : on reproduit la situation sur un vrai système, on mesure ce qu'il accorde ou refuse, et le simulateur doit donner exactement la même réponse.

| Outil | Système réel | Ce qui est comparé |
|---|---|---|
| SMAD-X | domaines **Windows Server 2019, 2022 et 2025** tout juste promus | le domaine neuf, objet par objet et permission par permission, et l'audit des deux (voir [Un domaine neuf, comme un vrai](audit.md#un-domaine-neuf-comme-un-vrai)) |
| SMAD-X | un domaine Windows Server 2025 durci par **HardenAD** 2.9.9 | le modèle appliqué : OU, groupes, GPO et leurs paramètres, appartenances, délégations |
| SMAR-X | **noyau Linux** 6.19, Debian 13 | 925 accès et 140 créations de fichiers, droits Unix et ACL POSIX |
| SMAR-X | **Samba** 4.22 | 20 connexions, 160 opérations et 72 créations par les partages |
| SMAR-X | **Windows Server 2025**, contrôleur de domaine | 1 512 ouvertures sous le jeton de chaque compte, les propriétaires, l'ordre réel des ACL |
| SMAR-X | Windows Server 2025, depuis un **Windows 11** du domaine | 1 540 ouvertures par les partages et 50 dossiers listés |
| SMAR-X | l'**Explorateur de Windows 11** | l'onglet Sécurité : 96 vues de comptes, 103 entrées de la fenêtre Avancé, 8 séquences de cases cochées |

Le détail de SMAR-X, et ce que ces mesures ont appris, est dans [Fidèle aux vrais systèmes](smar-x/index.md#fidele-aux-vrais-systemes) et [Règles de Windows vérifiées](smar-x/regles-windows.md). Ce qui n'est pas simulé est dit franchement : voir les [limites de SMAR-X](smar-x/limites.md).

## Pour qui ?

- **Étudiants et apprentis administrateurs** : comprendre un Active Directory, le modèle en tiers, les droits Linux et NTFS, les partages, et les erreurs de configuration qui mènent à la compromission.
- **Formateurs** : préparer une situation, la distribuer sous forme d'exercice et laisser Studio corriger les copies.
- **Administrateurs** : documenter un AD existant (script d'import de SMAD-X), en produire un rapport, et repérer ses faiblesses avant un audit.

## En un coup d'œil

| | |
|---|---|
| **Plateformes** | Windows, Linux, macOS (Intel et Apple Silicon) |
| **Installation** | une archive par application, un seul exécutable chacune, tout inclus |
| **Langues** | français et anglais, changement à chaud |
| **Formats** | `.smad-x.json` (domaine), `.smar-x.json` (serveurs), `.smadx-lab.json` et `.smarx-lab.json` (exercices) |
| **Licence** | Apache 2.0, y compris en formation payante |

!!! warning "Des simulateurs, pas des outils d'administration"
    Aucune application de la suite ne se connecte à un annuaire ou à un serveur réel. L'import d'un domaine dans SMAD-X se fait par un script PowerShell que vous lancez vous-même et qui ne fait que lire. Les commandes qu'affiche SMAR-X (`icacls`, `chmod`, `setfacl`, PowerShell) sont là pour être essayées sur un labo, par vous.
