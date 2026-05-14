# SMAD-X — Simulateur Expert Active Directory

<p align="center">
  <img alt="Version" src="https://img.shields.io/badge/version-0.3.0-blue"/>
  <img alt=".NET" src="https://img.shields.io/badge/.NET-10-purple"/>
  <img alt="Avalonia" src="https://img.shields.io/badge/Avalonia-11-blueviolet"/>
  <img alt="Plateforme" src="https://img.shields.io/badge/plateforme-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey"/>
  <img alt="Langue" src="https://img.shields.io/badge/langue-FR%20%7C%20EN-green"/>
  <img alt="Licence" src="https://img.shields.io/badge/Licence-CC%20BY--NC%204.0-lightgrey.svg"/>
</p>

> 🇬🇧 English documentation is available in [README.md](./README.md).

**SMAD-X** (*Simulate, Model and Audit Active Directory eXpert*) est un simulateur expert d'Active Directory construit avec Avalonia UI et .NET 10. Il génère une structure AD fidèle à une installation fraîche de Windows Server et vous permet de la visualiser, documenter et exporter sans aucune infrastructure réelle.

---

## 🎯 Fonctionnalités

### 🏗️ Structure AD par défaut complète et fidèle
- Génération automatique de tous les containers et objets présents dans un domaine AD fraîchement promu :
  - **Builtin** : Administrators, Users, Guests, Server Operators, Account Operators, Backup Operators, …
  - **Users** : Administrator, Guest, krbtgt, DefaultAccount, WDAGUtilityAccount + 16 groupes de domaine (Domain Admins, Schema Admins, Enterprise Admins, Protected Users, Key Admins, Cloneable Domain Controllers, Denied/Allowed RODC Password Replication Group, …)
  - **Computers** : container par défaut pour les postes joints au domaine
  - **Domain Controllers** (OU) : DC01 avec tous les rôles FSMO
  - **System** : Password Settings Container, Policies (Default Domain Policy, Default Domain Controllers Policy)
  - **ForeignSecurityPrincipals**
- Création d'un domaine personnalisé via `Fichier > Nouveau domaine`
- Distinguished Names calculés automatiquement

### 🌐 Vue Graphe de relations interactif
- Visualisation force-directed de toutes les relations entre objets
- Rendu séparé des appartenances **User → Groupe** et de l'imbrication **Groupe → Groupe**
- Filtres par type d'objet (User, Group, Computer, OU, GPO, PSO…) et par tier
- Navigation pan/zoom et sélection de nœuds

### 🔗 Gestion des relations
- **User → Groupe** : onglet dédié pour affecter des utilisateurs à des groupes
- **Groupe → Groupe** : onglet dédié pour gérer l'imbrication de groupes (groupes dans des groupes)
- **GPO** : liaisons des stratégies de groupe aux domaines et OUs, avec badge visuel `🔗 GPO` dans l'arborescence
- **PSO** : application des Password Settings Objects aux utilisateurs et groupes
- Les GPOs sont créées sous `System\Policies` pour correspondre à la structure AD réelle

### 📤 Export
- **JSON natif** (`.smadx.json`) : sauvegarde et rechargement complet de la structure
- **PowerShell** : scripts prêts à déployer pour créer la structure dans un vrai AD
  - Export de la structure AD (OUs, utilisateurs, groupes)
  - Export des GPOs liées
  - Export des PSOs

### 🎨 Modèle de tiering Microsoft
- **Tier 0** : Contrôleurs de domaine, comptes et systèmes critiques
- **Tier 1** : Serveurs d'infrastructure et d'applications
- **Tier 2** : Postes de travail et utilisateurs standard
- Couleurs configurables par tier via l'interface

### 📝 Documentation Markdown intégrée
- Chaque objet dispose d'une description Markdown enrichie
- Mode édition / mode prévisualisation
- Descriptions pré-remplies et localisées pour tous les objets par défaut

### 🌙 Thème Clair / Sombre
- Basculement entre le thème clair et le thème sombre à la volée
- Aucun redémarrage requis

### 🌍 Support multilingue
- Interface entièrement disponible en **Français** et en **English**
- Changement de langue à chaud — sans redémarrage

### ✅ Validation Active Directory
- Validation des noms selon les règles AD (caractères interdits, longueur, unicité)
- Règles de conteneurs respectées (ex : un container ne peut contenir que des CN, pas des OUs)

---

## 🚀 Démarrage rapide

1. **Prérequis**
   - .NET 10 SDK
   - Windows, macOS ou Linux

2. **Compilation**
   ```bash
   dotnet build
   ```

3. **Exécution**
   ```bash
   dotnet run --project SMAD-X/SMAD-X.csproj
   ```

---

## 📖 Utilisation

### Structure par défaut au démarrage

Au lancement, SMAD-X charge automatiquement un domaine `contoso.com` avec une structure AD complète et fidèle à une installation fraîche de Windows Server (tous les containers, comptes et groupes par défaut).

### Créer un nouveau domaine

`Fichier > Nouveau domaine` → saisissez le FQDN (ex : `corp.local`) et choisissez si le tiering doit être activé automatiquement.

### Ajouter des objets

- Sélectionnez un nœud parent dans l'arborescence
- Utilisez les boutons de la barre d'outils : 📁 OU, 👤 Utilisateur, 👥 Groupe, 💻 Ordinateur, 🔑 GMSA…
- L'objet est créé comme enfant du nœud sélectionné, avec son DN calculé automatiquement

### Copier / Coller

| Action | Raccourci |
|---|---|
| Copier un objet (et ses enfants) | `Ctrl+C` |
| Coller dans le conteneur sélectionné | `Ctrl+V` |
| Supprimer | `Suppr` |

### Modifier un objet

1. Sélectionnez l'objet dans l'arborescence
2. Le panneau de droite affiche ses propriétés (Nom, Type, Tier, DN, Description…)
3. Modifiez directement — les changements sont appliqués immédiatement

### Documentation Markdown

1. Sélectionnez un objet
2. Dans le panneau de détails, basculez entre `✏️ Éditer` et `📖 Prévisualiser`
3. Écrivez votre documentation en Markdown — rendu en temps réel

### Vue Graphe

`Affichage > Vue Graphe` : visualisation force-directed de toutes les relations.  
Filtrez par type d'objet ou par tier via les cases à cocher de la barre latérale.  
Activez **Imbrication de groupes** pour afficher séparément les arêtes Groupe → Groupe.

### Relations (GPO, PSO, MemberOf)

`Affichage > Relations` : fenêtre dédiée avec quatre onglets :

| Onglet | Rôle |
|---|---|
| 👤 **User → Groupe** | Affecter des utilisateurs à des groupes |
| 👥 **Groupe → Groupe** | Gérer l'imbrication de groupes |
| 📋 **Liens GPO → OU** | Lier des stratégies de groupe aux OUs/Domaine |
| 🔑 **Sujets PSO** | Affecter des Password Settings Objects |

### Sauvegarde / Chargement

| Action | Menu |
|---|---|
| Enregistrer | `Fichier > Enregistrer…` (`.smadx.json`) |
| Ouvrir | `Fichier > Ouvrir…` |
| Exporter PowerShell (structure) | `Fichier > Exporter PowerShell > Structure AD` |
| Exporter PowerShell (GPOs) | `Fichier > Exporter PowerShell > GPOs` |
| Exporter PowerShell (PSOs) | `Fichier > Exporter PowerShell > PSOs` |

---

## 🎨 Modèle de tiering Microsoft

| Tier | Couleur | Périmètre |
|---|---|---|
| **Tier 0** | 🔴 Rouge | Contrôleurs de domaine, comptes et systèmes critiques |
| **Tier 1** | 🟠 Orange | Serveurs d'infrastructure et d'applications |
| **Tier 2** | 🟢 Vert | Postes de travail et utilisateurs standard |

Les couleurs sont configurables via `Paramètres > Configuration des tiers`.

---

## 🏗️ Architecture

```
SMAD-X/
├── Models/
│   ├── ADObject.cs                  # Modèle de données principal (DN, GPO, PSO, MemberOf…)
│   ├── ADObjectType.cs              # Énumération des types d'objets AD
│   ├── ADTreeNode.cs                # Nœud TreeView (badge GPO, couleur tier)
│   └── TierConfiguration.cs        # Configuration des couleurs de tiering
├── Services/
│   ├── ADDataService.cs             # Structure par défaut, sauvegarde/chargement JSON
│   ├── ADImportPowerShellService.cs # Import depuis scripts PowerShell
│   ├── ADPowerShellExportService.cs # Export vers scripts PowerShell
│   ├── ADValidationService.cs       # Validation des noms et règles de conteneurs
│   ├── LocalizationService.cs       # Support multilingue FR/EN
│   └── ThemeService.cs              # Gestion du thème clair/sombre
├── ViewModels/
│   ├── MainWindowViewModel.cs       # ViewModel principal (MVVM)
│   ├── GraphViewModel.cs            # ViewModel vue graphe
│   ├── RelationsViewModel.cs        # ViewModel relations (User→Groupe, Groupe→Groupe, GPO, PSO)
│   └── TierConfigurationViewModel.cs
├── Views/
│   ├── MainWindow.axaml             # Interface principale avec badge GPO dans l'arbre
│   ├── GraphWindow.axaml            # Vue graphe force-directed
│   ├── RelationsWindow.axaml        # Fenêtre relations (4 onglets)
│   ├── NewDomainDialog.axaml        # Dialogue nouveau domaine
│   ├── TierConfigurationWindow.axaml
│   └── AboutDialog.axaml
├── Graph/
│   ├── GraphBuilder.cs              # Construction du graphe depuis l'arbre AD
│   ├── GraphCanvas.cs               # Rendu Avalonia (zoom/pan/hit-test)
│   ├── GraphNode.cs / GraphEdge.cs  # Modèles du graphe
│   ├── GraphFilter.cs               # Filtres par type/tier/imbrication
│   └── ForceSimulation.cs           # Algorithme force-directed
└── Converters/
	├── BoolToStringConverter.cs
	├── LocalizeConverter.cs
	└── MarkdownConverter.cs
```

---

## 🔧 Technologies

| Composant | Version | Rôle |
|---|---|---|
| **.NET** | 10 | Runtime cross-platform |
| **Avalonia UI** | 11 | Framework UI cross-platform |
| **CommunityToolkit.Mvvm** | latest | Implémentation MVVM |
| **Markdig** | latest | Rendu Markdown |
| **System.Text.Json** | intégré | Sérialisation JSON |

---

## 📝 Cas d'usage

| Profil | Usage |
|---|---|
| **Formateur / Étudiant** | Apprendre et enseigner les concepts AD sans infrastructure |
| **Administrateur** | Documenter et auditer une architecture AD existante |
| **Architecte** | Concevoir et valider une nouvelle structure avant déploiement |
| **Pentester / Red Team** | Visualiser les chemins d'attaque via les relations de groupes et tiers |
| **Intégrateur** | Générer des scripts PowerShell prêts à déployer |

---

## 🎯 Feuille de route

- [x] Structure AD par défaut complète et fidèle
- [x] Vue graphe de relations (force-directed)
- [x] Gestion GPO / PSO / MemberOf
- [x] Export PowerShell (structure, GPOs, PSOs)
- [x] Support multilingue FR/EN
- [x] Descriptions Markdown enrichies
- [x] Import depuis un Active Directory réel (via PowerShell)
- [x] Imbrication de groupes (Groupe → Groupe) dans le graphe et les relations
- [x] Badge GPO visuel dans l'arborescence
- [x] Fenêtre Relations divisée : onglets User → Groupe et Groupe → Groupe
- [x] Thème clair / sombre
- [ ] Drag & Drop pour déplacer les objets
- [ ] Recherche et filtrage dans l'arborescence
- [ ] Support multi-domaines / forêts
- [ ] Export vers diagrammes (Draw.io, Visio)

---

## 📄 Licence

Ce projet est sous licence **Creative Commons Attribution-NonCommercial 4.0 International (CC BY-NC 4.0)**.

[![License: CC BY-NC 4.0](https://img.shields.io/badge/License-CC%20BY--NC%204.0-lightgrey.svg)](https://creativecommons.org/licenses/by-nc/4.0/)

### ✅ Vous êtes libre de
- **Partager** — copier et redistribuer le matériel sur tout support ou format
- **Adapter** — remixer, transformer et créer à partir du matériel

### ⚠️ Sous les conditions suivantes
- **Attribution** — Vous **devez** créditer l'auteur original, fournir un lien vers ce dépôt et indiquer si des modifications ont été effectuées.

  Mention obligatoire :
  ```
  Basé sur SMAD-X — Expert Active Directory Simulator
  Œuvre originale : https://github.com/JM2K69/SMAD-X
  Copyright (c) 2025-2026 SMAD-X Project
  Sous licence CC BY-NC 4.0
  ```

- **Non commercial** — Vous ne pouvez **pas** utiliser ce projet ou tout dérivé à des fins commerciales sans autorisation écrite préalable de l'auteur.

Consultez le fichier [LICENSE](./LICENSE) pour les détails complets.

---

> Ce projet est inspiré de [MockAD-Release](https://github.com/shokkadev/MockAD-Release) par shokkadev.

## 🤝 Contribution

Les contributions sont les bienvenues ! N'hésitez pas à ouvrir des issues ou des pull requests.

## 👨‍💻 Auteur

**JM2K69**
