# MockAD - Simulateur Active Directory

Une application de simulation et de visualisation d'Active Directory construite avec Avalonia UI et .NET 10.

## 🎯 Fonctionnalités

- **Structure Active Directory complète**
  - Domaines, OUs (Unités Organisationnelles), Utilisateurs, Groupes, Ordinateurs, GMSA, et Policies
  - Hiérarchie arborescente avec TreeView interactif

- **Gestion des objets**
  - Ajouter, renommer, copier/coller, supprimer des objets
  - Drag & Drop pour réorganiser la structure (à implémenter)
  - Copier/Coller (Ctrl+C, Ctrl+V) pour dupliquer des nœuds et leurs enfants

- **Visualisation**
  - Interface similaire à ADUC (Active Directory Users & Computers)
  - Icônes visuelles pour chaque type d'objet
  - Code couleur basé sur le modèle de tiering (Tier 0, Tier 1, Tier 2)

- **Descriptions Markdown**
  - Support complet du Markdown pour documenter les objets
  - Mode édition et mode prévisualisation

- **Validation**
  - Validation des noms selon les règles Active Directory
  - Règles de conteneurs (ex: un domaine ne peut contenir que des OUs)

- **Sauvegarde/Chargement**
  - Export/Import en JSON (.mockad.json)
  - Préservation de toute la hiérarchie et métadonnées

- **Statistiques**
  - Compteur d'objets par type en temps réel
  - Distinguished Names automatiques

## 🚀 Démarrage rapide

1. **Prérequis**
   - .NET 10 SDK
   - Windows, macOS, ou Linux

2. **Compilation**
   ```bash
   dotnet build
   ```

3. **Exécution**
   ```bash
   dotnet run
   ```

## 📖 Utilisation

### Créer une nouvelle structure

1. Au démarrage, une structure d'exemple est automatiquement chargée
2. Ou utilisez `Fichier > Nouvelle structure` pour recommencer

### Ajouter des objets

- Utilisez les boutons de la barre d'outils : 📁 OU, 👤 Utilisateur, 👥 Groupe, etc.
- Sélectionnez d'abord le conteneur parent dans l'arborescence
- L'objet sera ajouté comme enfant du nœud sélectionné

### Copier/Coller

1. Sélectionnez un objet
2. Appuyez sur `Ctrl+C` ou utilisez `Édition > Copier`
3. Sélectionnez le conteneur de destination
4. Appuyez sur `Ctrl+V` ou utilisez `Édition > Coller`
5. L'objet et tous ses enfants seront dupliqués avec "(Copy)" ajouté au nom

### Modifier un objet

1. Sélectionnez l'objet dans l'arborescence
2. Le panneau de détails à droite affiche toutes les propriétés
3. Modifiez le Tier, la description, etc.
4. Les changements sont appliqués immédiatement

### Utiliser le Markdown

1. Sélectionnez un objet
2. Dans le panneau de détails, cliquez sur `✏️ Éditer`
3. Écrivez votre documentation en Markdown
4. Cliquez sur `📖 Prévisualiser` pour voir le rendu

### Sauvegarder/Charger

- `Fichier > Enregistrer...` : Sauvegarde la structure en JSON
- `Fichier > Ouvrir...` : Charge une structure précédemment sauvegardée
- Les fichiers utilisent l'extension `.mockad.json`

## 🎨 Modèle de Tiering

L'application supporte le modèle de tiering Microsoft pour la sécurité Active Directory :

- **Tier 0** (Rouge) : Contrôleurs de domaine, admins de domaine, systèmes critiques
- **Tier 1** (Orange) : Serveurs d'applications et d'infrastructure
- **Tier 2** (Vert) : Postes de travail et utilisateurs

Chaque objet peut être assigné à un tier, qui est visualisé par code couleur.

## 🏗️ Architecture

```
Activedirectory/
├── Models/
│   ├── ADObject.cs           # Modèle de données principal
│   ├── ADObjectType.cs       # Énumération des types d'objets
│   └── ADTreeNode.cs         # Nœud pour l'affichage TreeView
├── Services/
│   ├── ADDataService.cs      # Sauvegarde/Chargement JSON
│   └── ADValidationService.cs # Validation des noms et règles
├── ViewModels/
│   ├── MainWindowViewModel.cs # ViewModel principal (MVVM)
│   └── ViewModelBase.cs
├── Views/
│   └── MainWindow.axaml      # Interface utilisateur principale
└── Converters/
    └── BoolToStringConverter.cs
```

## 🔧 Technologies

- **Avalonia UI 12.0** : Framework UI cross-platform
- **.NET 10** : Runtime moderne
- **CommunityToolkit.Mvvm** : Implémentation MVVM
- **System.Text.Json** : Sérialisation JSON

## 📝 Cas d'usage

- **Formation** : Apprendre les concepts Active Directory sans infrastructure réelle
- **Documentation** : Documenter une architecture AD existante
- **Planification** : Concevoir une nouvelle structure AD avant déploiement
- **Démonstration** : Présenter des concepts de tiering et de sécurité AD
- **Tests** : Simuler des changements de structure sans risque

## 🎯 Améliorations futures

- [ ] Drag & Drop pour déplacer les objets
- [ ] Export vers PowerShell scripts
- [ ] Import depuis Active Directory réel (via scripts PowerShell)
- [ ] Recherche et filtrage dans l'arborescence
- [ ] Graphiques et visualisations avancées
- [ ] Support des relations entre objets (membre de groupe, etc.)
- [ ] Export vers diagrammes (Visio, Draw.io, etc.)
- [ ] Mode sombre
- [ ] Support multi-domaines/forêts

## 📄 Licence

Ce projet est inspiré de [MockAD-Release](https://github.com/shokkadev/MockAD-Release) par shokkadev.

## 🤝 Contribution

Les contributions sont les bienvenues ! N'hésitez pas à ouvrir des issues ou des pull requests.

## 👨‍💻 Auteur

Créé avec l'assistance de GitHub Copilot.
