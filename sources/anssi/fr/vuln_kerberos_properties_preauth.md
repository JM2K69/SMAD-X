# vuln_kerberos_properties_preauth — Comptes sans préauthentification Kerberos

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_kerberos_properties_preauth.md](../en/vuln_kerberos_properties_preauth.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_kerberos_properties_preauth` |
| Niveau ANSSI | 2 (1 : le plus critique) |
| Ajouté à la checklist | 2018-10-01 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | KRB-04 |

## Règles de SMAD-X qui le citent

- **KRB-04** — Compte sans préauthentification Kerberos

## Texte officiel

Des comptes n'imposent pas la préauthentification Kerberos. Sans préauthentification il est possible d'obtenir un ticket chiffré avec un des secrets associés au compte correspondant. Il est ensuite possible de lancer une attaque afin de retrouver le mot de passe de l'utilisateur.

*(Tableau des objets concernés, dans le rapport d'analyse.)*

## Recommandation officielle

La préauthentification permet de s'assurer que l'utilisateur connaît un de ses secrets d'authentification lors d'une demande de TGT (ticket Kerberos obtenu auprès d'un contrôleur de domaine). Par défaut, les comptes utilisateur imposent la préauthentification. Cette propriété ne doit jamais être positionnée pour les comptes du domaine.

#### Cas général

La propriété `DONT_REQUIRE_PREAUTH` doit être supprimée pour ces comptes et le mot de passe doit être changé.

#### Cas particuliers

En cas d'incompatibilité avec une application, celle-ci doit faire l'objet d'une évolution applicative. Ce problème peut être réduit via les contournements suivants:

- une politique de mot de passe affinée (*Password Settings Object* ou PSO) s'applique sur le compte avec une longueur de mot de passe supérieure à **32** ;
- une politique de mot de passe affinée (*Password Settings Object* ou PSO) s'applique sur le compte avec une durée d'expiration maximale de **3 ans** ;
- le mot de passe du compte a changé depuis la dernière modification du PSO ;
- le compte ne peut utiliser que les algorithmes AES.

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
