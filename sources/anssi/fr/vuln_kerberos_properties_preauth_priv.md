# vuln_kerberos_properties_preauth_priv — Comptes privilégiés sans préauthentification Kerberos

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_kerberos_properties_preauth_priv.md](../en/vuln_kerberos_properties_preauth_priv.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_kerberos_properties_preauth_priv` |
| Niveau ANSSI | 1 (1 : le plus critique) |
| Ajouté à la checklist | 2018-10-01 |
| Dernière modification | 2019-11-06 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | KRB-03 |

## Règles de SMAD-X qui le citent

- **KRB-03** — Compte privilégié sans préauthentification Kerberos

## Texte officiel

Des comptes privilégiés n'imposent pas la préauthentification Kerberos.

*(Tableau des objets concernés, dans le rapport d'analyse.)*

## Recommandation officielle

La préauthentification permet de s'assurer que l'utilisateur connaît un de ses secrets d'authentification lors d'une demande de TGT (ticket Kerberos obtenu auprès d'un contrôleur de domaine). Sans préauthentification il est possible d'obtenir un ticket chiffré avec un des secrets associés au compte correspondant. Il est ensuite possible de lancer une attaque afin de retrouver le mot de passe de l'utilisateur, ce qui peut être facilité s'il n'est pas assez robuste.

La propriété `DONT_REQUIRE_PREAUTH` doit être supprimée pour ces comptes et le mot de passe doit être changé.

Par défaut, tous les comptes utilisateur imposent la préauthentification car la propriété `DONT_REQUIRE_PREAUTH` n'est pas positionnée. Cette propriété ne doit jamais être positionnée pour les comptes privilégiés du domaine. En cas d'incompatibilité avec une application, celle-ci doit faire l'objet d'une évolution applicative.

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
