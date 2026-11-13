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

> #### Contexte : groupes privilégiés
>
> Les groupes privilégiés sont les groupes d'administration et les groupes opératifs ayant tous les droits et privilèges sur la forêt ou pouvant se les attribuer :
>
> - « *Administrateurs* » ;
> - « *Contrôleurs de domaine* »;
> - « *Administrateurs du schéma* » ;
> - « *Administrateurs de l'entreprise* » ;
> - « *Administrateurs du domaine* » : ces administrateurs ont les privilèges de lire la base des secrets de tous les comptes et donc d'extraire les secrets de l'ensemble des comptes privilégiés ;
> - « *Administrateurs de clés* » et « *Administrateurs de clés de l'entreprise* »: ces administrateurs peuvent fixer des valeurs arbitraires aux attributs relatifs à Windows Hello for Business, pour tous les utilisateurs sauf ceux protégés par le mécanisme d'`adminSDHolder`. Si l'authentification basée sur les certificats a été activée, ils peuvent générer un certificat sous leur contrôle, l'assigner à un compte privilégié (par exemple, un contrôleur de domaine), et s'authentifier en tant que lui ;
> - « *Opérateurs de compte* » : ces opérateurs peuvent administrer tous les comptes d'utilisateurs, machines et groupes; à l'exception des comptes protégés par l'`adminSDHolder` ;
> - « *Opérateurs de serveur* » : ces opérateurs peuvent administrer les contrôleurs de domaine, et donc récupérer les secrets de tous les comptes privilégiés ;
> - « *Opérateurs de sauvegarde* » : ces opérateurs peuvent sauvegarder un contrôleur de domaine et donc extraire les secrets de tous les comptes privilégiés depuis cette sauvegarde ;
> - « *Opérateurs d'impression* » : ces opérateurs peuvent charger des pilotes d'impression sur les contrôleurs de domaine et donc charger un pilote malveillant pour par exemple extraire les secrets de tous les comptes privilégiés.
>
> Les groupes « opérateurs » sont vides par défaut et ne devraient contenir aucun membre.

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
