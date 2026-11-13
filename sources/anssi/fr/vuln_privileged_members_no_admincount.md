# vuln_privileged_members_no_admincount — Comptes membres de groupes privilégiés ayant un attribut adminCount vide ou valant 0

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_privileged_members_no_admincount.md](../en/vuln_privileged_members_no_admincount.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_privileged_members_no_admincount` |
| Niveau ANSSI | 2 (1 : le plus critique) |
| Ajouté à la checklist | 2020-01-06 |
| Dernière modification | 2024-02-14 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | PRIV-01 |

## Règles de SMAD-X qui le citent

- **PRIV-01** — Membre d'un groupe privilégié sans adminCount

## Texte officiel

Lorsqu'un compte devient membre d'un groupe privilégié, son descripteur de sécurité est repris de celui de l'`adminSDHolder` et son attribut `adminCount` est positionné à 1. Par la suite le contrôleur de domaine ayant le rôle PDCe vérifie de manière régulière le descripteur de sécurité des comptes privilégiés.

La présence d'un attribut `adminCount` vide ou valant 0 pour un compte membre d'un groupe privilégié révèle donc d'une modification explicite de cet attribut. Ceci peut témoigner d'une volonté de mettre en défaut l'analyse d'outils de sécurité.

Des groupes privilégiés de la forêt contiennent des comptes ayant un attribut `adminCount` vide ou valant 0 :

*(Tableau des objets concernés, dans le rapport d'analyse.)*

Remarque : les groupes "Contrôleurs de domaine" et "Contrôleurs de domaine en lecture seule" forment une exception. Ils sont protégés par l'`adminSDHolder`, mais leurs membres ne le sont pas.

## Recommandation officielle

Il est recommandé d'identifier la raison pour laquelle l'attribut `adminCount` des comptes listés a été modifié.

Par la suite, il est nécessaire de repositionner cet attribut à la valeur `1`.

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
