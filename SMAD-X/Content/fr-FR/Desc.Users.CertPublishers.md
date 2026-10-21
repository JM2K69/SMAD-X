# 👥 Groupe Cert Publishers

Groupe **domaine local** créé automatiquement lors de la promotion du domaine.

## Rôle
- Membres autorisés à **publier des certificats** dans Active Directory (attribut `userCertificate`)
- Typiquement : les serveurs **Certificate Authority (CA)** enterprise

## ⚠️ Sécurité
> 🟠 **Tier 1 — Lié à l'infrastructure PKI**

- Limiter les membres aux serveurs CA uniquement
- La compromission d'un CA enterprise permet d'émettre des certificats frauduleux
- Surveiller les modifications de ce groupe — Event ID 4728, 4729
- Voir **ESC4** et autres techniques d'attaque PKI documentées dans **Certify/Certipy**