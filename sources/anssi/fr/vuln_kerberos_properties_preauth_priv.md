# vuln_kerberos_properties_preauth_priv — Comptes privilégiés sans préauthentification Kerberos

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_kerberos_properties_preauth_priv.md](../en/vuln_kerberos_properties_preauth_priv.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_kerberos_properties_preauth_priv` |
| Niveau ANSSI | 1 (1 : le plus critique) |
| Ajouté à la checklist | 2018-10-01 |
| Dernière modification | 2019-11-06 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | KRB-03 |

## Règles de SMAD-X qui le citent

- **KRB-03** — Compte privilégié sans préauthentification Kerberos

## Texte officiel

Des comptes privilégiés n'imposent pas la préauthentification Kerberos.

*(Tableau des objets concernés, dans le rapport d'analyse.)*

## Recommandation officielle

La préauthentification permet de s'assurer que l'utilisateur connaît un de ses secrets d'authentification lors d'une demande de TGT (ticket Kerberos obtenu auprès d'un contrôleur de domaine). Sans préauthentification il est possible d'obtenir un ticket chiffré avec un des secrets associés au compte correspondant. Il est ensuite possible de lancer une attaque afin de retrouver le mot de passe de l'utilisateur, ce qui peut être facilité s'il n'est pas assez robuste.

La propriété `DONT_REQUIRE_PREAUTH` doit être supprimée pour ces comptes et le mot de passe doit être changé.

Par défaut, tous les comptes utilisateur imposent la préauthentification car la propriété `DONT_REQUIRE_PREAUTH` n'est pas positionnée. Cette propriété ne doit jamais être positionnée pour les comptes privilégiés du domaine. En cas d'incompatibilité avec une application, celle-ci doit faire l'objet d'une évolution applicative.

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
