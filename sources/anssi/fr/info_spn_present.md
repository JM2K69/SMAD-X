# info_spn_present — Comptes utilisateurs avec SPN

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [info_spn_present.md](../en/info_spn_present.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `info_spn_present` |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | KRB-02 |

## Règles de SMAD-X qui le citent

- **KRB-02** — Compte utilisateur vulnérable au Kerberoasting

## Texte officiel

L'attribut `servicePrincipalName` (SPN) permet d'associer des noms de service Kerberos à des comptes. Lorsqu'un compte possède un nom de service Kerberos, il est possible de demander un ticket pour ce service. Dans ce cas, le ticket émis est chiffré avec une des clés Kerberos associée au compte correspondant. Il est ensuite possible de lancer une attaque afin de retrouver le mot de passe de l'utilisateur, ce qui peut être facilité s'il n'est pas assez robuste. Par défaut seuls les comptes machine possèdent des SPN.

Les SPN suivants sont positionnés :

*(Tableau des objets concernés, dans le rapport d'analyse.)*

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
