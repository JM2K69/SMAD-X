# vuln_dont_expire_priv — Comptes privilégiés dont le mot de passe n'expire jamais

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_dont_expire_priv.md](../en/vuln_dont_expire_priv.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_dont_expire_priv` |
| Niveau ANSSI | 1 (1 : le plus critique) |
| Ajouté à la checklist | 2018-10-12 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | PWD-01 |

## Règles de SMAD-X qui le citent

- **PWD-01** — Compte privilégié dont le mot de passe n'expire jamais

## Texte officiel

Certains comptes privilégiés possèdent des mots de passe n'expirant pas.

Si aucun mécanisme de sécurité ne force le changement de ces mots de passe, la récupération d'un compte privilégié permet à un individu malveillant de conserver ces droits d'accès au domaine sur le long terme.

*(Tableau des objets concernés, dans le rapport d'analyse.)*

## Recommandation officielle

#### Cas général

Il est recommandé de changer régulièrement les mots de passe des membres des groupes privilégiés (au maximum tous les 3 ans). Afin que la politique de mot de passe du domaine soit imposée sur ces comptes, la propriété `DONT_EXPIRE` ne doit pas être positionnée. Il convient donc de supprimer cette propriété pour ces comptes et de renouveler régulièrement leur mot de passe dès la suppression de la propriété. Cette action peut être effectuée en décochant la case « Le mot de passe n'expire jamais » dans les propriétés du compte de l'utilisateur (onglet compte).

Note importante : il est déconseillé de forcer ce renouvellement trop fréquemment (quelques mois). Une demande de renouvellement trop fréquente induit statistiquement un nouveau choix de mot de passe plus faible, ce qui en fait une mesure de sécurité contre-productive. La fréquence de changement doit être adaptée en fonction du contexte d'emploi du compte. La politique de mot de passe imposée par le domaine peut être soit globale, soit affinée via le mécanisme des Password Settings Object (PSO).

Lorsque la propriété `DONT_EXPIRE` n'est pas positionnée sur un compte donné, l'annuaire Active Directory construit, pour ce compte, un attribut `msDS-UserPasswordExpiryTimeComputed`. Cet attribut contient la date d'expiration du mot de passe, qui peut servir pour anticiper l'expiration des mots de passe, en particulier pour les comptes de service.

#### Cas particuliers

Si la propriété `DONT_EXPIRE` a été positionnée car le changement de mot de passe de ces comptes n'est pas possible facilement, cela peut signifier que ces comptes ne sont pas utilisés pour des opérations d'administration (par exemple, des comptes de service). Ces comptes ne doivent en aucun cas être membres d'un groupe privilégié. Il convient de :

- les retirer de ces groupes ;
- leur conférer les délégations nécessaires à leur fonctionnement ;
- documenter une procédure de changement de mot de passe spécifique ;
- enlever la propriété `DONT_EXPIRE`.

#### Cas des comptes non utilisés

Il est nécessaire de neutraliser les comptes qui ne sont plus utilisés. La méthode suivante de neutralisation apporte les mêmes avantages que la suppression tout en conservant la traçabilité offerte par la simple désactivation. Pour neutraliser un compte, il faut :

- désactiver le compte ;
- le retirer de tous ses groupes d'appartenance ;
- changer son mot de passe de façon aléatoire. Pour garantir un mot de passe aléatoire, il est recommandé de sélectionner l’option « Une carte à puce est nécessaire pour ouvrir une session interactive » dans les propriétés du compte. Cela revient à positionner l'attribut `ADS_UF_SMARTCARD_REQUIRED` dans la propriété `userAccountControl` ;
- éventuellement, déplacer le compte dans une OU dédiée ;
- éventuellement, mettre à jour le champ description du compte afin d'indiquer le contexte de désactivation (date, raison, etc.).

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
