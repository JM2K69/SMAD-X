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

Si une permission est liée à Exchange (par exemple `WRITE_SPN`), il est nécessaire d'appliquer les mises à jour Exchange ad hoc. La documentation relative aux permissions Exchange positionnées sur l'objet `adminSDHolder` est accessible à cette URL :

- [https://support.microsoft.com/en-us/help/4490059/using-shared-permissions-model-to-run-exchange-server](https://support.microsoft.com/en-us/help/4490059/using-shared-permissions-model-to-run-exchange-server) Information : le contrôle depuis `Exchange Trusted Subsystem` avec `WRITE_ALT_IDENTITY` est un défaut de configuration qui a été corrigé par Microsoft.

Si les permissions concernent un compte connecteur Entra ID Connect (MSOL), il est nécessaire de reconfigurer les droits dans l'AD relatifs au compte MSOL spécifié. La documentation relative aux comptes Enta ID Connect (MSOL) est accessible à cette URL :

- [https://docs.microsoft.com/en-us/azure/active-directory/hybrid/how-to-connect-configure-ad-ds-connector-account](https://docs.microsoft.com/en-us/azure/active-directory/hybrid/how-to-connect-configure-ad-ds-connector-account) Utiliser la cmdlet `Set-ADSyncRestrictedPermissions` avec `-SkipAdminSdHolders` dans les commandes de configuration.

Si les permissions concernent un compte d'agent de provisionnement Entra ID Cloud Sync (ProvAgentgMSA$), il est nécessaire de reconfigurer les droits dans l'AD relatifs au compte ProvAgentgMSA$ spécifié. La documentation relative aux comptes Enta ID Cloud Sync (ProvAgentgMSA$) est accessible à cette URL :

- [https://learn.microsoft.com/en-us/entra/identity/hybrid/cloud-sync/how-to-gmsa-cmdlets#using-set-aadcloudsyncrestrictedpermissions](https://learn.microsoft.com/en-us/entra/identity/hybrid/cloud-sync/how-to-gmsa-cmdlets#using-set-aadcloudsyncrestrictedpermissions) Utiliser la cmdlet `Set-AADCloudSyncRestrictedPermissions`.

> #### Contexte : chemins de contrôle
>
> Un chemin de contrôle est composé d'un ensemble de relations de contrôle direct, où chaque relation traduit la maîtrise d'un objet sur un autre au travers d'une propriété particulière. Ainsi, les chemins de contrôle représentent des moyens pour un attaquant d'atteindre des cibles, et permettent d'identifier des déviances dans la gestion du domaine, de valider l'application d'un périmètre de sécurité autour des cibles considérées, mais également de révéler des moyens de persistance laissés par un attaquant après une compromission.
>
> #### Limites
>
> Certains chemins de contrôle peuvent disposer de correctifs qui les rendent non exploitables. Ces cas imposent généralement d'avoir l'ensemble des contrôleurs de domaine au même niveau de mise à jour. De plus, les fonctionnalités dangereuses peuvent parfois être réactivées par un paramètre de registre ou dsHeuristics. Il est donc toujours recommandé de nettoyer les permissions concernées de l'Active Directory pour prévenir une réapparition du problème.
>
> Liste des correctifs spécifiques connus :
>
> - [KB5008383](https://support.microsoft.com/fr-fr/topic/kb5008383-active-directory-permissions-updates-cve-2021-42291-536d5555-ffba-4248-a60e-d6cbc849cde1) corrige les chemins de contrôle de type `OWNER` et `WRITE_OWNER` sur des comptes machines uniquement, mais le comportement dangereux peut être réactivé.
> - Divers chemins de contrôle relatifs à Exchange Server ont été corrigés par des mises à jour cumulatives spécifiques ; se référer à l'encart dédié si pertinent.
>
> #### Cas particulier de Microsoft Exchange Server
>
> Certains chemins de contrôle qui apparaissent peuvent être liés à des groupes Exchange bénéficiant de permissions dangereuses. Deux cas particuliers peuvent se présenter :
>
> - s'il s'agit de groupes datant d'une ancienne installation de Exchange Server 2007 ou antérieure ("Exchange Enterprise Servers", "Exchange Domain Servers" ou "Exchange Services") et que cette infrastructure a été migrée vers une version 2010 ou ultérieure, il est recommandé de supprimer ces groupes et les permissions associées. [Microsoft fournit une procédure à cette fin](https://learn.microsoft.com/fr-fr/previous-versions/office/exchange-server-2007/bb288905(v=exchg.80)?redirectedfrom=MSDN) ;
> - sinon pour les autres groupes, il convient de vérifier les contrôles dangereux concernés pour déterminer la correction adéquate (voir plus bas).
>
> Les contrôles suivants, uniquement s'ils sont mentionnés pour un groupe lié à Exchange, ont fait l'objet d'un correctif de Microsoft :
>
> - `WRITE_SPN`
> - `WRITE_DAC`
> - `WRITE_OWNER`
> - `WRITE_MANAGED_BY`
> - `WRITE_ALT_IDENTITY`
>
> Attention : certaines des propriétés LDAP concernées par ces écritures (`servicePrincipalName` et `altSecurityIdentities`) font partie du *property-set* `Public-Information`. Ainsi, les permissions d'écritures peuvent être attribuées pour chaque attribut unitairement ou le *property-set* entier, auquel cas seul ce dernier sera visible dans la DACL de l'objet concerné.
>
> Pour appliquer ce correctif, il est nécessaire d'effectuer les actions suivantes :
>
> - pour Exchange Server 2019 : installer le SU de mai 2022 sur le CU12 ([KB5014261](https://support.microsoft.com/fr-fr/topic/description-of-the-security-update-for-microsoft-exchange-server-2016-and-2019-may-10-2022-kb5014261-cd5ecb59-a0eb-47ef-ae35-f62b13c8b817)) ;
> - pour Exchange Server 2016 : installer le SU de mai 2022 sur le CU23 ([KB5014261](https://support.microsoft.com/fr-fr/topic/description-of-the-security-update-for-microsoft-exchange-server-2016-and-2019-may-10-2022-kb5014261-cd5ecb59-a0eb-47ef-ae35-f62b13c8b817)) ;
> - pour Exchange Server 2013 : installer le SU de mai 2022 sur le CU23 ([KB5014260](https://support.microsoft.com/fr-fr/topic/description-of-the-security-update-for-microsoft-exchange-server-2013-may-10-2022-kb5014260-701fda2b-7417-4ffa-979b-0b2bc8e93b1c)) ;
> - pour toutes les versions, après installation des correctifs précédents : exécuter la phase de préparation des domaines "/PrepareAllDomains"
> - pour Exchange Server 2019 et 2016 : `Setup.exe /IAcceptExchangeServerLicenseTerms_DiagnosticDataON /PrepareAllDomains` ou `Setup.exe /IAcceptExchangeServerLicenseTerms_DiagnosticDataOFF /PrepareAllDomains` depuis le répertoire d'installation de Exchange ;
> - pour Exchange Server 2013 : `Setup.exe /IAcceptExchangeServerLicenseTerms /PrepareAllDomains` ;
> - il est rappelé que Exchange Server 2010 n'est plus supporté par l'éditeur.
>
> Pour tout autre chemin de contrôle, une qualification manuelle doit être effectuée pour caractériser le problème.
>
> À titre d'information, la liste des derniers correctifs publiés pour Exchange Server est [disponible ici](https://learn.microsoft.com/fr-fr/exchange/new-features/build-numbers-and-release-dates).

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
