# 👥 Groupe Cloneable Domain Controllers

Groupe **global** créé automatiquement lors de la promotion du domaine (Windows Server 2012+).

## Rôle
- Membres (DCs) qui peuvent être **clonés** pour déployer rapidement des contrôleurs de domaine supplémentaires
- Fonctionnalité de clonage basée sur Hyper-V

## ⚠️ Sécurité
> 🔴 **Tier 0 — Fonctionnalité de clonage de DC**

- Limiter aux DCs qui doivent réellement être clonés
- Un clone de DC hérite des secrets du DC source — processus à encadrer strictement
- Vérifier que l'hôte Hyper-V est sécurisé avant tout clonage