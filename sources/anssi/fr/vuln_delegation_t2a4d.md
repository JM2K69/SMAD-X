# vuln_delegation_t2a4d — Délégation d'authentification contrainte avec transition de protocole vers un service privilégié

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_delegation_t2a4d.md](../en/vuln_delegation_t2a4d.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_delegation_t2a4d` |
| Niveau ANSSI | 1 (1 : le plus critique) |
| Ajouté à la checklist | 2018-10-01 |
| Dernière modification | 2021-05-31 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | DLG-02 |

## Règles de SMAD-X qui le citent

- **DLG-02** — Délégation contrainte avec transition de protocole vers un contrôleur de domaine

## Texte officiel

Des comptes possèdent des délégations d'authentification contraintes avec transition de protocole vers un service privilégié. Elles permettent au compte d'élever ses privilèges. Les services privilégiés considérés sont :

- les services des contrôleurs de domaine ;
- le service `krbtgt`.

*(Tableau des objets concernés, dans le rapport d'analyse.)*

## Recommandation officielle

La délégation d'authentification contrainte avec transition de protocole permet à un compte de s'authentifier auprès d'un service Kerberos avec l'identité d'un autre utilisateur. Si cette délégation est autorisée vers une ressource privilégiée (notamment un service présent sur un contrôleur de domaine), elle permet au compte d'élever ses privilèges et de compromettre la forêt.

La délégation d'authentification contrainte avec transition de protocole vers un service privilégié ne doit pas être utilisée.

Pour les comptes concernés, il faut supprimer dans l'attribut `msDS-AllowedToDelegateTo` tous les *Service Principal Name* (SPN) référençant les contrôleurs de domaine. Cette opération peut être effectuée directement à partir de l'onglet « Délégation » de la console de gestion des utilisateurs et ordinateurs.

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
