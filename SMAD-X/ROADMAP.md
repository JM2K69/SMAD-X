# SMAD-X — Roadmap : Évolution du domaine & Délégations AD

> Dernière mise à jour : 2025-06-01

---

## Partie 1 — Timeline / Évolution du domaine

### Étape 1 — Modèle `DomainSnapshot`
**Fichier :** `SMAD-X\Models\DomainSnapshot.cs`
- `FilePath` (string), `DomainName` (string), `SnapshotDate` (DateTime), `Root` (ADObject)

### Étape 2 — Modèle `ADChangeItem`
**Fichier :** `SMAD-X\Models\ADChangeItem.cs`
- `ChangeType` : enum `Added | Removed | Modified`
- `ObjectName`, `DistinguishedName`, `ObjectType`, `ChangedFields`

### Étape 3 — Service `ADDiffService`
**Fichier :** `SMAD-X\Services\ADDiffService.cs`
- `Compare(ADObject before, ADObject after) → List<ADChangeItem>`

### Étape 4 — `DomainTimelineViewModel`
**Fichier :** `SMAD-X\ViewModels\DomainTimelineViewModel.cs`
- Snapshots, SnapshotBefore/After, Changes, FilterType, FilterText
- Commandes : AddSnapshot, RemoveSnapshot, Compare

### Étape 5 — Vue `DomainTimelineWindow`
**Fichiers :** `SMAD-X\Views\DomainTimelineWindow.axaml/.axaml.cs`
- Liste snapshots | ComboBox Avant/Après | DataGrid changements colorés

### Étape 6 — Intégration menu
- `Analyse > Évolution du domaine` — `Ctrl+T`

---

## Partie 2 — Délégations Active Directory

### Étape 7 — Modèle `ADDelegation`
**Fichier :** `SMAD-X\Models\ADDelegation.cs`
- `TrusteeName`, `TrusteeType`, `TargetDN`, `Right`, `IsInherited`, `Tier`

### Étape 8 — Ajout dans `ADObject`
```csharp
public ObservableCollection<ADDelegation> Delegations { get; set; } = new();
```

### Étape 9 — Export PowerShell des délégations
- `Get-Acl` dans `Build-NodeJson` → clé `"Delegations"` dans le JSON

### Étape 10 — `DelegationsViewModel`
**Fichier :** `SMAD-X\ViewModels\DelegationsViewModel.cs`
- Aplatit toutes les délégations de l'arbre, filtres, ExportToCsvCommand

### Étape 11 — Vue `DelegationsWindow`
**Fichiers :** `SMAD-X\Views\DelegationsWindow.axaml/.axaml.cs`
- Filtres + DataGrid + Export CSV

### Étape 12 — Intégration menu
- `Analyse > Délégations` — `Ctrl+D`

---

## Tableau des dépendances

| # | Étape | Dépendances |
|---|-------|-------------|
| 1 | Modèle DomainSnapshot | — |
| 2 | Modèle ADChangeItem | — |
| 3 | ADDiffService | 1, 2 |
| 4 | DomainTimelineViewModel | 3 |
| 5 | DomainTimelineWindow | 4 |
| 6 | Menu Timeline | 5 |
| 7 | Modèle ADDelegation | — |
| 8 | Delegations dans ADObject | 7 |
| 9 | Export PS délégations | 8 |
| 10 | DelegationsViewModel | 7, 8 |
| 11 | DelegationsWindow | 10 |
| 12 | Menu Délégations | 11 |