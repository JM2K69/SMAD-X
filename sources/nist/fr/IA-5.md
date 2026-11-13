# SP 800-53 Rev. 5 IA-5 — Authenticator Management

> [Sommaire : NIST — SP 800-53 Rev. 5 et SP 800-63B](README.md) · English: [IA-5.md](../en/IA-5.md)

| Fiche | |
|:---|:---|
| Référentiel | NIST — SP 800-53 Rev. 5 et SP 800-63B |
| Identifiant | `SP 800-53 Rev. 5 IA-5` |
| Version | 5.2.0 |
| Lien | [csrc.nist.gov/…](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final) |
| Cité par SMAD-X | KRB-02 · KRB-04 · KRB-05 · PWD-01 · PWD-02 · PWD-03 · PWD-04 · PWD-05 · PWD-07 · PWD-08 · LAPS-01 |

## Règles de SMAD-X qui le citent

- **KRB-02** — Compte utilisateur vulnérable au Kerberoasting
- **KRB-04** — Compte sans préauthentification Kerberos
- **KRB-05** — Mot de passe du compte krbtgt trop ancien
- **PWD-01** — Compte privilégié dont le mot de passe n'expire jamais
- **PWD-02** — Mot de passe qui n'expire jamais
- **PWD-03** — Mot de passe non requis
- **PWD-04** — Mot de passe d'un compte privilégié inchangé depuis plus de 3 ans
- **PWD-05** — Mot de passe machine d'un contrôleur de domaine inchangé
- **PWD-07** — Compte de service managé dont le mot de passe ne change plus
- **PWD-08** — Politique de mot de passe du domaine trop courte
- **LAPS-01** — Ordinateur sans LAPS

## Texte officiel (en anglais : c'est la version qui fait foi)

Manage system authenticators by:
- a. Verifying, as part of the initial authenticator distribution, the identity of the individual, group, role, service, or device receiving the authenticator;
- b. Establishing initial authenticator content for any authenticators issued by the organization;
- c. Ensuring that authenticators have sufficient strength of mechanism for their intended use;
- d. Establishing and implementing administrative procedures for initial authenticator distribution, for lost or compromised or damaged authenticators, and for revoking authenticators;
- e. Changing default authenticators prior to first use;
- f. Changing or refreshing authenticators [Assignment: time period by authenticator type] or when [Assignment: events] occur;
- g. Protecting authenticator content from unauthorized disclosure and modification;
- h. Requiring individuals to take, and having devices implement, specific controls to protect authenticators; and
- i. Changing authenticators for group or role accounts when membership to those accounts changes.

## Source et licence

- **Source** : [https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)
- **Publication relevée** : SP 800-53 Rev. 5, OSCAL 5.2.0 (2026-10-22)
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : domaine public : publication du gouvernement des États-Unis (17 U.S.C. § 105) ; énoncés officiels repris, en anglais
