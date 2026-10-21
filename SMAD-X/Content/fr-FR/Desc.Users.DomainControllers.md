# 👥 Groupe Domain Controllers

Groupe **global** créé automatiquement lors de la promotion du domaine.

## Rôle
- Contient **tous** les contrôleurs de domaine (DC) du domaine
- Ajout automatique lors de la promotion d'un serveur en DC

## ⚠️ Sécurité
> 🔴 **Tier 0 — Groupe critique**

- Surveiller toute modification (ajout/suppression de DC) — Event ID 4728, 4729
- Un contrôleur de domaine compromis compromet l'intégralité du domaine