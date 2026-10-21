# Polices embarquées pour l'export PDF

Polices de secours utilisées par `ADDocumentReportService` pour rendre emoji et symboles
dans les PDF sans dépendre des polices installées (Windows, Linux, macOS).
Elles sont embarquées dans l'exécutable (`EmbeddedResource`, voir `SMAD-X.csproj`).

| Fichier | Rôle | Source |
|---|---|---|
| `NotoEmoji-wght.ttf` | emoji monochromes (👤 🔴 🔗 📁 …) | google/fonts, `ofl/notoemoji/NotoEmoji[wght].ttf` |
| `NotoSansSymbols2-Regular.ttf` | symboles (✓ ✕ ⚠ ⬇ ★ …) | notofonts/notofonts.github.io, `fonts/NotoSansSymbols2/hinted/ttf/` |
| `NotoSansMono-Regular.ttf` | flèches, ≤ ≥, filets (→ ≤ ≥ ─) | notofonts/notofonts.github.io, `fonts/NotoSansMono/hinted/ttf/` |

Licence : SIL Open Font License 1.1 — voir `OFL.txt`.
