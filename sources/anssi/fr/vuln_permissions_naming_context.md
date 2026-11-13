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
- Documentation relative aux comptes Entra ID Connect (MSOL) :[https://docs.microsoft.com/en-us/azure/active-directory/hybrid/how-to-connect-configure-ad-ds-connector-account](https://docs.microsoft.com/en-us/azure/active-directory/hybrid/how-to-connect-configure-ad-ds-connector-account)  Utiliser la cmdlet `Set-ADSyncRestrictedPermissions` avec `-SkipAdminSdHolders` dans les commandes de configuration.
- Documentation relative aux comptes Entra ID Cloud Sync (ProvAgentgMSA$) :[https://learn.microsoft.com/en-us/entra/identity/hybrid/cloud-sync/how-to-gmsa-cmdlets#using-set-aadcloudsyncrestrictedpermissions](https://learn.microsoft.com/en-us/entra/identity/hybrid/cloud-sync/how-to-gmsa-cmdlets#using-set-aadcloudsyncrestrictedpermissions)  Utiliser la cmdlet `Set-AADCloudSyncRestrictedPermissions`.

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
