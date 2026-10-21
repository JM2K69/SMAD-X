# 👥 Groupe Protected Users

Groupe **universel** créé automatiquement (Windows Server 2012 R2+).

## Rôle
- Les membres bénéficient de **protections Kerberos renforcées** automatiquement :
  - Pas de délégation Kerberos
  - Pas de RC4 pour l'authentification (Kerberos AES uniquement)
  - Pas de mise en cache des credentials NTLM
  - TGT réduit à 4 heures (non renouvelable)
  - Pas d'authentification NTLM, Digest, CredSSP

## ⚠️ Sécurité
> 🟢 **Bonne pratique — ajouter les comptes sensibles**

- **Ajouter** tous les comptes d'administration Tier 0 et Tier 1
- **Tester** avant d'ajouter des comptes de service — incompatible avec certains protocoles legacy
- **Ne pas ajouter** : comptes de service utilisant NTLM, délégation Kerberos, ou authentification avec mot de passe en clair
- Protège contre **Pass-the-Hash**, **Pass-the-Ticket**, **Overpass-the-Hash**