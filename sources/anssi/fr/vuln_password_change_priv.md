# vuln_password_change_priv — Comptes privilégiés dont le mot de passe est inchangé depuis plus de 3 ans

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_password_change_priv.md](../en/vuln_password_change_priv.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_password_change_priv` |
| Niveau ANSSI | 1 (1 : le plus critique) |
| Ajouté à la checklist | 2018-10-01 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | PWD-04 |

## Règles de SMAD-X qui le citent

- **PWD-04** — Mot de passe d'un compte privilégié inchangé depuis plus de 3 ans

## Texte officiel

La date de dernier changement du mot de passe de comptes privilégiés est supérieure à 3 ans.

*(Tableau des objets concernés, dans le rapport d'analyse.)*

## Recommandation officielle

#### Cas général

Afin de limiter l'impact en cas de vol d'un mot de passe, il est nécessaire de mettre en œuvre une politique de renouvellement des mots de passe pour les comptes privilégiés. La durée de ce renouvellement doit être inférieure à 3 ans.

Le compte « Administrateur intégré » (RID 500, utilisé en tant que compte *bris de glace* ne devant jamais être utilisé sauf en de rares exceptions) doit également faire l'objet d'un changement de mot de passe tous les 3 ans. En effet il est important de s'assurer régulièrement que celui-ci est toujours accessible et que les procédures associées à son utilisation sont toujours correctes.

Note importante : il est déconseillé de forcer ce renouvellement trop fréquemment (quelques mois). Une demande de renouvellement trop fréquente induit statistiquement un nouveau choix de mot de passe plus faible, ce qui en fait une mesure de sécurité contre-productive.

#### Cas des comptes de service

S'il s'agit d'un compte de service où le changement de mot de passe n'est pas possible, il convient de :

- le retirer de ces groupes ;
- lui conférer les délégations nécessaires à son fonctionnement
- étudier la possibilité de recourir aux *Managed Service Accounts* (sMSA, gMSA).

#### Cas des comptes non utilisés

Il est nécessaire de neutraliser les comptes qui ne sont plus utilisés. Cette méthode de neutralisation apporte les mêmes avantages que la suppression tout en conservant la traçabilité offerte par la simple désactivation. Pour neutraliser un compte, il faut :

- désactiver le compte ;
- le retirer de tous ses groupes ;
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
