# Les sources de SMAD-X

> English: [README.md](README.md)

Les règles d'audit de SMAD-X, ses niveaux et ses recettes d'exercice s'appuient sur ces référentiels. Chaque fiche dit d'où vient l'élément, quand il a été relevé, sous quelle licence, et quelles règles de SMAD-X le citent. Les textes officiels ne sont repris que lorsque la licence le permet.

Relevé du 2026-11-13, par `tools/SmadxSources`.

| Référentiel | Source | Publication relevée | Fiches | Licence |
|---|---|---|---|---|
| [ANSSI — Points de contrôle Active Directory](anssi/fr/README.md) | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) | 2026-11-12 | 33 | licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais |
| [NIST — SP 800-53 Rev. 5 et SP 800-63B](nist/fr/README.md) | [csrc.nist.gov/…](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final) | SP 800-53 Rev. 5, OSCAL 5.2.0 (2026-10-22) | 6 | domaine public : publication du gouvernement des États-Unis (17 U.S.C. § 105) ; énoncés officiels repris, en anglais |
| [CIS Controls v8](cis/fr/README.md) | [www.cisecurity.org/…](https://www.cisecurity.org/controls/v8) | 2025-03-19 | 5 | © Center for Internet Security, licence CC BY-NC-ND 4.0 : seuls l'identifiant et le titre officiel de chaque mesure sont repris, avec le lien |
| [MITRE ATT&CK® (Enterprise)](mitre-attack/fr/README.md) | [attack.mitre.org](https://attack.mitre.org/) | ATT&CK v20.1 | 14 | conditions d'utilisation de MITRE ATT&CK : reproduction autorisée avec la mention ci-dessous ; premier paragraphe de chaque description repris, en anglais |
| [Microsoft Learn](microsoft/fr/README.md) | [learn.microsoft.com](https://learn.microsoft.com/) | — | 11 | © Microsoft : seuls le titre et la description de chaque page sont repris, avec le lien ; le contenu reste sur Microsoft Learn |
| [Modèles de cloisonnement reproduits](tier-models/fr/README.md) | [github.com/…](https://github.com/JM2K69/SMAD-X/tree/master/SMAD-X.Core/TierModels) | — | 2 | MIT, avec le copyright de chaque projet ; les modèles sont reproduits dans SMAD-X.Core/TierModels |

## Rangement

Un dossier par référentiel (`anssi`, `nist`, `cis`, `mitre-attack`, `microsoft`, `tier-models`), et dedans un dossier par langue (`fr`, `en`). Chaque élément cité a sa fiche, nommée d'après l'identifiant que SMAD-X affiche : `anssi/fr/vuln_spn_priv.md`, `mitre-attack/fr/T1558.003.md`, `nist/fr/AC-6(5).md`, `cis/fr/5.4.md` ; pour une page Microsoft Learn, le nom de la page (`microsoft/fr/protected-users-security-group.md`). Le `README.md` de chaque dossier en est le sommaire.

Une fois générés, les PDF sont à côté des fiches, sous le même nom : `anssi/fr/vuln_spn_priv.pdf`. On peut ainsi donner ou imprimer la seule fiche d'une recommandation.

## Mettre à jour

```bash
dotnet run --project tools/SmadxSources -- update
```

L'outil relit chaque source sur le web, réécrit ces fiches et `sources/sources.json`, et affiche ce qui a changé depuis le relevé précédent (titre, niveau, texte). Si un élément cité par SMAD-X a disparu de sa source, il s'arrête sans rien écrire. Relisez le diff avant de commiter.

```bash
dotnet run --project tools/SmadxSources -- write
```

Réécrit les fiches depuis `sources/sources.json`, sans rien relire sur le web : après le changement du titre d'une règle, par exemple. Une nouvelle référence dans une règle demande un `update`.

## Les PDF

```bash
dotnet run --project tools/SmadxSources -- pdf
dotnet run --project tools/SmadxSources -- pdf --only vuln_spn_priv
```

Un PDF par fiche et par langue, aux couleurs de SMAD-X, à côté de la fiche. `--only` ne génère que les fiches d'un identifiant (`vuln_spn_priv`) ou d'un référentiel (`anssi`). Les PDF ne sont pas versionnés : on les régénère depuis les fiches.

## Dans SMAD-X Studio

Studio embarque ce relevé : **Fichier > Fiches des sources...** montre les fiches règle par règle, les exporte en PDF et relit une source sur le web pour voir ce qui a changé depuis la version installée, sans le dépôt ni le SDK .NET. Le code est le même que celui de cet outil (`SMAD-X.Core/Services/Sources`).
