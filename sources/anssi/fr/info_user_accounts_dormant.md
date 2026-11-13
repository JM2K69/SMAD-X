# info_user_accounts_dormant — Comptes actifs inutilisés

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [info_user_accounts_dormant.md](../en/info_user_accounts_dormant.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `info_user_accounts_dormant` |
| Ajouté à la checklist | 2018-09-26 |
| Dernière modification | 2024-04-23 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | ACC-02 |

## Règles de SMAD-X qui le citent

- **ACC-02** — Compte dormant

## Texte officiel

La forêt contient des comptes dormants (… sur … comptes non désactivés soit …%). Ce type de compte correspond à des comptes :

- utilisateur ou machine,
- non désactivés,
- dont le mot de passe a été changé il y a plus de trois ans,
- qui ne se sont pas authentifiés auprès de l'Active Directory depuis plus d'un an ou qui ont été créés il y a plus d'un an et ne se sont jamais connectés.

Ces comptes dormants sont soit des comptes légitimes devant être rarement utilisés, soit des comptes obsolètes.

Les comptes obsolètes peuvent donner accès à des personnes n'en ayant plus besoin (suite à un départ par exemple) ou être utilisés de manière furtive par un attaquant, ce qui est d'autant plus préjudiciable si ces comptes sont privilégiés. La présence de ces comptes augmente par ailleurs le travail de gestion des comptes (revue de droits, etc.).

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
