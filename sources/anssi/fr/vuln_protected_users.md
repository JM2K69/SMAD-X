# vuln_protected_users — Comptes privilégiés non membres du groupe Protected Users

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_protected_users.md](../en/vuln_protected_users.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_protected_users` |
| Niveau ANSSI | 3 (1 : le plus critique) |
| Ajouté à la checklist | 2019-06-11 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | DLG-04 |

## Règles de SMAD-X qui le citent

- **DLG-04** — Compte privilégié non protégé contre la délégation

## Texte officiel

Des comptes privilégiés ne sont pas protégés par le groupe *Protected Users*.

*(Tableau des objets concernés, dans le rapport d'analyse.)*

## Recommandation officielle

L'ensemble des comptes privilégiés doivent être ajoutés au groupe *Protected Users* afin de :

- forcer l'authentification en Kerberos ;
- réduire la durée de vie des tickets Kerberos ;
- forcer l'utilisation d'algorithmes de chiffrement forts (AES) ;
- interdire la mise en cache du mot de passe sur les postes de travail ;
- interdire toute forme de délégation.

**Attention** : l'utilisation du groupe *Protected Users* a des impacts fonctionnels importants. Référence : [https://docs.microsoft.com/en-us/windows-server/security/credentials-protection-and-management/protected-users-security-group](https://docs.microsoft.com/en-us/windows-server/security/credentials-protection-and-management/protected-users-security-group).

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
