# vuln_guest — Compte Invité actif

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_guest.md](../en/vuln_guest.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_guest` |
| Niveau ANSSI | 2 (1 : le plus critique) |
| Ajouté à la checklist | 2021-12-02 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | ACC-01 |

## Règles de SMAD-X qui le citent

- **ACC-01** — Compte Invité actif

## Texte officiel

Le compte *Invité* est activé.

Le compte *Invité* permet aux utilisateurs réseau non authentifiés de se connecter en tant qu'*Invité* sans mot de passe. Ces utilisateurs non autorisés peuvent accéder à toutes les ressources accessibles au compte *Invité* sur le réseau. Cette fonctionnalité signifie que tous les objets ou dossiers partagés avec des autorisations qui autorisent l'accès au compte *Invité*, au groupe *Invités du domaine*, au groupe *Invités*, ou au groupe *Tout le monde* sont accessibles sur le réseau, ce qui peut entraîner l'exposition ou l'altération des données.

*(Tableau des objets concernés, dans le rapport d'analyse.)*

## Recommandation officielle

Désactiver le compte *Invité*.

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
