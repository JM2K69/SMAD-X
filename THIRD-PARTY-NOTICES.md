# Third-party notices / Composants tiers

SMAD-X and SMAD-X Studio are licensed under the Apache License 2.0 (see `LICENSE`).
Their executables also contain the following third-party components, each under its own license.

SMAD-X et SMAD-X Studio sont sous licence Apache 2.0 (voir `LICENSE`).
Leurs exécutables contiennent aussi les composants tiers ci-dessous, chacun sous sa propre licence.

## Libraries / Bibliothèques

| Component | License | Project |
|---|---|---|
| .NET runtime (self-contained) | MIT | https://github.com/dotnet/runtime |
| Avalonia (Avalonia.Desktop, Themes.Fluent, Controls.DataGrid, Fonts.Inter, Skia, HarfBuzz, Win32, X11, Native, FreeDesktop) | MIT | https://github.com/AvaloniaUI/Avalonia |
| ANGLE (Avalonia.Angle.Windows.Natives) | BSD 3-Clause | https://chromium.googlesource.com/angle/angle |
| SkiaSharp and Skia | MIT (SkiaSharp), BSD 3-Clause (Skia) | https://github.com/mono/SkiaSharp |
| HarfBuzzSharp and HarfBuzz | MIT (HarfBuzzSharp), Old MIT (HarfBuzz) | https://github.com/mono/SkiaSharp |
| MicroCom.Runtime | MIT | https://github.com/kekekeks/MicroCom |
| Tmds.DBus.Protocol | MIT | https://github.com/tmds/Tmds.DBus |
| CommunityToolkit.Mvvm | MIT | https://github.com/CommunityToolkit/dotnet |
| OfficeIMO (Word, Markdown, Pdf, Html) | MIT | https://github.com/EvotecIT/OfficeIMO |
| DocumentFormat.OpenXml | MIT | https://github.com/dotnet/Open-XML-SDK |
| AngleSharp, AngleSharp.Css | MIT | https://github.com/AngleSharp/AngleSharp |
| Microsoft.IO.RecyclableMemoryStream | MIT | https://github.com/microsoft/Microsoft.IO.RecyclableMemoryStream |
| Microsoft.Extensions.*.Abstractions, System.IO.Packaging | MIT | https://github.com/dotnet/runtime |

The copyright notices of these components are in the license file of each project, at the address given above.

Les mentions de copyright de ces composants figurent dans le fichier de licence de chaque projet, à l'adresse indiquée.

### MIT License

```
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Tier models / Modèles de cloisonnement

SMAD-X includes the structure (names, locations, GPO links and delegated rights) of two published Active Directory tier models, generated from a pinned release by `tools/TierModelGenerator` (`SMAD-X.Core/TierModels/*.json`). Both are under the MIT License above.

SMAD-X reprend la structure (noms, emplacements, liens de GPO et droits délégués) de deux modèles de cloisonnement d'Active Directory publiés, générée depuis une version figée par `tools/TierModelGenerator` (`SMAD-X.Core/TierModels/*.json`). Les deux sont sous la licence MIT ci-dessus.

| Model | Version (commit) | License | Copyright | Project |
|---|---|---|---|---|
| Active Directory Tier Model | v2.1.0 (`6be6b820d824def06c951805f04816d14c6471ef`) | MIT | Copyright (c) Microsoft Corporation. | https://github.com/microsoft/ActiveDirectoryTierModel |
| HardenAD | HardenAD-2.9.9v2 (`bbe9474fc396d642988c874a1d6b1442f718c5a7`) | MIT | Copyright (c) 2021 HardenAD | https://github.com/LoicVeirman/HardenAD |

## Fonts / Polices

| Font | License | Copyright |
|---|---|---|
| Noto Emoji, Noto Sans Symbols 2, Noto Sans Mono (PDF export) | SIL Open Font License 1.1 | Google LLC, The Noto Project Authors |
| Inter (user interface, through Avalonia.Fonts.Inter) | SIL Open Font License 1.1 | The Inter Project Authors |

The full text of the SIL Open Font License 1.1 is in `OFL.txt`.

Le texte complet de la SIL Open Font License 1.1 est dans `OFL.txt`.
