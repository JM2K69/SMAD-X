# vuln_spn_priv — Comptes privilégiés avec SPN

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_spn_priv.md](../en/vuln_spn_priv.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_spn_priv` |
| Niveau ANSSI | 1 (1 : le plus critique) |
| Ajouté à la checklist | 2018-09-27 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | KRB-01 |

## Règles de SMAD-X qui le citent

- **KRB-01** — Compte privilégié vulnérable au Kerberoasting

## Texte officiel

L'attribut `servicePrincipalName` (SPN) est positionné pour plusieurs comptes privilégiés.

Il est possible de lancer une attaque afin de retrouver le mot de passe de l'utilisateur, ce qui peut être facilité si celui-ci n'est pas assez robuste.

*(Tableau des objets concernés, dans le rapport d'analyse.)*

## Recommandation officielle

Le SPN permet d'associer des noms de service Kerberos à des comptes. Lorsqu'un compte possède un nom de service Kerberos, n'importe quel utilisateur authentifié peut demander un ticket pour ce service. Dans ce cas, le ticket émis est chiffré avec une des clés Kerberos associées au compte correspondant, ce qui permet de faire une attaque par *brute force*. Par défaut seuls les comptes machine possèdent des SPN, cet attribut doit toujours être vide pour les comptes privilégiés du domaine.

#### Cas général: supprimer le SPN

Si les comptes ayant un SPN ont réellement besoin d'être privilégiés, il convient de supprimer leur attribut `servicePrincipalName` et de changer leur mot de passe.

Note : il est toléré qu'un compte privilégié possède un SPN aux conditions suivantes :

- le compte est un *Managed Service Account* (`sMSA` / `gMSA`) ;
- **OU**
  - une politique de mot de passe affinée (*Password Settings Object* ou PSO) s'applique sur le compte avec une longueur de mot de passe supérieure à **32** ;
  - une politique de mot de passe affinée (*Password Settings Object* ou PSO) s'applique sur le compte avec une durée d'expiration maximale de **3 ans** ;
  - le mot de passe du compte a changé depuis la dernière modification du PSO ;
- le compte ne peut utiliser que les algorithmes AES.

Les politiques de mot de passe affinées (*Password Settings Object* ou PSO) ne peuvent être prises en compte que si elles ont pu être collectées par ORADAD. En cas d'échec de collecte, un point d'attention de niveau 3 "Politiques de mot de passe affinées (PSO) manquantes dans la collecte" apparait ; il convient alors de suivre la recommandation associée.

#### Cas particulier: retirer le compte des groupes privilégiés

Si les comptes ayant un SPN n'ont pas réellement besoin d'être membres d'un groupe privilégié de l'Active Directory, il est préférable de les retirer de ces groupes et de faire les délégations nécessaires.

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
