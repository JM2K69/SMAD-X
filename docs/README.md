# Site de documentation / Documentation site

Publié sur / Published at **https://jm2k69.github.io/SMAD-X/** (français) et / and **https://jm2k69.github.io/SMAD-X/en/** (English).

## Organisation

| Chemin | Contenu |
|---|---|
| `zensical.fr.toml`, `zensical.en.toml` | configuration Zensical de chaque langue (navigation, thème, sélecteur de langue) |
| `fr/`, `en/` | pages Markdown de chaque langue |
| `*/assets/screens/` | captures d'écran (générées) |
| `fr/reference/regles-audit.md`, `en/reference/audit-rules.md` | règles d'audit (générées) |
| `fr/exercices/catalogue.md`, `en/labs/catalog.md` | catalogue des exercices (généré) |

## Publication

Le workflow `.github/workflows/docs.yml` construit les deux sites à chaque push sur `master` qui touche `docs/`, puis les publie sur GitHub Pages. Le français est à la racine, l'anglais sous `/en/`. La construction est **stricte** : un lien cassé fait échouer la publication.

Il faut avoir réglé une fois **Settings > Pages > Source** sur **GitHub Actions** dans le dépôt.

Aucune construction locale n'est nécessaire. Pour prévisualiser malgré tout : `pip install zensical`, puis `zensical serve -f docs/zensical.fr.toml`.

## Contenu généré

Les captures et les pages de référence sont produites par `tools/DocGenerator`, à relancer quand l'interface, les règles d'audit ou les exercices changent :

```bash
dotnet run --project tools/DocGenerator -- .
```

L'outil rend les fenêtres de SMAD-X **hors écran** (Avalonia Headless avec le moteur Skia), en français et en anglais : rien ne s'affiche à l'écran et aucune touche n'est simulée.

## Règle : pas de solution

La documentation **ne donne aucune solution** des exercices fournis :

- les captures utilisent le domaine d'exemple et une démonstration propre à la documentation (`DemoWeakness`, `DemoLab` dans `tools/DocGenerator`), qu'aucun exercice n'utilise ;
- les pages ne décrivent ni le chemin ni la correction d'un exercice ;
- les exemples du mode opératoire sont fictifs.
