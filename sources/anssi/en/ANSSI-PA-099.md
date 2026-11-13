# ANSSI-PA-099 — Recommandations pour l’administration sécurisée des SI reposant sur AD (document in French)

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [ANSSI-PA-099.md](../fr/ANSSI-PA-099.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `ANSSI-PA-099` |
| Link | [messervices.cyber.gouv.fr/…](https://messervices.cyber.gouv.fr/guides/recommandations-pour-ladministration-securisee-des-si-reposant-sur-ad) |
| Cited by SMAD-X | PRIV-05 · ACL-02 · TIER-01 · TIER-02 · TIER-03 · TIER-04 · TIER-05 · TIER-06 · TIER-07 · TIER-08 · TIER-09 |

## SMAD-X rules that cite it

- **PRIV-05** — Protected group classified outside Tier 0
- **ACL-02** — Control path to the domain through permissions
- **TIER-01** — Tiering violation: membership in a more sensitive group
- **TIER-02** — Tiering violation: GPO from a less sensitive tier
- **TIER-03** — Tiering violation: delegation to a less sensitive tier
- **TIER-04** — Privileged account logged on to a less sensitive machine
- **TIER-05** — Tiering model element missing
- **TIER-06** — Object outside the tiering model's OUs
- **TIER-07** — Object waiting in a transit OU
- **TIER-08** — Privileged account or group outside Tier 0
- **TIER-09** — Privileged account outside an enforced authentication silo

## Official text

L’ANSSI fait régulièrement le constat que des compromissions de systèmes d'information (SI) reposant sur un annuaire Active Directory (AD) résultent de l’application de mauvaises pratiques d’administration et d'un cloisonnement insuffisant. Ces compromissions commencent souvent par des attaques qui ciblent les postes de travail. Les attaquants exploitent ensuite des faiblesses du SI pour opérer des déplacements, dits latéraux et élever progressivement leurs privilèges jusqu’à obtenir le contrôle total de l'AD. À ce niveau de contrôle de l’AD, un attaquant est en mesure de s’aménager des portes dérobées qui lui assurent un contrôle persistant du SI, c'est-à-dire également des processus et des données métiers de l'organisation.Le guide d'administration sécurisée des systèmes d'information reposant sur un annuaire AD aborde les aspects spécifiques à l’administration de ces environnements et guide dans le cloisonnement du SI en zones de confiance. Il complète ainsi les recommandations de sécurité relatives à l'administration sécurisée des systèmes d'information qui se veulent agnostiques des technologies mises en œuvre.

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
