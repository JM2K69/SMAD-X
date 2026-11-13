# vuln_krbtgt — Mot de passe du compte krbtgt inchangé depuis plus d'un an

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_krbtgt.md](../en/vuln_krbtgt.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_krbtgt` |
| Niveau ANSSI | 2 (1 : le plus critique) |
| Ajouté à la checklist | 2018-09-26 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | KRB-05 |

## Règles de SMAD-X qui le citent

- **KRB-05** — Mot de passe du compte krbtgt trop ancien

## Texte officiel

Le mot de passe du compte `krbtgt` n'a pas été changé depuis plus d'un an.

Le compte `krbtgt` est un compte d'infrastructure servant de support de stockage aux clés des centres de distribution des clés Kerberos. La compromission du compte `krbtgt` permet à un attaquant de forger des tickets Kerberos (souvent appelés *golden tickets*) et ainsi de pouvoir s'authentifier auprès de n'importe quelle ressource (serveur, poste de travail, etc.) du domaine Active Directory avec des droits d'administration, et ce, de manière relativement furtive. Le compte `krbtgt` ne changeant pas automatiquement de mot de passe, si la base des comptes de l'Active Directory a été extraite (par exemple par un ancien administrateur, lors d'un audit ou pour un test de robustesse des mots de passe), il est possible, tant que ce mot de passe n'a pas été changé, d'utiliser les informations contenues dans la base afin d'en extraire les secrets. Une personne malveillante peut ainsi s'authentifier sur l'ensemble des services du domaine Active Directory plusieurs années après.

*(Tableau des objets concernés, dans le rapport d'analyse.)*

## Recommandation officielle

Le compte `krbtgt` est un compte d'infrastructure servant de support de stockage aux clés des centres de distribution des clés Kerberos. Afin de renouveler les clés Kerberos utilisées pour chiffrer les TGT, il est nécessaire de procéder annuellement à un changement manuel du mot de passe du compte `krbtgt`. Il est conseillé d'effectuer ce changement en utilisant le [script mis à disposition par Microsoft](https://github.com/microsoft/New-KrbtgtKeys.ps1).

Le changement de mot de passe doit être effectué deux fois pour être efficace.

Attention : toute opération de changement du mot de passe du compte `krbtgt` doit être effectuée uniquement dans un environnement Active Directory où la réplication entre contrôleurs de domaine est nominale. Ainsi il est indispensable d'attendre un délai avant le deuxième changement de mot de passe.

Il est également possible de réinitialiser manuellement le mot de passe du compte `krbtgt`, de la même façon qu'un compte classique. Si le script mis à disposition n'est pas utilisé, il est recommandé un délai d'au minimum 24 heures entre les deux changements et de s'assurer de la réplication effective entre contrôleurs de domaine. Une stratégie peut être d'effectuer un unique changement de mot de passe tous les 6 mois, afin de garantir un changement effectif annuel.

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
