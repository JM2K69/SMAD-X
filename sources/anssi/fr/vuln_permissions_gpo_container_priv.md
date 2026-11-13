# vuln_permissions_gpo_container_priv — Permissions dangereuses sur des conteneurs d'objets privilégiés (chemins de contrôle)

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_permissions_gpo_container_priv.md](../en/vuln_permissions_gpo_container_priv.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_permissions_gpo_container_priv` |
| Niveau ANSSI | 2 (1 : le plus critique) |
| Ajouté à la checklist | 2022-07-29 |
| Dernière modification | 2023-11-08 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | ACL-02 |

## Règles de SMAD-X qui le citent

- **ACL-02** — Chemin de contrôle vers le domaine par les permissions

## Recommandation officielle

Il recommandé d'enlever les permissions dangereuses sur ces conteneurs afin de revenir à un état par défaut. La correction est généralement effectuée à l'aide du composant logiciel enfichable `ADSI Edit` ou de l'utilitaire `ldp`.

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

> #### Acceptation du risque
>
> Si des permissions spécifiques sont nécessaires, il convient de considérer que les comptes ou les groupes délégués sont eux-mêmes privilégiés. Ainsi, ces objets doivent être correctement protégés et leurs permissions appliquées doivent être au moins aussi restrictives que celles appliquées à l'objet `adminSDHolder` afin d'éviter de créer de nouveaux chemins de contrôle. Il est donc nécessaire de leur appliquer des permissions "sécurisées". Pour cela, il est recommandé d'utiliser la commande `` du module `AdSyncConfig.psm1` fourni avec Azure AD Connect. Cette commande supprime l'héritage et applique des permissions (ACE) restreintes, qui sont toutes également présentes dans un objet `adminSDHolder` par défaut.
>
> Pour un **groupe délégué**, l'application de cette seule commande n'est pas suffisante : il faut également retirer les membres du groupe, ou les considérer eux-mêmes comme étant privilégié en leur appliquant la même commande.
>
> Exemple d'utilisation de `` pour appliquer des permissions "sécurisées" sur un compte privilégié :
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
> Si l'objet `adminSDHolder` a été modifié, il faut s'assurer que les conditions ci-dessus sont bien remplies : la commande `` ne correspondent alors peut-être plus à l'objet `adminSDHolder`.
>
> Une fois protégés, les comptes apparaissent dans le point d'information "Comptes Tier 0 supplémentaires".

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
