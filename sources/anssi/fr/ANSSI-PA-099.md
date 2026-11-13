# ANSSI-PA-099 — Recommandations pour l’administration sécurisée des SI reposant sur AD

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [ANSSI-PA-099.md](../en/ANSSI-PA-099.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `ANSSI-PA-099` |
| Lien | [messervices.cyber.gouv.fr/…](https://messervices.cyber.gouv.fr/guides/recommandations-pour-ladministration-securisee-des-si-reposant-sur-ad) |
| Cité par SMAD-X | PRIV-05 · ACL-02 · TIER-01 · TIER-02 · TIER-03 · TIER-04 · TIER-05 · TIER-06 · TIER-07 · TIER-08 · TIER-09 |

## Règles de SMAD-X qui le citent

- **PRIV-05** — Groupe protégé classé hors du Tier 0
- **ACL-02** — Chemin de contrôle vers le domaine par les permissions
- **TIER-01** — Violation du cloisonnement : appartenance à un groupe plus sensible
- **TIER-02** — Violation du cloisonnement : GPO d'un tier moins sensible
- **TIER-03** — Violation du cloisonnement : délégation à un tier moins sensible
- **TIER-04** — Compte privilégié connecté sur une machine moins sensible
- **TIER-05** — Élément du modèle de cloisonnement absent
- **TIER-06** — Objet rangé hors des OU du modèle de cloisonnement
- **TIER-07** — Objet en attente dans une OU de transit
- **TIER-08** — Compte ou groupe privilégié rangé hors du Tier 0
- **TIER-09** — Compte privilégié hors d'un silo d'authentification appliqué

## Texte officiel

L’ANSSI fait régulièrement le constat que des compromissions de systèmes d'information (SI) reposant sur un annuaire Active Directory (AD) résultent de l’application de mauvaises pratiques d’administration et d'un cloisonnement insuffisant. Ces compromissions commencent souvent par des attaques qui ciblent les postes de travail. Les attaquants exploitent ensuite des faiblesses du SI pour opérer des déplacements, dits latéraux et élever progressivement leurs privilèges jusqu’à obtenir le contrôle total de l'AD. À ce niveau de contrôle de l’AD, un attaquant est en mesure de s’aménager des portes dérobées qui lui assurent un contrôle persistant du SI, c'est-à-dire également des processus et des données métiers de l'organisation.Le guide d'administration sécurisée des systèmes d'information reposant sur un annuaire AD aborde les aspects spécifiques à l’administration de ces environnements et guide dans le cloisonnement du SI en zones de confiance. Il complète ainsi les recommandations de sécurité relatives à l'administration sécurisée des systèmes d'information qui se veulent agnostiques des technologies mises en œuvre.

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
