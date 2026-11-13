# vuln_delegation_a2d2 — Délégation d'authentification contrainte vers un service privilégié

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_delegation_a2d2.md](../en/vuln_delegation_a2d2.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_delegation_a2d2` |
| Niveau ANSSI | 1 (1 : le plus critique) |
| Ajouté à la checklist | 2018-10-01 |
| Dernière modification | 2021-05-31 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | DLG-03 |

## Règles de SMAD-X qui le citent

- **DLG-03** — Délégation contrainte vers un contrôleur de domaine

## Texte officiel

Des comptes possèdent des délégations d'authentification contraintes vers un service privilégié de l'AD. Elles permettent au compte d'élever ses privilèges auprès d'un service privilégié.

*(Tableau des objets concernés, dans le rapport d'analyse.)*

## Recommandation officielle

La délégation d'authentification contrainte permet à un compte de s'authentifier auprès d'un service Kerberos avec l'identité d'un autre utilisateur (s'étant préalablement authentifié auprès de ce compte). Si cette délégation est autorisée vers une ressource privilégiée (notamment un service présent sur un contrôleur de domaine), elle permet au compte d'élever ses privilèges et de compromettre la forêt.

La délégation d'authentification contrainte vers un service privilégié ne doit pas être utilisée.

Pour les comptes concernés, il faut supprimer dans l'attribut `msDS-AllowedToDelegateTo` tous les *Service Principal Name* (SPN) référençant des services portés par des objets privilégiés. Cette opération peut être effectuée directement à partir de l'onglet « Délégation » de la console de gestion des utilisateurs et ordinateurs.

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
