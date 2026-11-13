# vuln_delegation_t4d — Délégation d'authentification non contrainte

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_delegation_t4d.md](../en/vuln_delegation_t4d.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_delegation_t4d` |
| Niveau ANSSI | 1 (1 : le plus critique) |
| Ajouté à la checklist | 2018-09-26 |
| Dernière modification | 2024-04-02 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | DLG-01 |

## Règles de SMAD-X qui le citent

- **DLG-01** — Délégation Kerberos non contrainte

## Texte officiel

Des comptes de la forêt possèdent des délégations d'authentification non contraintes. Cette délégation permet d'usurper l'identité de tout utilisateur s'authentifiant auprès de ces comptes.

*(Tableau des objets concernés, dans le rapport d'analyse.)*

## Recommandation officielle

La délégation d'authentification non contrainte permet à un compte de s'authentifier auprès de n'importe quel service Kerberos avec l'identité d'un utilisateur (s'étant préalablement authentifié auprès de ce compte). Elle n'est donnée par défaut qu'aux contrôleurs de domaine et ne doit pas être accordée à d'autres comptes.

Pour les comptes concernés, il convient de supprimer la propriété `TRUSTED_FOR_DELEGATION` présente dans l'attribut `userAccountControl`. Cette opération peut être effectuée directement à partir de l'onglet « Délégation » de la console de gestion des utilisateurs et ordinateurs. Si une délégation Kerberos doit être mise en œuvre, il faut utiliser la délégation contrainte.

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
