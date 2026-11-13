# vuln_dont_expire — Comptes dont le mot de passe n'expire jamais

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_dont_expire.md](../en/vuln_dont_expire.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_dont_expire` |
| Niveau ANSSI | 2 (1 : le plus critique) |
| Ajouté à la checklist | 2018-09-28 |
| Dernière modification | 2022-11-10 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | PWD-02 |

## Règles de SMAD-X qui le citent

- **PWD-02** — Mot de passe qui n'expire jamais

## Texte officiel

Plus de 10% des comptes possèdent des mots de passe n'expirant pas. La récupération d'un compte permet à un individu malveillant de conserver ses droits d'accès au domaine sur le long terme.

*(Tableau des objets concernés, dans le rapport d'analyse.)*

## Recommandation officielle

#### Cas général

Afin qu'un renouvellement soit imposé techniquement par l'Active Directory, il est nécessaire que les comptes ne possèdent pas la propriété `DONT_EXPIRE`. Cette propriété doit être supprimée et le mot de passe être changé.

S'il est contre-productif d'imposer un délai d'expiration trop court, il convient également d'éviter que des mots de passe ne soient jamais changés. En effet, il est parfois constaté la présence de comptes actifs dont le mot de passe n'a pas changé depuis parfois plus de 10 ans, augmentant la probabilité de son exposition, de sa réutilisation ou de sa faiblesse.

Aussi, cette exception peut avoir pour effet de mettre l'organisme dans la situation de ne jamais pouvoir changer certains mots de passe.

Enfin, les comptes utilisateurs d'un Active Directory ne sont techniquement pas distinguables de comptes de services non gérés (au sens gMSA/sMSA). La politique de mot de passe imposée par le domaine peut ainsi être soit globale, soit différenciée entre les types de comptes utilisateurs non privilégiés via le mécanisme des Password Settings Object (PSO). Si une durée de vie longue des mots de passe est tolérée par la politique de l'organisme, celle-ci doit être explicitement définie par la politique de mots de passe. La différenciation permet de maintenir une gestion suivie dans le temps de certaines catégories de comptes et impose la documentation des exceptions et de leurs procédures de changement de mots de passe.

L'usage de la propriété `DONT_EXPIRE` a généralement pour conséquence l'oubli du renouvellement du mot de passe de certains comptes d'importance particulière, après un incident de sécurité ou non, voire légitime l'installation de produits ou l'usage de comptes fonctionnels ayant cette caractéristique et dont la procédure de renouvellement risque de ne pas être documentée.

#### Cas des comptes de service

Pour être en mesure de changer le mot de passe des comptes de service et de pouvoir tracer leur utilisation, il est nécessaire de documenter leur emploi. Un document doit ainsi recenser tous les comptes de service et leur utilisation. Il doit contenir pour chaque compte de service identifié :

- les informations concernant le compte (`samAccountName`, description, groupes AD, caractéristiques, etc.) ;
- les applications ou les systèmes qui utilisent ce compte ;
- la date de dernier changement du mot de passe associé au compte ;
- la procédure de changement du mot de passe du compte ;
- une personne ou une équipe responsable de son cycle de vie.

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
