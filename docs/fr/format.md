# Format des fichiers

## Le domaine : `.smad-x.json`

Un fichier JSON lisible, qui se versionne bien dans Git :

```json
{
  "Version": 2,
  "Domain": {
    "Name": "contoso.com",
    "Type": "Domain",
    "DistinguishedName": "DC=contoso,DC=com",
    "Children": [ { "Name": "Users", "Type": "Container", "Children": [ ] } ]
  },
  "SitesTopology": { "Sites": [ ], "SiteLinks": [ ] },
  "TierConfigurations": [ ]
}
```

| Champ | Contenu |
|---|---|
| `Version` | version du format, actuellement **2** |
| `Domain` | l'arbre des objets : nom, type, DN, tier, description Markdown, appartenances, GPO liées, délégations, attributs de sécurité, enfants |
| `SitesTopology` | sites, sous-réseaux, contrôleurs de domaine et liens de réplication |
| `TierConfigurations` | les tiers personnalisés (noms, niveaux, couleurs), s'il y en a |

### Versions

| Version | Structure |
|---|---|
| 1 | l'objet domaine directement à la racine du fichier, sans champ `Version` |
| 2 | le document ci-dessus |

À l'ouverture, un fichier de version 1 est **converti** automatiquement, et la barre d'état le signale. Un fichier écrit par une version **plus récente** de SMAD-X est refusé avec un message qui donne les deux versions, plutôt que d'être lu à moitié.

## L'exercice : `.smadx-lab.json`

Un exercice contient un énoncé, un domaine de départ complet (un document `.smad-x.json`, migré à la lecture comme un fichier normal) et des objectifs. Sa structure est décrite dans [Créer un exercice](exercices/creer.md).

## Les scripts PowerShell

| Script | Produit par | Rôle |
|---|---|---|
| Export | **Fichier > Exporter en PowerShell...** | recrée la structure dans un AD |
| Import | **Fichier > Générer le script d'import depuis AD...** | lit un AD et écrit un `.smad-x.json` |

Voir [Importer et exporter](export.md).
