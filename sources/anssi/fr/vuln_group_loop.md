# vuln_group_loop — Présences de boucles d'imbrication de groupes

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_group_loop.md](../en/vuln_group_loop.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_group_loop` |
| Niveau ANSSI | 4 (1 : le plus critique) |
| Ajouté à la checklist | 2023-12-06 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | GRP-01 |

## Règles de SMAD-X qui le citent

- **GRP-01** — Boucle d'imbrication de groupes

## Texte officiel

La présence de boucles d'imbrication de groupes ne présente pas de danger pour la sécurité de l'Active Directory mais peut affecter le fonctionnement de différents outils de sécurité.

*(Tableau des objets concernés, dans le rapport d'analyse.)*

## Recommandation officielle

Par mesure d'hygiène il est recommandé de nettoyer les boucles d'imbrication de groupes.

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
