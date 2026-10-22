# 🔗 DEFAULTIPSITELINK — Lien Paris ↔ Lyon

Lien de réplication AD entre les sites **Site-Paris** et **Site-Lyon**.

## Paramètres
| Paramètre | Valeur |
|-----------|--------|
| Transport | IP (RPC/IP) |
| Coût | 100 |
| Intervalle | 15 minutes |
| Planification | Toujours disponible |

## ⚠️ Bonnes pratiques
- Ajuster l'intervalle de réplication selon la bande passante WAN disponible
- En cas de liaison WAN < 512 Kbps, envisager SMTP comme transport alternatif
- Utiliser `repadmin /showrepl` pour diagnostiquer les retards de réplication