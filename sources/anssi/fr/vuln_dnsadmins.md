# vuln_dnsadmins — Permissions dangereuses sur le groupe DnsAdmins

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_dnsadmins.md](../en/vuln_dnsadmins.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_dnsadmins` |
| Niveau ANSSI | 1 (1 : le plus critique) |
| Ajouté à la checklist | 2018-12-19 |
| Dernière modification | 2022-10-26 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | PRIV-04 |

## Règles de SMAD-X qui le citent

- **PRIV-04** — Membre non privilégié du groupe DnsAdmins

## Texte officiel

Plusieurs comptes non privilégiés sont membres du groupe `DnsAdmins` ou possèdent des droits dangereux sur celui-ci.

*(Tableau des objets concernés, dans le rapport d'analyse.)*

Les membres du groupe `DnsAdmins` disposent de nombreux droits pour gérer le service DNS Microsoft, qui est généralement hébergé par un contrôleur de domaine.

Parmi ces droits, il est possible de gérer l'intégralité des enregistrements des zones DNS ayant une portée domaine, d'employer des fonctionnalités de debug du serveur, voire non documentées, comme par exemple la possibilité de faire redémarrer instantanément ou d'arrêter le service DNS.

Enfin, selon le niveau de mise à jour du système d'exploitation de l'intégralité des serveurs DNS en production, ou de la configuration locale du service DNS, il peut toujours être possible de faire exécuter du code arbitraire *via* la fonctionnalité `serverlevelplugindll`.

Malgré la [disponibilité d'un correctif auprès de Microsoft](https://msrc.microsoft.com/update-guide/vulnerability/CVE-2021-40469), ces fonctionnalités dangereuses peuvent toujours être réactivées. Il est donc impossible d'évaluer l'innocuité du paramétrage uniquement par l'étude de la configuration dans l'Active Directory, tout comme il est impossible de vérifier avec certitude que l'ensemble des serveurs DNS Microsoft soient à jour.

L'ensemble de ces possibilités donne des capacités de nuisance significatives, voire facilite des attaques réseau, et justifient donc de considérer ce groupe comme étant privilégié.

## Recommandation officielle

Il est recommandé de ne pas utiliser le groupe `DNSAdmin`. Une délégation doit être créée pour la gestion du DNS (création/suppression de zones, gestion des enregistrements, etc.), généralement effectuée à l'aide de l'utilitaire `ldp`. Cette délégation peut être effectuée en deux étapes.

#### Étape 1: autoriser l'accès aux RPC nécessaires à l'utilisation des composants enfichables de gestion DNS

Dans la partition du domaine, dans le conteneur `CN=System`, déléguer les droits suivants sur le conteneur `CN=MicrosoftDNS` :

#### Étape 2: autoriser la modification de la zone DNS

Dans la partition `DC=DomainDnsZones`, sur chaque zone ayant besoin d'être administrée, déléguer les droits suivants avec héritage sur le conteneur `CN=MicrosoftDNS` :

Attention à ne pas oublier également de déléguer les zones inverses (*reverse DNS*).

Note : pour changer de *naming context* (partition) dans `ldp`, il faut utiliser le menu `affichage`, `arborescence` et utiliser le menu déroulant pour sélectionner `DomainDnsZones`.

De plus, les opérations suivantes sont à réaliser sur le groupe `DnsAdmins` :

- vider le groupe ;
- s'assurer que le propriétaire du groupe est `S-1-5-21-domain-512` ;
- optionnellement, positionner une ACE interdisant à tout le monde de changer le propriétaire, de changer les droits d'accès et d'écrire les membres du groupe.

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
