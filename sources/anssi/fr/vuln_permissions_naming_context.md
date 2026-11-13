# vuln_permissions_naming_context — Permissions dangereuses sur la racine des naming contexts (chemins de contrôle)

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_permissions_naming_context.md](../en/vuln_permissions_naming_context.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_permissions_naming_context` |
| Niveaux ANSSI | 1, 2 (1 : le plus critique ; un seuil par niveau, voir le texte) |
| Ajouté à la checklist | 2019-02-22 |
| Dernière modification | 2026-07-15 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | ACL-03 |

## Règles de SMAD-X qui le citent

- **ACL-03** — Permissions dangereuses sur le domaine, AdminSDHolder ou les contrôleurs de domaine

## Texte officiel

> #### Permissions dangereuses et chemins de contrôle
>
> Des permissions dangereuses permettent la prise de contrôle d'un objet cible, sur lequel ces permissions sont positionnées, par l'écriture de propriétés sensibles, par délégation de droits privilégiés ou par l'usage de fonctionnalités spécifiques.
>
> Une fois l'objet maîtrisé par un attaquant, ce dernier peut alors poursuivre un chemin de prise de contrôle d'autres objets qui, par transitivité, peut mener à des élévations de privilèges et à la compromission de l'annuaire Active Directory tout entier. L'enchaînement de permissions dangereuses ainsi exploitées s'appelle un **chemin de contrôle**, ou un chemin d'attaque.
>
> Ainsi, les chemins de contrôle représentent des moyens pour un attaquant d'atteindre des cibles, et permettent d'identifier des déviances dans la gestion du domaine, de valider l'application d'un périmètre de sécurité autour des cibles considérées, mais également de révéler des moyens de persistance laissés par un attaquant après une compromission.

> #### Seuils de tolérance
>
> Ce point de contrôle présente des seuils de tolérance différents en fonction du niveau de sécurité visé :
>
> - au niveau **1** seules les permissions pouvant être exploitées par un attaquant, sans prérequis majeur, sont prises en compte ;
> - au niveau **2** l'ensemble des permissions potentiellement dangereuses sont prises en compte.

Des permissions dangereuses existent sur les racines LDAP de l'Active Directory, appelées *naming contexts*.

Selon le contrôle appliqué sur le *naming context*, un attaquant peut prendre le contrôle complet de l'Active Directory.

*(Tableau des objets concernés, dans le rapport d'analyse.)*

## Recommandation officielle

Un annuaire Active Directory contient plusieurs partitions appelées *naming contexts* :

- `Domain`, contient les informations des objets d'un domaine, comme les ordinateurs, les utilisateurs, les groupes, ... ;
- `Configuration`, contient les éléments de configuration de la forêt ;
- `Schema`, contient la définition de tous les objets ;
- `DomainDns`, contient les zones DNS de niveau domaine ;
- `ForestDns`, contient les zones DNS de niveau forêt.

Il est fortement déconseillé de changer les permissions par défaut de la racine des *naming contexts*. Ainsi il est recommandé d'enlever ces permissions dangereuses. La correction est généralement effectuée à l'aide du composant logiciel enfichable `ADSI Edit` ou de l'utilitaire `ldp`.

Certaines de ces permissions, notamment relatives au service Microsoft Exchange, peuvent faire l'objet de correctifs de sécurité qu'il est nécessaire d'appliquer. C'est notamment le cas lorsque les objets suivants sont concernés :

- Exchange Windows Permissions ;
- Exchange Trusted Subsystem.

- Documentation relative aux permissions Exchange positionnées sur la racine des domaines :[https://support.microsoft.com/en-us/help/4490059/using-shared-permissions-model-to-run-exchange-server](https://support.microsoft.com/en-us/help/4490059/using-shared-permissions-model-to-run-exchange-server)
- Documentation relative aux comptes Entra ID Connect (MSOL) :[https://docs.microsoft.com/en-us/azure/active-directory/hybrid/how-to-connect-configure-ad-ds-connector-account](https://docs.microsoft.com/en-us/azure/active-directory/hybrid/how-to-connect-configure-ad-ds-connector-account) Utiliser la cmdlet `Set-ADSyncRestrictedPermissions` avec `-SkipAdminSdHolders` dans les commandes de configuration.
- Documentation relative aux comptes Entra ID Cloud Sync (ProvAgentgMSA$) :[https://learn.microsoft.com/en-us/entra/identity/hybrid/cloud-sync/how-to-gmsa-cmdlets#using-set-aadcloudsyncrestrictedpermissions](https://learn.microsoft.com/en-us/entra/identity/hybrid/cloud-sync/how-to-gmsa-cmdlets#using-set-aadcloudsyncrestrictedpermissions) Utiliser la cmdlet `Set-AADCloudSyncRestrictedPermissions`.

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

> #### Acceptation du risque
>
> Si des permissions spécifiques sont nécessaires, il convient de considérer que les comptes ou les groupes délégués sont eux-mêmes privilégiés. Ainsi, ces objets doivent être correctement protégés et leurs permissions appliquées doivent être au moins aussi restrictives que celles appliquées à l'objet `adminSDHolder` afin d'éviter de créer de nouveaux chemins de contrôle. Il est donc nécessaire de leur appliquer des permissions "sécurisées". Pour cela, il est recommandé d'utiliser la commande `Set-ADSyncRestrictedPermissions` du module `AdSyncConfig.psm1` fourni avec Azure AD Connect. Cette commande supprime l'héritage et applique des permissions (ACE) restreintes, qui sont toutes également présentes dans un objet `adminSDHolder` par défaut.
>
> Pour un **groupe délégué**, l'application de cette seule commande n'est pas suffisante : il faut également retirer les membres du groupe, ou les considérer eux-mêmes comme étant privilégié en leur appliquant la même commande.
>
> Exemple d'utilisation de `Set-ADSyncRestrictedPermissions` pour appliquer des permissions "sécurisées" sur un compte privilégié :
>
> ```
> Import-Module .\AdSyncConfig.psm1
> $credential = Get-Credential
> Set-ADSyncRestrictedPermissions -ADConnectorAccountDN "CN=ExampleUserAccount,CN=Users,DC=example,DC=ads" -Credential $credential
> ```
>
> Note : le module `AdSyncConfig.psm1` peut être directement extrait du fichier d'installation d'Entra ID Connect à l'aide de l'utilitaire intégré `msiexec` : `msiexec /a "AzureADConnect.msi" /qb TARGETDIR=c:\temp\`
>
> Le paquet d'installation peut être téléchargé à l'adresse suivante : [https://go.microsoft.com/fwlink/?LinkId=615771](https://go.microsoft.com/fwlink/?LinkId=615771).
>
> #### Comptes considérés comme protégés
>
> Un objet est considéré comme protégé "à la `adminSDHolder`" aux conditions cumulatives suivantes :
>
> - L'héritage a été désactivé (flag `SE_DACL_PROTECTED`) ;
> - L'intégralité des ACEs positionnées sur le descripteur de sécurité de l'objet sont soit :
> - Des ACE également présentes à l'identique sur l'objet `adminSDHolder` ;
> - Des ACE bénignes : ACE pour `Self`, pour *Contrôleurs de domaine d'entreprise*, droit étendu "Sent-To" ;
> - Si l'objet est un groupe, alors ses membres doivent également être protégés. Les sous-groupes ne sont pas tolérés.
>
> Si l'objet `adminSDHolder` a été modifié, il faut s'assurer que les conditions ci-dessus sont bien remplies : la commande `Set-ADSyncRestrictedPermissions` ne correspondent alors peut-être plus à l'objet `adminSDHolder`.
>
> Une fois protégés, les comptes apparaissent dans le point d'information "Comptes Tier 0 supplémentaires".

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
