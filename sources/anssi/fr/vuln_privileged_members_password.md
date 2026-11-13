# vuln_privileged_members_password — Comptes membres de groupes privilégiés avec une mauvaise politique de mot de passe

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_privileged_members_password.md](../en/vuln_privileged_members_password.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_privileged_members_password` |
| Niveau ANSSI | 2 (1 : le plus critique) |
| Ajouté à la checklist | 2019-06-03 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | PWD-09 |

## Règles de SMAD-X qui le citent

- **PWD-09** — Compte privilégié soumis à une politique de mot de passe faible

## Texte officiel

Une politique de mot de passe faible est imposée à des comptes privilégiés.

*(Tableau des objets concernés, dans le rapport d'analyse.)*

## Recommandation officielle

Il est recommandé d'appliquer une politique de mot de passe pour les comptes privilégiés avec :

- un renouvellement au maximum tous les 3 ans ;
- une longueur d'au moins 8 caractères.

Note importante : il est déconseillé de forcer un renouvellement trop fréquent (quelques mois). Une demande de renouvellement trop fréquente induit statistiquement un nouveau choix de mot de passe plus faible, ce qui en fait une mesure de sécurité contre-productive.

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
