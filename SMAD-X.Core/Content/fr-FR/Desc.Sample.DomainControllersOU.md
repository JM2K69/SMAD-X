# 🏛️ OU Domain Controllers (Tier 0)

Unité d'organisation système contenant tous les **contrôleurs de domaine**.

## Rôle
- Créée automatiquement lors de la promotion du premier DC
- Tous les DCs y sont automatiquement placés lors de leur promotion
- Cible exclusive de la **Default Domain Controllers Policy** et des GPOs de durcissement DC

## ⚠️ Sécurité
> 🔴 **Tier 0 — Zone la plus critique du domaine**

- La **Default Domain Controllers Policy** doit rester liée à cette OU et ne jamais être supprimée
- Appliquer des GPOs de durcissement supplémentaires : CIS DC Benchmark, ANSSI AD
- Interdire toute connexion interactive de comptes Tier 1 ou Tier 2 sur les DCs
- Activer l'audit avancé sur tous les DCs (connexions, modifications AD, Kerberos)
- Surveiller les tentatives d'exploitation Kerberos (AS-REP Roasting, Kerberoasting) via les Event IDs 4768, 4769, 4771
- Sauvegardes de l'état du système (System State) planifiées et testées régulièrement