# 🖧 DC03 — Contrôleur de domaine — Site Lyon (Tier 0)

Contrôleur de domaine secondaire hébergé dans le site **Site-Lyon** (10.0.2.0/24).

## Rôle
- Authentification locale des utilisateurs du site de Lyon (**réduction de la latence WAN**)
- Maintient une réplique locale complète de l'annuaire AD
- Évite les flux d'authentification inter-sites coûteux
- Point de continuité en cas de coupure WAN avec Paris

## Rôles FSMO
- Aucun rôle FSMO (peut être désigné PDC Emulator de secours)

## Infrastructure réseau
- Adresse IP : 10.0.2.1/24
- Site AD : **Site-Lyon**
- Réplication inter-site : **DEFAULTIPSITELINK** — coût 100, intervalle 15 min

## ⚠️ Sécurité
> 🔴 **Tier 0 — Infrastructure AD critique**

- Sécuriser physiquement la salle serveur de Lyon (accès badge, vidéosurveillance)
- Appliquer BitLocker sur les volumes du DC pour protéger les données AD en cas de vol physique
- Trafic de réplication chiffré (Kerberos + RPC/SChannel)
- Surveiller les Event ID 4741 (création compte machine) et 4743 (suppression)