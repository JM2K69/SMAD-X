# vuln_privileged_members_perm — Permissions dangereuses sur des membres de groupes privilégiés (chemins de contrôle)

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_privileged_members_perm.md](../en/vuln_privileged_members_perm.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_privileged_members_perm` |
| Niveaux ANSSI | 1, 2 (1 : le plus critique ; un seuil par niveau, voir le texte) |
| Ajouté à la checklist | 2019-01-11 |
| Dernière modification | 2023-11-08 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | ACL-02 · TIER-03 |

## Règles de SMAD-X qui le citent

- **ACL-02** — Chemin de contrôle vers le domaine par les permissions
- **TIER-03** — Violation du cloisonnement : délégation à un tier moins sensible

## Texte officiel

Ce point de contrôle présente des seuils de tolérance différents en fonction du niveau de sécurité visé : au niveau **1** seules les permissions pouvant être exploitées par un attaquant, sans prérequis majeur, sont prises en compte ; au niveau **2** l'ensemble des permissions potentiellement dangereuses sont prises en compte.

Des permissions dangereuses existent sur des membres de groupes privilégiés. Ces chemins de contrôle correspondent à des droits ou privilèges permettant à des utilisateurs non privilégiés de prendre le contrôle de comptes privilégiés.

*(Tableau des objets concernés, dans le rapport d'analyse.)*

## Recommandation officielle

La plupart des permissions dangereuses citées dans cette vulnérabilité sont également liées aux vulnérabilités « Permissions dangereuses sur l'objet adminSDHolder » et « Permissions dangereuses sur les objets des contrôleurs de domaine ». L'application des recommandations associées à ces vulnérabilités corrigera une majorité de ces chemins.

Pour les permissions restantes, il est recommandé d'enlever les entrées dangereuses. La correction est généralement effectuée à l'aide du composant logiciel enfichable `ADSI Edit` ou de l'utilitaire `ldp`.

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
