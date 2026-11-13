# warning_admincount — Comptes ayant la propriété adminCount

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [warning_admincount.md](../en/warning_admincount.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `warning_admincount` |
| Niveaux ANSSI | 1, 3 (1 : le plus critique ; un seuil par niveau, voir le texte) |
| Ajouté à la checklist | 2018-09-27 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | PRIV-02 |

## Règles de SMAD-X qui le citent

- **PRIV-02** — adminCount orphelin

## Texte officiel

Plusieurs comptes actifs de la forêt ont la propriété `adminCount` positionnée, alors qu'ils n'appartiennent plus à des groupes privilégiés. Cette situation peut résulter d'une mauvaise pratique d'administration consistant à mettre temporairement des utilisateurs dans les groupes d'administration les plus privilégiés de l'Active Directory.

Note :

- au niveau **1**, ce point d'attention indique uniquement les comptes non membres des groupes privilégiés possédant la propriété `adminCount` positionnée à 1 ;
- au niveau **3**, ce point d'attention indique l'ensemble des comptes ayant la propriété `adminCount` positionnée à 0 (signifiant que le risque a été accepté ou traduisant une mauvaise pratique de remise à 0 non documentée).

*(Tableau des objets concernés, dans le rapport d'analyse.)*

Les comptes ayant l'héritage de la DACL Désactivé présentent le risque de ne plus se soumettre au modèle de délégation des permissions de la forêt Active Directory, bien qu'ils ne soient potentiellement plus considérés comme des comptes exceptionnels du fait de privilèges élevés.

## Recommandation officielle

Les opérations suivantes sont effectuées sur un compte lorsqu'il rejoint un groupe privilégié :

- la propriété `adminCount` est positionnée à 1 ;
- l'héritage des permissions est désactivé ;
- les permissions sont périodiquement recopiées de l'objet `adminSDHolder` (mécanisme `SDProp`).

Lorsqu'un compte quitte tous les groupes privilégiés, l'attribut `adminCount` reste positionné à 1 et l'héritage des permissions reste désactivé. Il en résulte un état non maîtrisé appelé *orphaned `adminSDHolder`*. La propriété `adminCount`, lorsqu'elle a la valeur 1, traduit donc le fait qu'un compte (utilisateur ou machine) a fait partie, à un moment donné, d'un des groupes d'administration les plus privilégiés de l'Active Directory.

En dépit du retrait des groupes privilégiés, il n'est pas possible de garantir pour le compte la perte effective de tous ses droits et ses privilèges acquis. Par exemple, l'utilisateur peut encore être propriétaire d'objets de l'Active Directory (GPO, ordinateurs, utilisateurs, etc.). Le traitement de ces comptes doit être fait en deux étapes :

- déterminer et documenter la raison du positionnement de l'`adminCount` à 1 et, le cas échéant, bannir les pratiques d'administration consistant à mettre temporairement des comptes dans les groupes d'administration les plus privilégiés de l'Active Directory ;
- déterminer (si possible) les actions effectuées avec ces comptes.

#### Cas 1 : suppression du risque

Si le risque n'est pas accepté, il convient de neutraliser le compte. Cette méthode de neutralisation apporte les mêmes avantages que la suppression tout en conservant la traçabilité offerte par la simple désactivation. Pour neutraliser un compte, il faut :

- désactiver le compte ;
- le retirer de tous ses groupes d'appartenance ;
- changer son mot de passe de façon aléatoire. Pour garantir un mot de passe aléatoire, il est recommandé de sélectionner l'option « Une carte à puce est nécessaire pour ouvrir une session interactive » dans les propriétés du compte. Cela revient à positionner l'attribut `ADS_UF_SMARTCARD_REQUIRED` dans la propriété `userAccountControl` ;
- éventuellement, déplacer le compte dans une OU dédiée ;
- éventuellement, mettre à jour le champ description du compte afin d'indiquer le contexte de désactivation (date, raison, etc.).

#### Cas 2 : acceptation du risque

Dans le cas d'une simple erreur de manipulation (par exemple, un administrateur ayant mis par inadvertance un utilisateur dans un groupe privilégié durant un laps de temps court), il est possible d'accepter le risque. Dans ce cas, il convient de positionner l'attribut `adminCount` à 0. Afin de faciliter d'éventuelles futures investigations, cette action doit être documentée et une liste des comptes ainsi traités doit être maintenue.

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
