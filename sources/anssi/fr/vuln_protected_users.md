# vuln_protected_users — Comptes privilégiés non membres du groupe Protected Users

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_protected_users.md](../en/vuln_protected_users.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_protected_users` |
| Niveau ANSSI | 3 (1 : le plus critique) |
| Ajouté à la checklist | 2019-06-11 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | DLG-04 |

## Règles de SMAD-X qui le citent

- **DLG-04** — Compte privilégié non protégé contre la délégation

## Texte officiel

Des comptes privilégiés ne sont pas protégés par le groupe `S-1-5-21-domain-525`.

*(Tableau des objets concernés, dans le rapport d'analyse.)*

## Recommandation officielle

L'ensemble des comptes privilégiés doivent être ajoutés au groupe `S-1-5-21-domain-525` afin de :

- forcer l'authentification en Kerberos ;
- réduire la durée de vie des tickets Kerberos ;
- forcer l'utilisation d'algorithmes de chiffrement forts (AES) ;
- interdire la mise en cache du mot de passe sur les postes de travail ;
- interdire toute forme de délégation.

**Attention** : l'utilisation du groupe `S-1-5-21-domain-525` a des impacts fonctionnels importants. Référence : [https://docs.microsoft.com/en-us/windows-server/security/credentials-protection-and-management/protected-users-security-group](https://docs.microsoft.com/en-us/windows-server/security/credentials-protection-and-management/protected-users-security-group).

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
