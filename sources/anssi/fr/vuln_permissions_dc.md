# vuln_permissions_dc — Permissions dangereuses sur les objets contrôleurs de domaine (chemins de contrôle)

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_permissions_dc.md](../en/vuln_permissions_dc.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_permissions_dc` |
| Niveaux ANSSI | 1, 2 (1 : le plus critique ; un seuil par niveau, voir le texte) |
| Ajouté à la checklist | 2019-02-22 |
| Dernière modification | 2023-11-08 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | ACL-03 |

## Règles de SMAD-X qui le citent

- **ACL-03** — Permissions dangereuses sur le domaine, AdminSDHolder ou les contrôleurs de domaine

## Recommandation officielle

Il convient de considérer les comptes ayant des permissions sur les contrôleurs de domaine comme étant privilégiés. Il est alors nécessaire de les protéger avec le mécanisme de l'`adminSDHolder`. Pour cela, ces comptes doivent appartenir au groupe `S-1-5-21-domain-519` ou `S-1-5-21-domain-512`.

Dans le cadre de la mise en place de forêts d'administration, il est toléré d'ajouter des droits sur les contrôleurs de domaine aux conditions suivantes :

- le groupe est de type `DOMAIN LOCAL` ;
- les permissions sur ce groupe sont plus restrictives que celles de l'`adminSDHolder`.

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
