# warning_privileged_members_not_empty — Groupes privilégiés non vides

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [warning_privileged_members_not_empty.md](../en/warning_privileged_members_not_empty.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `warning_privileged_members_not_empty` |
| Niveau ANSSI | 3 (1 : le plus critique) |
| Ajouté à la checklist | 2019-01-24 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | PRIV-03 |

## Règles de SMAD-X qui le citent

- **PRIV-03** — Groupe privilégié qui devrait être vide

## Texte officiel

Les groupes privilégiés de la forêt ne sont pas vides ou ne contiennent pas uniquement les comptes d'administration intégrés (RID 500).

*(Tableau des objets concernés, dans le rapport d'analyse.)*

Note : le nombre d'utilisateurs n'inclut pas les comptes d'administration intégrés.

## Recommandation officielle

…

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
