# 📋 GPO-Paris-Workstations

> 🖥️ GPO appliquée au site **Site-Paris** — postes de travail.

## Objectif
Gérer la configuration des postes de travail du datacenter Paris : déploiements logiciels, mappages de lecteurs réseau et imprimantes.

## Paramètres principaux
| Catégorie | Valeur |
|---|---|
| Déploiement logiciels | Office 365, VPN Client |
| Lecteurs réseau | P: → \\srv-paris\partages |
| Fond d'écran | Charte graphique Paris |
| Imprimantes | AUTO (site-aware) |

## ⚠️ Bonnes pratiques
- Tester sur un groupe pilote avant déploiement en production
- Utiliser les filtres WMI pour cibler Windows 10/11 uniquement