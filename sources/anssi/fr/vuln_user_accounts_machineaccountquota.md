# vuln_user_accounts_machineaccountquota — Ajout de machines au domaine non limité

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_user_accounts_machineaccountquota.md](../en/vuln_user_accounts_machineaccountquota.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_user_accounts_machineaccountquota` |
| Niveau ANSSI | 4 (1 : le plus critique) |
| Ajouté à la checklist | 2022-07-29 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | ACC-03 |

## Règles de SMAD-X qui le citent

- **ACC-03** — Tout utilisateur peut joindre des ordinateurs au domaine

## Texte officiel

Par défaut les utilisateurs authentifiés peuvent joindre 10 machines au domaine. Cette valeur est contrôlée par l'attribut `ms-DS-MachineAccountQuota` de la racine du domaine.

Les comptes machine ajoutés ont alors pour propriétaire le compte utilisé, lui offrant un contrôle total dessus. La vulnérabilité CVE-2021-42287 utilisait des machines jointes pour réaliser une élévation de privilèges.

*(Tableau des objets concernés, dans le rapport d'analyse.)*

## Recommandation officielle

Positionner l'attribut `ms-DS-MachineAccountQuota` à 0 à la racine des domaines concernés. La correction est généralement effectuée à l'aide des consoles `dsa.msc`, `adsiedit.msc` ou de l'utilitaire `ldp`.

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
