# 👥 Groupe Denied RODC Password Replication Group

Groupe **domaine local** créé automatiquement lors de la promotion du domaine.

## Rôle
- Définit les comptes dont les **mots de passe NE PEUVENT PAS être mis en cache** sur les RODCs
- Protège les comptes critiques contre l'exposition en cas de compromission d'un RODC

## Membres par défaut
`krbtgt`, `Domain Admins`, `Schema Admins`, `Enterprise Admins`, `Group Policy Creator Owners`, `Read-Only Domain Controllers`

## ⚠️ Sécurité
> 🟢 **Bonne pratique — maintenir à jour**

- Ajouter tous les comptes de service critiques et comptes Tier 0 à ce groupe
- Ne jamais retirer les membres par défaut
- Auditer régulièrement la liste pour s'assurer qu'elle est exhaustive