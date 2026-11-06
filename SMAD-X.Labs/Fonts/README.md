# Polices embarquées pour l'export PDF

Polices de secours utilisées par `MarkdownPdf` (SMAD-X.Labs) pour rendre emoji et symboles
dans les PDF de la suite (rapports de SMAD-X, comptes rendus de Studio, aide-mémoire de SMAR-X)
sans dépendre des polices installées (Windows, Linux, macOS).
Elles sont embarquées dans `SMAD-X.Labs` (`EmbeddedResource`, voir `SMAD-X.Labs.csproj`), donc dans chaque exécutable.

| Fichier | Rôle | Source |
|---|---|---|
| `NotoEmoji-wght.ttf` | emoji monochromes (👤 🔴 🔗 📁 …) | google/fonts, `ofl/notoemoji/NotoEmoji[wght].ttf` |
| `NotoSansSymbols2-Regular.ttf` | symboles (✓ ✕ ⚠ ⬇ ★ …) | notofonts/notofonts.github.io, `fonts/NotoSansSymbols2/hinted/ttf/` |
| `NotoSansMono-Regular.ttf` | flèches, ≤ ≥, filets (→ ≤ ≥ ─) | notofonts/notofonts.github.io, `fonts/NotoSansMono/hinted/ttf/` |

Licence : SIL Open Font License 1.1 — voir `OFL.txt`.
