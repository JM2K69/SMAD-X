# 👤 John Doe (jdoe) — Développeur (Tier 2)

Compte utilisateur standard pour un développeur de l'organisation.

## Rôle
- Accès aux ressources de développement (dépôts de code, serveurs de dev, tickets)
- Membre du groupe **Domain Users** et éventuellement d'un groupe **Developers**
- Pas de droits administrateurs locaux sur son poste de travail

## ⚠️ Sécurité
> 🟢 **Tier 2**

- Ne pas accorder de droits administrateurs locaux (utiliser des groupes dédiés via GPO si nécessaire)
- Sensibiliser aux risques liés au développement : secrets dans le code, dépendances malveillantes
- Surveiller les accès aux ressources sensibles hors périmètre développement
- Si accès à des environnements de production : créer un compte séparé Tier 1