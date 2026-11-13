# vuln_password_change_server_no_change_90 — Serveurs dont le mot de passe de compte d'ordinateur est inchangé depuis plus de 90 jours

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_password_change_server_no_change_90.md](../en/vuln_password_change_server_no_change_90.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_password_change_server_no_change_90` |
| Niveau ANSSI | 2 (1 : le plus critique) |
| Ajouté à la checklist | 2019-03-21 |
| Dernière modification | 2025-08-01 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | PWD-06 |

## Règles de SMAD-X qui le citent

- **PWD-06** — Compte d'ordinateur dont le mot de passe ne change plus

## Texte officiel

Des serveurs non désactivés n'ont pas changé le mot de passe de leur compte d'ordinateur depuis plus de 90 jours, ce qui indique que leurs secrets ne sont pas renouvelés. Les serveurs ne s'étant pas authentifiés depuis plus de 90 jours apparaissent seulement dans la vulnérabilité de niveau **3** "Serveurs inactifs".

*(Tableau des objets concernés, dans le rapport d'analyse.)*

## Recommandation officielle

Dans leur fonctionnement par défaut, les serveurs changent automatiquement le mot de passe de leur compte d'ordinateur périodiquement (30 jours par défaut). Il convient d'investiguer la raison qui empêche les serveurs de changer leur mot de passe.

Une première vérification consiste à vérifier la valeur des entrées de Registre suivantes :

- `HKLM\System\CurrentControlSet\Services\Netlogon\Parameters\DisablePasswordChange` : doit valoir 0 ou ne pas exister ;
- `HKLM\System\CurrentControlSet\Services\Netlogon\Parameters\MaximumPasswordAge` : doit valoir 30.

Si ces valeurs sont incorrectes, il convient de les remettre aux valeurs par défaut et de s'assurer qu'elles ne sont pas positionnées par une GPO.

Si ces valeurs sont correctes, vérifier si le service `NETLOGON` est démarré avec `sc.exe query netlogon`.

Enfin, il faut vérifier que le mot de passe de la machine soit synchronisé avec celui du compte machine dans l'annuaire. Ceci peut être vérifié avec la commande `nltest /SC_VERIFY:<DomainName>`, où `DomainName` correspond au nom NetBIOS du domaine. Dans les deux vérifications effectuées, la commande doit indiquer le code de retour `0 0x0 NERR_Success`.

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
