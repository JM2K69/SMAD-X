# SMAD-X — Expert Active Directory Simulator

<p align="center">
  <img alt="Version" src="https://img.shields.io/badge/version-0.2.0-blue"/>
  <img alt=".NET" src="https://img.shields.io/badge/.NET-10-purple"/>
  <img alt="Avalonia" src="https://img.shields.io/badge/Avalonia-11-blueviolet"/>
  <img alt="Platform" src="https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey"/>
  <img alt="Language" src="https://img.shields.io/badge/language-FR%20%7C%20EN-green"/>
  <img alt="License" src="https://img.shields.io/badge/License-CC%20BY--NC%204.0-lightgrey.svg"/>
</p>

> 🇫🇷 La documentation en français est disponible dans [ReadmeFR.md](./ReadmeFR.md).

**SMAD-X** (*Simulate, Model and Audit Active Directory eXpert*) is an expert Active Directory simulator built with Avalonia UI and .NET 10. It generates an AD structure faithful to a fresh Windows Server installation and lets you visualize, document and export it without any real infrastructure.

---

## 🎯 Features

### 🏗️ Complete and Faithful Default AD Structure
- Automatic generation of all containers and objects present in a freshly promoted AD domain:
  - **Builtin**: Administrators, Users, Guests, Server Operators, Account Operators, Backup Operators, …
  - **Users**: Administrator, Guest, krbtgt, DefaultAccount, WDAGUtilityAccount + 16 default domain groups (Domain Admins, Schema Admins, Enterprise Admins, Protected Users, Key Admins, Cloneable Domain Controllers, Denied/Allowed RODC Password Replication Group, …)
  - **Computers**: default container for domain-joined workstations
  - **Domain Controllers** (OU): DC01 with all FSMO roles
  - **System**: Password Settings Container, Policies (Default Domain Policy, Default Domain Controllers Policy)
  - **ForeignSecurityPrincipals**
- Create a custom domain via `File > New Domain`
- Distinguished Names computed automatically

### 🌐 Interactive Relationship Graph
- Force-directed visualization of all relationships between objects
- Separate rendering of **User → Group** memberships and **Group → Group** nesting
- Filters by object type (User, Group, Computer, OU, GPO, PSO…) and by tier
- Pan, zoom and node selection

### 🔗 Relationship Management
- **User → Group**: dedicated tab to assign users to groups
- **Group → Group**: dedicated tab to manage group nesting (groups inside groups)
- **GPO**: Group Policy Object links to domains and OUs, with visual badge `🔗 GPO` in the tree
- **PSO**: Password Settings Object assignment to users and groups
- GPOs are created under `System\Policies` to match the real AD structure

### 📤 Export
- **Native JSON** (`.smadx.json`): full save and reload of the structure
- **PowerShell**: ready-to-deploy scripts to create the structure in a real AD
  - AD structure export (OUs, users, groups)
  - Linked GPOs export
  - PSOs export

### 🎨 Microsoft Tiering Model
- **Tier 0**: Domain controllers, critical accounts and systems
- **Tier 1**: Infrastructure and application servers
- **Tier 2**: Workstations and standard users
- Per-tier colors configurable through the UI

### 📝 Built-in Markdown Documentation
- Every object has a rich Markdown description
- Edit / Preview toggle
- Pre-filled and localized descriptions for all default objects

### 🌙 Light / Dark Theme
- Switch between Light and Dark themes at runtime
- No restart required

### 🌍 Multilingual Support
- Full interface available in **French** and **English**
- Language switch at runtime — no restart required

### ✅ Active Directory Validation
- Name validation following AD rules (forbidden characters, length, uniqueness)
- Container rules enforced (e.g. a Container can only hold CN objects, not OUs)

---

## 🚀 Quick Start

1. **Prerequisites**
   - .NET 10 SDK
   - Windows, macOS or Linux

2. **Build**
   ```bash
   dotnet build
   ```

3. **Run**
   ```bash
   dotnet run --project SMAD-X/SMAD-X.csproj
   ```

---

## 📖 Usage

### Default Structure on Startup

On launch, SMAD-X automatically loads a `contoso.com` domain with a complete AD structure faithful to a fresh Windows Server installation (all default containers, accounts and groups).

### Create a New Domain

`File > New Domain` → enter the FQDN (e.g. `corp.local`) and choose whether tiering should be assigned automatically.

### Add Objects

- Select a parent node in the tree
- Use the toolbar buttons: 📁 OU, 👤 User, 👥 Group, 💻 Computer, 🔑 GMSA…
- The object is created as a child of the selected node, with its DN computed automatically

### Copy / Paste

| Action | Shortcut |
|---|---|
| Copy an object (and its children) | `Ctrl+C` |
| Paste into the selected container | `Ctrl+V` |
| Delete | `Del` |

### Edit an Object

1. Select the object in the tree
2. The right panel shows its properties (Name, Type, Tier, DN, Description…)
3. Edit directly — changes are applied immediately

### Markdown Documentation

1. Select an object
2. In the details panel, toggle between `✏️ Edit` and `📖 Preview`
3. Write your documentation in Markdown — rendered in real time

### Graph View

`View > Graph View`: force-directed visualization of all relationships.  
Filter by object type or tier using the sidebar checkboxes.  
Toggle **Group nesting** to display Group → Group edges separately.

### Relations (GPO, PSO, MemberOf)

`View > Relations`: dedicated window with four tabs:

| Tab | Purpose |
|---|---|
| 👤 **User → Group** | Assign users to groups |
| 👥 **Group → Group** | Manage group nesting |
| 📋 **GPO Links → OU** | Link Group Policy Objects to OUs/Domain |
| 🔑 **PSO Subjects** | Assign Password Settings Objects |

### Save / Load

| Action | Menu |
|---|---|
| Save | `File > Save…` (`.smadx.json`) |
| Open | `File > Open…` |
| Export PowerShell (structure) | `File > Export PowerShell > AD Structure` |
| Export PowerShell (GPOs) | `File > Export PowerShell > GPOs` |
| Export PowerShell (PSOs) | `File > Export PowerShell > PSOs` |

---

## 🎨 Microsoft Tiering Model

| Tier | Color | Scope |
|---|---|---|
| **Tier 0** | 🔴 Red | Domain controllers, critical accounts and systems |
| **Tier 1** | 🟠 Orange | Infrastructure and application servers |
| **Tier 2** | 🟢 Green | Workstations and standard users |

Colors are configurable via `Settings > Tier Configuration`.

---

## 🏗️ Architecture

```
SMAD-X/
├── Models/
│   ├── ADObject.cs                  # Core data model (DN, GPO, PSO, MemberOf…)
│   ├── ADObjectType.cs              # AD object type enumeration
│   ├── ADTreeNode.cs                # TreeView display node (GPO badge, tier color)
│   └── TierConfiguration.cs        # Tier color configuration
├── Services/
│   ├── ADDataService.cs             # Default structure, JSON save/load
│   ├── ADImportPowerShellService.cs # Import from PowerShell scripts
│   ├── ADPowerShellExportService.cs # Export to PowerShell scripts
│   ├── ADValidationService.cs       # Name validation and container rules
│   ├── LocalizationService.cs       # FR/EN multilingual support
│   └── ThemeService.cs              # Light/Dark theme management
├── ViewModels/
│   ├── MainWindowViewModel.cs       # Main ViewModel (MVVM)
│   ├── GraphViewModel.cs            # Graph view ViewModel
│   ├── RelationsViewModel.cs        # Relations ViewModel (User→Group, Group→Group, GPO, PSO)
│   └── TierConfigurationViewModel.cs
├── Views/
│   ├── MainWindow.axaml             # Main interface with GPO badge in tree
│   ├── GraphWindow.axaml            # Force-directed graph view
│   ├── RelationsWindow.axaml        # Relations window (4 tabs)
│   ├── NewDomainDialog.axaml        # New domain dialog
│   ├── TierConfigurationWindow.axaml
│   └── AboutDialog.axaml
├── Graph/
│   ├── GraphBuilder.cs              # Build graph from AD tree
│   ├── GraphCanvas.cs               # Avalonia graph renderer (zoom/pan/hit-test)
│   ├── GraphNode.cs / GraphEdge.cs  # Graph model
│   ├── GraphFilter.cs               # Type/tier/nesting filters
│   └── ForceSimulation.cs           # Force-directed algorithm
└── Converters/
    ├── BoolToStringConverter.cs
    ├── LocalizeConverter.cs
    └── MarkdownConverter.cs
```

---

## 🔧 Technologies

| Component | Version | Role |
|---|---|---|
| **.NET** | 10 | Cross-platform runtime |
| **Avalonia UI** | 11 | Cross-platform UI framework |
| **CommunityToolkit.Mvvm** | latest | MVVM implementation |
| **Markdig** | latest | Markdown rendering |
| **System.Text.Json** | built-in | JSON serialization |

---

## 📝 Use Cases

| Profile | Use Case |
|---|---|
| **Trainer / Student** | Learn and teach AD concepts without real infrastructure |
| **Administrator** | Document and audit an existing AD architecture |
| **Architect** | Design and validate a new AD structure before deployment |
| **Pentester / Red Team** | Visualize attack paths via group relations and tiers |
| **Integrator** | Generate ready-to-deploy PowerShell scripts |

---

## 🎯 Roadmap

- [x] Complete default AD structure faithful to a fresh domain
- [x] Force-directed relationship graph view
- [x] GPO / PSO / MemberOf management
- [x] PowerShell export (structure, GPOs, PSOs)
- [x] Multilingual support FR/EN
- [x] Rich Markdown descriptions
- [x] Import from a real Active Directory (via PowerShell)
- [x] Group nesting (Group → Group) in graph and relations
- [x] GPO visual badge in TreeView
- [x] Split Relations window: User → Group and Group → Group tabs
- [x] Light / Dark theme
- [ ] Drag & Drop to move objects
- [ ] Search and filtering in the tree
- [ ] Multi-domain / forest support
- [ ] Export to diagrams (Draw.io, Visio)

---

## 📄 License

This project is licensed under the **Creative Commons Attribution-NonCommercial 4.0 International (CC BY-NC 4.0)**.

[![License: CC BY-NC 4.0](https://img.shields.io/badge/License-CC%20BY--NC%204.0-lightgrey.svg)](https://creativecommons.org/licenses/by-nc/4.0/)

### ✅ You are free to
- **Share** — copy and redistribute the material in any medium or format
- **Adapt** — remix, transform, and build upon the material

### ⚠️ Under the following terms
- **Attribution** — You **must** give appropriate credit to the original author, provide a link to this repository, and indicate if changes were made.

  Required attribution notice:
  ```
  Based on SMAD-X — Expert Active Directory Simulator
  Original work: https://github.com/JM2K69/SMAD-X
  Copyright (c) 2025-2026 SMAD-X Project
  Licensed under CC BY-NC 4.0
  ```

- **NonCommercial** — You may **not** use this project or any derivative for commercial purposes without explicit prior written permission from the author.

See the full [LICENSE](./LICENSE) file for details.

---

> This project was inspired by [MockAD-Release](https://github.com/shokkadev/MockAD-Release) by shokkadev.

## 🤝 Contributing

Contributions are welcome! Feel free to open issues or pull requests.

## 👨‍💻 Author

**JM2K69**


<p align="center">
  <img alt="Version" src="https://img.shields.io/badge/version-0.2.0-blue"/>
  <img alt=".NET" src="https://img.shields.io/badge/.NET-10-purple"/>
  <img alt="Avalonia" src="https://img.shields.io/badge/Avalonia-11-blueviolet"/>
  <img alt="Platform" src="https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey"/>
  <img alt="Language" src="https://img.shields.io/badge/language-FR%20%7C%20EN-green"/>
</p>

**SMAD-X** (*Simulate, Model and Audit Active Directory eXpert*) is an expert Active Directory simulator built with Avalonia UI and .NET 10. It generates an AD structure faithful to a fresh Windows Server installation and lets you visualize, document and export it without any real infrastructure.

---

## 🎯 Features

### 🏗️ Complete and Faithful Default AD Structure
- Automatic generation of all containers and objects present in a freshly promoted AD domain:
  - **Builtin**: Administrators, Users, Guests, Server Operators, Account Operators, Backup Operators, …
  - **Users**: Administrator, Guest, krbtgt, DefaultAccount, WDAGUtilityAccount + 16 default domain groups (Domain Admins, Schema Admins, Enterprise Admins, Protected Users, Key Admins, Cloneable Domain Controllers, Denied/Allowed RODC Password Replication Group, …)
  - **Computers**: default container for domain-joined workstations
  - **Domain Controllers** (OU): DC01 with all FSMO roles
  - **System**: Password Settings Container, Policies (Default Domain Policy, Default Domain Controllers Policy)
  - **ForeignSecurityPrincipals**
- Create a custom domain via `File > New Domain`
- Distinguished Names computed automatically

### 🌐 Interactive Relationship Graph
- Force-directed visualization of all relationships between objects
- Filters by object type (User, Group, Computer, OU, GPO, PSO…) and by tier
- Pan and zoom navigation

### 🔗 Relationship Management
- **MemberOf**: group membership links
- **GPO**: Group Policy Object links to domains and OUs
- **PSO**: Password Settings Object assignment to users and groups
- Dedicated Relations window to view and edit all links

### 📤 Export
- **Native JSON** (`.smadx.json`): full save and reload of the structure
- **PowerShell**: ready-to-deploy scripts to create the structure in a real AD
  - AD structure export (OUs, users, groups)
  - Linked GPOs export
  - PSOs export

### 🎨 Microsoft Tiering Model
- **Tier 0**: Domain controllers, critical accounts and systems
- **Tier 1**: Infrastructure and application servers
- **Tier 2**: Workstations and standard users
- Per-tier colors configurable through the UI

### 📝 Built-in Markdown Documentation
- Every object has a rich Markdown description
- Edit / Preview toggle
- Pre-filled and localized descriptions for all default objects

### 🌍 Multilingual Support
- Full interface available in **French** and **English**
- Language switch at runtime — no restart required

### ✅ Active Directory Validation
- Name validation following AD rules (forbidden characters, length, uniqueness)
- Container rules enforced (e.g. a Container can only hold CN objects, not OUs)

---

## 🚀 Quick Start

1. **Prerequisites**
   - .NET 10 SDK
   - Windows, macOS or Linux

2. **Build**
   ```bash
   dotnet build
   ```

3. **Run**
   ```bash
   dotnet run --project Activedirectory/SMAD-X.csproj
   ```

---

## 📖 Usage

### Default Structure on Startup

On launch, SMAD-X automatically loads a `contoso.com` domain with a complete AD structure faithful to a fresh Windows Server installation (all default containers, accounts and groups).

### Create a New Domain

`File > New Domain` → enter the FQDN (e.g. `corp.local`) and choose whether tiering should be assigned automatically.

### Add Objects

- Select a parent node in the tree
- Use the toolbar buttons: 📁 OU, 👤 User, 👥 Group, 💻 Computer, 🔑 GMSA…
- The object is created as a child of the selected node, with its DN computed automatically

### Copy / Paste

| Action | Shortcut |
|---|---|
| Copy an object (and its children) | `Ctrl+C` |
| Paste into the selected container | `Ctrl+V` |
| Delete | `Del` |

### Edit an Object

1. Select the object in the tree
2. The right panel shows its properties (Name, Type, Tier, DN, Description…)
3. Edit directly — changes are applied immediately

### Markdown Documentation

1. Select an object
2. In the details panel, toggle between `✏️ Edit` and `📖 Preview`
3. Write your documentation in Markdown — rendered in real time

### Graph View

`View > Graph View`: force-directed visualization of all relationships.  
Filter by object type or tier using the sidebar checkboxes.

### Relations (GPO, PSO, MemberOf)

`View > Relations`: dedicated window to browse and manage all links between objects.

### Save / Load

| Action | Menu |
|---|---|
| Save | `File > Save…` (`.smadx.json`) |
| Open | `File > Open…` |
| Export PowerShell (structure) | `File > Export PowerShell > AD Structure` |
| Export PowerShell (GPOs) | `File > Export PowerShell > GPOs` |
| Export PowerShell (PSOs) | `File > Export PowerShell > PSOs` |

---

## 🎨 Microsoft Tiering Model

| Tier | Color | Scope |
|---|---|---|
| **Tier 0** | 🔴 Red | Domain controllers, critical accounts and systems |
| **Tier 1** | 🟠 Orange | Infrastructure and application servers |
| **Tier 2** | 🟢 Green | Workstations and standard users |

Colors are configurable via `Settings > Tier Configuration`.

---

## 🏗️ Architecture

```
Activedirectory/
├── Models/
│   ├── ADObject.cs                  # Core data model (DN, GPO, PSO, MemberOf…)
│   ├── ADObjectType.cs              # AD object type enumeration
│   ├── ADTreeNode.cs                # TreeView display node
│   └── TierConfiguration.cs        # Tier color configuration
├── Services/
│   ├── ADDataService.cs             # Default structure, JSON save/load
│   ├── ADImportPowerShellService.cs # Import from PowerShell scripts
│   ├── ADPowerShellExportService.cs # Export to PowerShell scripts
│   ├── ADValidationService.cs       # Name validation and container rules
│   ├── LocalizationService.cs       # FR/EN multilingual support
│   └── ThemeService.cs              # Theme management
├── ViewModels/
│   ├── MainWindowViewModel.cs       # Main ViewModel (MVVM)
│   ├── GraphViewModel.cs            # Graph view ViewModel
│   ├── RelationsViewModel.cs        # Relations view ViewModel
│   └── TierConfigurationViewModel.cs
├── Views/
│   ├── MainWindow.axaml             # Main interface
│   ├── GraphWindow.axaml            # Force-directed graph view
│   ├── RelationsWindow.axaml        # Relations view
│   ├── NewDomainDialog.axaml        # New domain dialog
│   ├── TierConfigurationWindow.axaml
│   └── AboutDialog.axaml
├── Graph/
│   ├── GraphBuilder.cs              # Build graph from AD tree
│   ├── GraphCanvas.cs               # Avalonia graph renderer
│   ├── GraphNode.cs / GraphEdge.cs  # Graph model
│   ├── GraphFilter.cs               # Type/tier filters
│   └── ForceSimulation.cs           # Force-directed algorithm
└── Converters/
    ├── BoolToStringConverter.cs
    ├── LocalizeConverter.cs
    └── MarkdownConverter.cs
```

---

## 🔧 Technologies

| Component | Version | Role |
|---|---|---|
| **.NET** | 10 | Cross-platform runtime |
| **Avalonia UI** | 11 | Cross-platform UI framework |
| **CommunityToolkit.Mvvm** | latest | MVVM implementation |
| **Markdig** | latest | Markdown rendering |
| **System.Text.Json** | built-in | JSON serialization |

---

## 📝 Use Cases

| Profile | Use Case |
|---|---|
| **Trainer / Student** | Learn and teach AD concepts without real infrastructure |
| **Administrator** | Document and audit an existing AD architecture |
| **Architect** | Design and validate a new AD structure before deployment |
| **Integrator** | Generate ready-to-deploy PowerShell scripts |

---

## 🎯 Roadmap

- [x] Complete default AD structure faithful to a fresh domain (v2.0)
- [x] Force-directed relationship graph view
- [x] GPO / PSO / MemberOf management
- [x] PowerShell export (structure, GPOs, PSOs)
- [x] Multilingual support FR/EN
- [x] Rich Markdown descriptions
- [x] Import from a real Active Directory (via PowerShell)
- [ ] Drag & Drop to move objects
- [ ] Search and filtering in the tree
- [ ] Multi-domain / forest support
- [ ] Export to diagrams (Draw.io, Visio)
- [ ] Dark mode

---

## 📄 License

This project is licensed under the **Creative Commons Attribution-NonCommercial 4.0 International (CC BY-NC 4.0)**.

[![License: CC BY-NC 4.0](https://img.shields.io/badge/License-CC%20BY--NC%204.0-lightgrey.svg)](https://creativecommons.org/licenses/by-nc/4.0/)

### ✅ You are free to
- **Share** — copy and redistribute the material in any medium or format
- **Adapt** — remix, transform, and build upon the material

### ⚠️ Under the following terms
- **Attribution** — You **must** give appropriate credit to the original author, provide a link to this repository, and indicate if changes were made.

  Required attribution notice:
  ```
  Based on SMAD-X — Expert Active Directory Simulator
  Original work: https://github.com/<your-username>/SMAD-X
  Copyright (c) 2025-2026 SMAD-X Project
  Licensed under CC BY-NC 4.0
  ```

- **NonCommercial** — You may **not** use this project or any derivative for commercial purposes without explicit prior written permission from the author.

See the full [LICENSE](./LICENSE) file for details.

---

> This project was inspired by [MockAD-Release](https://github.com/shokkadev/MockAD-Release) by shokkadev.

## 🤝 Contributing

Contributions are welcome! Feel free to open issues or pull requests.

## 👨‍💻 Author

JM2K69

---

## 🎯 Fonctionnalités

### 🏗️ Structure AD par défaut complète et fidèle
- Génération automatique de tous les containers et objets présents dans un domaine AD fraîchement promu :
  - **Builtin** : Administrators, Users, Guests, Server Operators, Account Operators, Backup Operators, …
  - **Users** : Administrator, Guest, krbtgt, DefaultAccount, WDAGUtilityAccount + 16 groupes de domaine (Domain Admins, Schema Admins, Enterprise Admins, Protected Users, Key Admins, Cloneable Domain Controllers, Denied/Allowed RODC Replication Group, …)
  - **Computers** : container par défaut pour les postes joints au domaine
  - **Domain Controllers** (OU) : DC01 avec tous les rôles FSMO
  - **System** : Password Settings Container, Policies (Default Domain Policy, Default Domain Controllers Policy)
  - **ForeignSecurityPrincipals**
- Création d'un nouveau domaine personnalisé via `Fichier > Nouveau domaine`
- Distinguished Names calculés automatiquement

### 🌐 Vue Graphe de relations interactif
- Visualisation force-directed de toutes les relations entre objets
- Filtres par type d'objet (User, Group, Computer, OU, GPO, PSO…) et par tier
- Navigation et zoom dans le graphe

### 🔗 Gestion des relations
- **MemberOf** : appartenance aux groupes
- **GPO** : liaison des stratégies de groupe aux domaines et OUs
- **PSO** : application des Password Settings Objects aux utilisateurs et groupes
- Fenêtre Relations dédiée pour visualiser et éditer toutes les liaisons

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

### 🌍 Support multilingue
- Interface entièrement disponible en **Français** et en **English**
- Changement de langue à chaud sans redémarrage

### ✅ Validation Active Directory
- Validation des noms selon les règles AD (caractères interdits, longueur, unicité)
- Règles de conteneurs respectées (ex : un container ne peut contenir que des CN, pas des OUs)
  - Préservation de toute la hiérarchie et métadonnées

- **Statistiques**
  - Compteur d'objets par type en temps réel
  - Distinguished Names automatiques

## 🚀 Démarrage rapide

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
   dotnet run --project Activedirectory/SMAD-X.csproj
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

### Relations (GPO, PSO, MemberOf)

`Affichage > Relations` : fenêtre dédiée pour consulter et gérer toutes les liaisons entre objets.

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
Activedirectory/
├── Models/
│   ├── ADObject.cs                  # Modèle de données principal (DN, GPO, PSO, MemberOf…)
│   ├── ADObjectType.cs              # Énumération des types d'objets AD
│   ├── ADTreeNode.cs                # Nœud pour l'affichage TreeView
│   └── TierConfiguration.cs        # Configuration des couleurs de tiering
├── Services/
│   ├── ADDataService.cs             # Structure par défaut, sauvegarde/chargement JSON
│   ├── ADImportPowerShellService.cs # Import depuis scripts PowerShell
│   ├── ADPowerShellExportService.cs # Export vers scripts PowerShell
│   ├── ADValidationService.cs       # Validation des noms et règles de conteneurs
│   ├── LocalizationService.cs       # Support multilingue FR/EN
│   └── ThemeService.cs              # Gestion du thème
├── ViewModels/
│   ├── MainWindowViewModel.cs       # ViewModel principal (MVVM)
│   ├── GraphViewModel.cs            # ViewModel vue graphe
│   ├── RelationsViewModel.cs        # ViewModel vue relations
│   └── TierConfigurationViewModel.cs
├── Views/
│   ├── MainWindow.axaml             # Interface principale
│   ├── GraphWindow.axaml            # Vue graphe force-directed
│   ├── RelationsWindow.axaml        # Vue relations
│   ├── NewDomainDialog.axaml        # Dialogue nouveau domaine
│   ├── TierConfigurationWindow.axaml
│   └── AboutDialog.axaml
├── Graph/
│   ├── GraphBuilder.cs              # Construction du graphe depuis l'arbre AD
│   ├── GraphCanvas.cs               # Rendu Avalonia du graphe
│   ├── GraphNode.cs / GraphEdge.cs  # Modèles du graphe
│   ├── GraphFilter.cs               # Filtres par type/tier
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

- [x] Structure AD par défaut complète et fidèle (v2.0)
- [x] Vue graphe de relations (force-directed)
- [x] Gestion GPO / PSO / MemberOf
- [x] Export PowerShell (structure, GPOs, PSOs)
- [x] Support multilingue FR/EN
- [x] Descriptions Markdown enrichies
- [ ] Import depuis un Active Directory réel (via PowerShell)
- [ ] Drag & Drop pour déplacer les objets
- [ ] Recherche et filtrage dans l'arborescence
- [ ] Support multi-domaines / forêts
- [ ] Export vers diagrammes (Draw.io, Visio)
- [ ] Mode sombre

---

## 📄 Licence

Ce projet est inspiré de [MockAD-Release](https://github.com/shokkadev/MockAD-Release) par shokkadev.

## 🤝 Contribution

Les contributions sont les bienvenues ! N'hésitez pas à ouvrir des issues ou des pull requests.

## 👨‍💻 Auteur

Créé avec l'assistance de GitHub Copilot.
