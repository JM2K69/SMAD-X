# vuln_owner — Objets ayant un propriétaire inadapté

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_owner.md](../en/vuln_owner.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_owner` |
| Niveau ANSSI | 3 (1 : le plus critique) |
| Ajouté à la checklist | 2019-08-27 |
| Dernière modification | 2021-05-31 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | ACL-01 |

## Règles de SMAD-X qui le citent

- **ACL-01** — Objet critique avec un propriétaire inadapté

## Texte officiel

Des objets, créés il y a plus de 7 jours, ont des propriétaires non standard. Note : seuls les 20 premiers objets sont affichés, pour les 10000 premiers propriétaires.

*(Tableau des objets concernés, dans le rapport d'analyse.)*

## Recommandation officielle

Tous les objets (utilisateurs, groupes, sMSA, gMSA, ordinateurs, OU et GPO) doivent avoir pour propriétaire l'un des objets suivants :

- « `S-1-5-21-domain-512` » ;
- « `S-1-5-21-domain-519` » ;
- « `S-1-5-32-544` » ;
- « `S-1-5-18` ».

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
