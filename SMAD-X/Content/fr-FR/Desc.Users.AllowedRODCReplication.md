# 👥 Groupe Allowed RODC Password Replication Group

Groupe **domaine local** créé automatiquement lors de la promotion du domaine.

## Rôle
- Définit les comptes dont les **mots de passe PEUVENT être mis en cache** sur les RODCs
- Vide par défaut — aucun mot de passe n'est répliqué sur les RODCs par défaut

## ⚠️ Sécurité
> 🔴 **Tier 0 — Contrôle de la réplication RODC**

- **Ne jamais ajouter** de comptes Tier 0 (admins, krbtgt, service accounts critiques)
- La compromission d'un RODC expose uniquement les comptes dont le hash est présent dans son cache
- Utiliser le groupe **Denied RODC Password Replication Group** pour protéger les comptes sensibles
- Auditer régulièrement les comptes mis en cache sur chaque RODC via `Get-ADDomainControllerPasswordReplicationPolicy`