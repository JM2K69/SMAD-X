# vuln_password_change_msa_no_change_90 — Comptes de service managés dont le mot de passe de compte est inchangé depuis plus de 90 jours

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_password_change_msa_no_change_90.md](../en/vuln_password_change_msa_no_change_90.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_password_change_msa_no_change_90` |
| Niveau ANSSI | 2 (1 : le plus critique) |
| Ajouté à la checklist | 2021-03-15 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | PWD-07 |

## Règles de SMAD-X qui le citent

- **PWD-07** — Compte de service managé dont le mot de passe ne change plus

## Texte officiel

Des comptes de service managés n'ont pas changé le mot de passe de leur compte depuis plus de 90 jours, ce qui indique que leurs secrets ne sont pas renouvelés. Les comptes désactivés ne sont pas considérés.

*(Tableau des objets concernés, dans le rapport d'analyse.)*

## Recommandation officielle

Dans leur fonctionnement par défaut, les comptes de service managés changent automatiquement le mot de passe de leur compte périodiquement (30 jours par défaut). Il convient d'investiguer la raison qui empêche les serveurs de changer leur mot de passe.

Une première vérification consiste à vérifier la valeur des entrées de Registre suivantes :

- `HKLM\System\CurrentControlSet\Services\Netlogon\Parameters\DisablePasswordChange` : doit valoir 0 ou ne pas exister ;
- `HKLM\System\CurrentControlSet\Services\Netlogon\Parameters\MaximumPasswordAge` : doit valoir 30.

Si ces valeurs sont incorrectes, il convient de les remettre aux valeurs par défaut et de s'assurer qu'elles ne sont pas positionnées par une GPO.

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
