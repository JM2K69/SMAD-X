# warning_privileged_members_not_empty — Groupes privilégiés non vides

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [warning_privileged_members_not_empty.md](../en/warning_privileged_members_not_empty.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `warning_privileged_members_not_empty` |
| Niveau ANSSI | 3 (1 : le plus critique) |
| Ajouté à la checklist | 2019-01-24 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | PRIV-03 |

## Règles de SMAD-X qui le citent

- **PRIV-03** — Groupe privilégié qui devrait être vide

## Texte officiel

Les groupes privilégiés de la forêt ne sont pas vides ou ne contiennent pas uniquement les comptes d'administration intégrés (RID 500).

*(Tableau des objets concernés, dans le rapport d'analyse.)*

Note : le nombre d'utilisateurs n'inclut pas les comptes d'administration intégrés.

## Recommandation officielle

Il est nécessaire de mettre en place un modèle d'administration permettant de réduire le nombre de comptes privilégiés. Pour cela, il convient de référencer les besoins d'administration de chaque compte et de faire les délégations ad hoc. Par exemple, pour un administrateur ayant besoin de créer et de supprimer des utilisateurs, les délégations nécessaires seront :

- créer un enfant ;
- supprimer un enfant ;
- propriété d'écriture ;
- reset password.

Note : l'« Assistant Délégation de contrôle » de l'Active Directory permet également de déléguer certaines actions d'administration. Bien que plus facile d'utilisation, cet assistant positionne trop de privilèges pour certaines délégations. Il est donc conseillé de privilégier l'utilitaire `ldp`.

Enfin, les règles d'appartenance suivantes doivent être mises en œuvre :

- groupes opératifs : tous vides ;
- groupes réplicateurs : tous vides ;
- groupes d'administration de niveau forêt (entreprise et schéma) : vides et peuplés uniquement lors d'opérations nécessitant ces droits ;
- groupes d'administration de niveau domaine : aucun compte de service.

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
