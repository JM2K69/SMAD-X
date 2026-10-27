# 🛡️ AdminSDHolder

Modèle des permissions des comptes et groupes protégés (Domain Admins, Administrators, opérateurs…).

## Rôle
- Toutes les heures, le processus **SDProp** recopie les permissions d'AdminSDHolder sur chaque objet protégé (`adminCount = 1`)
- Une délégation posée sur une OU ne s'applique donc pas aux administrateurs qu'elle contient

## ⚠️ Sécurité
> 🔴 **Tier 0 — Point de persistance connu**

- Quiconque peut modifier AdminSDHolder obtient, une heure plus tard, des droits sur tous les administrateurs
- Ne jamais y ajouter de permission ; surveiller toute modification (Event ID 5136)
