# 📋 GPO-Baseline-DC

> 🔒 GPO de sécurité appliquée au site **Default-First-Site-Name**.

## Objectif
Définir la ligne de base de sécurité pour tous les contrôleurs de domaine du site par défaut.

## Paramètres principaux
| Catégorie | Valeur |
|---|---|
| Audit de connexion | Succès, Échec |
| Pare-feu Windows | Activé (tous profils) |
| Droits locaux | Administrators uniquement |
| SMB Signing | Requis |

## ⚠️ Bonnes pratiques
- Ne pas modifier sans validation en environnement de test
- Compléter avec les benchmarks CIS Level 1 pour les DC