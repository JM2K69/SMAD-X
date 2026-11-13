# vuln_permissions_adminsdholder — Permissions dangereuses sur l'objet adminSDHolder

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_permissions_adminsdholder.md](../en/vuln_permissions_adminsdholder.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_permissions_adminsdholder` |
| Niveaux ANSSI | 1, 2 (1 : le plus critique ; un seuil par niveau, voir le texte) |
| Ajouté à la checklist | 2019-02-22 |
| Dernière modification | 2026-07-15 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | ACL-03 |

## Règles de SMAD-X qui le citent

- **ACL-03** — Permissions dangereuses sur le domaine, AdminSDHolder ou les contrôleurs de domaine

## Recommandation officielle

#### Cas général

Les permissions de l'objet `adminSDHolder` sont régulièrement appliquées sur l'ensemble des objets protégés (membres des groupes administratifs et opératifs) de l'Active Directory. Par défaut, seuls les objets privilégiés possèdent des droits sur l'objet `adminSDHolder`. Ainsi, ce mécanisme permet de protéger les utilisateurs et les groupes les plus privilégiés de l'Active Directory.

Il est fortement déconseillé de changer les permissions par défaut de cet objet. Ainsi il est recommandé d'enlever les permissions dangereuses sur l'objet `adminSDHolder` afin de revenir à un état par défaut. La correction est généralement effectuée à l'aide du composant logiciel enfichable `ADSI Edit` ou de l'utilitaire `ldp`.

#### Cas particuliers

Dans le cadre de la mise en œuvre de forêts d'administration, il est toléré d'ajouter des droits à un groupe sur l'`adminSDHolder` aux conditions suivantes :

- les permissions sur ce groupe sont plus restrictives que celles appliquées initialement par l'objet `adminSDHolder` ;
- l'héritage des permissions sur ce groupe est désactivé.

Si une permission est liée à Exchange (par exemple ), il est nécessaire d'appliquer les mises à jour Exchange ad hoc. La documentation relative aux permissions Exchange positionnées sur l'objet `adminSDHolder` est accessible à cette URL :

- [https://support.microsoft.com/en-us/help/4490059/using-shared-permissions-model-to-run-exchange-server](https://support.microsoft.com/en-us/help/4490059/using-shared-permissions-model-to-run-exchange-server) Information : le contrôle depuis `Exchange Trusted Subsystem` avec  est un défaut de configuration qui a été corrigé par Microsoft.

Si les permissions concernent un compte connecteur Entra ID Connect (MSOL), il est nécessaire de reconfigurer les droits dans l'AD relatifs au compte MSOL spécifié. La documentation relative aux comptes Enta ID Connect (MSOL) est accessible à cette URL :

- [https://docs.microsoft.com/en-us/azure/active-directory/hybrid/how-to-connect-configure-ad-ds-connector-account](https://docs.microsoft.com/en-us/azure/active-directory/hybrid/how-to-connect-configure-ad-ds-connector-account) Utiliser la cmdlet `Set-ADSyncRestrictedPermissions` avec `-SkipAdminSdHolders` dans les commandes de configuration.

Si les permissions concernent un compte d'agent de provisionnement Entra ID Cloud Sync (ProvAgentgMSA$), il est nécessaire de reconfigurer les droits dans l'AD relatifs au compte ProvAgentgMSA$ spécifié. La documentation relative aux comptes Enta ID Cloud Sync (ProvAgentgMSA$) est accessible à cette URL :

- [https://learn.microsoft.com/en-us/entra/identity/hybrid/cloud-sync/how-to-gmsa-cmdlets#using-set-aadcloudsyncrestrictedpermissions](https://learn.microsoft.com/en-us/entra/identity/hybrid/cloud-sync/how-to-gmsa-cmdlets#using-set-aadcloudsyncrestrictedpermissions) Utiliser la cmdlet `Set-AADCloudSyncRestrictedPermissions`.

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
