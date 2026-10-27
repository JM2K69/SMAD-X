---
title: Accueil
---

# SMAD-X

**Simuler, Modéliser et Auditer Active Directory eXpert** : un simulateur d'Active Directory pensé pour **apprendre**. On y construit un domaine fidèle à une vraie installation, on l'audite selon les recommandations de l'**ANSSI**, du **NIST**, du **CIS** et de **MITRE ATT&CK**, on découvre **qui peut prendre le contrôle du domaine**, et on s'entraîne sur des **exercices corrigés automatiquement**.

Aucune infrastructure n'est nécessaire : tout se passe dans l'application, sur Windows, Linux ou macOS.

[Télécharger SMAD-X](installation.md){ .md-button .md-button--primary }
[Prendre en main](prise-en-main.md){ .md-button }

![La fenêtre principale de SMAD-X](assets/screens/main.png)

## Ce que l'on peut faire

<div class="grid cards" markdown>

-   :material-file-tree: **Modéliser un domaine**

    ---

    Une structure identique à un AD fraîchement installé (Builtin, Users, Domain Controllers, System…), puis vos OU, comptes, groupes, GPO, PSO, délégations et sites. Chaque objet a une fiche pédagogique.

    [:octicons-arrow-right-24: Modéliser](modelisation.md)

-   :material-shield-search: **Auditer**

    ---

    31 règles de sécurité, chacune reliée à ses sources : checklist AD de l'ANSSI, NIST SP 800-53 et 800-63B, CIS Controls, MITRE ATT&CK, Microsoft. Un score et un niveau ANSSI estimé.

    [:octicons-arrow-right-24: Audit de sécurité](audit.md)

-   :material-sword-cross: **Trouver les chemins d'attaque**

    ---

    Qui peut prendre le contrôle du domaine, et par quelles étapes : Kerberoasting, délégations, propriétaires, GPO, délégation Kerberos… Le chemin se surligne dans le graphe.

    [:octicons-arrow-right-24: Chemins d'attaque](chemins-d-attaque.md)

-   :material-school: **S'exercer**

    ---

    Des exercices réalistes, en français et en anglais, corrigés par l'audit et les chemins d'attaque, avec indices et compte rendu. Et un mode opératoire pour écrire les vôtres.

    [:octicons-arrow-right-24: Exercices](exercices/index.md)

</div>

## Pour qui ?

- **Étudiants et apprentis administrateurs** : comprendre la structure d'un AD, le modèle en tiers et les erreurs de configuration qui mènent à la compromission, sans risquer un vrai domaine.
- **Formateurs** : préparer une situation, la distribuer sous forme d'exercice et laisser SMAD-X corriger.
- **Administrateurs** : documenter un AD existant (script d'import), en produire un rapport, et repérer ses faiblesses avant un audit.

## En un coup d'œil

| | |
|---|---|
| **Plateformes** | Windows, Linux, macOS (Intel et Apple Silicon) |
| **Installation** | un seul fichier exécutable, tout inclus |
| **Langues** | français et anglais, changement à chaud |
| **Format** | `.smad-x.json` (domaine), `.smadx-lab.json` (exercice) |
| **Exports** | JSON, rapport Markdown / DOCX / PDF, image PNG |
| **Licence** | Apache 2.0, y compris en formation payante |

!!! warning "Un simulateur, pas un outil d'administration"
    SMAD-X ne se connecte jamais à un annuaire réel. L'import se fait par un script PowerShell que vous lancez vous-même et qui ne fait que lire ; SMAD-X ne produit aucun script qui modifie un annuaire.
