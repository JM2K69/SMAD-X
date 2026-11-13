# vuln_silo_priv — Membres des groupes privilégiés hors silo d'authentification

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_silo_priv.md](../en/vuln_silo_priv.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_silo_priv` |
| Niveau ANSSI | 4 (1 : le plus critique) |
| Ajouté à la checklist | 2020-07-03 |
| Dernière modification | 2025-01-23 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | TIER-04 · TIER-09 |

## Règles de SMAD-X qui le citent

- **TIER-04** — Compte privilégié connecté sur une machine moins sensible
- **TIER-09** — Compte privilégié hors d'un silo d'authentification appliqué

## Texte officiel

Des membres de groupes privilégiés ne sont pas membres d'un silo d'authentification, ou celui-ci n'est pas correctement configuré. Le silo de stratégies d'authentification doit être appliqué (`msDS-AuthNPolicySiloEnforced` doit être positionné à TRUE). Les membres doivent être également affectés au silo, en cochant la case "Affecter un silo de stratégies d'authentification", ce qui positionne l'attribut LDAP `msDS-AssignedAuthNPolicySilo` visible dans les données associées au point de contrôle "Membres des groupes privilégiés".

*(Tableau des objets concernés, dans le rapport d'analyse.)*

## Recommandation officielle

Tous les utilisateurs privilégiés doivent être membres d'un silo d'authentification correctement configuré.

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
