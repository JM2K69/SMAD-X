# SP 800-53 Rev. 5 AC-6 — Least Privilege

> [Sommaire : NIST — SP 800-53 Rev. 5 et SP 800-63B](README.md) · English: [AC-6.md](../en/AC-6.md)

| Fiche | |
|:---|:---|
| Référentiel | NIST — SP 800-53 Rev. 5 et SP 800-63B |
| Identifiant | `SP 800-53 Rev. 5 AC-6` |
| Version | 5.2.0 |
| Lien | [csrc.nist.gov/…](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final) |
| Cité par SMAD-X | DLG-01 · DLG-02 · DLG-03 · PRIV-04 · ACL-01 · ACL-02 · ACL-03 · TIER-01 · TIER-03 |

## Règles de SMAD-X qui le citent

- **DLG-01** — Délégation Kerberos non contrainte
- **DLG-02** — Délégation contrainte avec transition de protocole vers un contrôleur de domaine
- **DLG-03** — Délégation contrainte vers un contrôleur de domaine
- **PRIV-04** — Membre non privilégié du groupe DnsAdmins
- **ACL-01** — Objet critique avec un propriétaire inadapté
- **ACL-02** — Chemin de contrôle vers le domaine par les permissions
- **ACL-03** — Permissions dangereuses sur le domaine, AdminSDHolder ou les contrôleurs de domaine
- **TIER-01** — Violation du cloisonnement : appartenance à un groupe plus sensible
- **TIER-03** — Violation du cloisonnement : délégation à un tier moins sensible

## Texte officiel (en anglais : c'est la version qui fait foi)

Employ the principle of least privilege, allowing only authorized accesses for users (or processes acting on behalf of users) that are necessary to accomplish assigned organizational tasks.

## Source et licence

- **Source** : [https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)
- **Publication relevée** : SP 800-53 Rev. 5, OSCAL 5.2.0 (2026-10-22)
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : domaine public : publication du gouvernement des États-Unis (17 U.S.C. § 105) ; énoncés officiels repris, en anglais
