# 👥 Groupe DnsAdmins

Groupe **domaine local** créé automatiquement lors de l'installation du rôle DNS.

## Rôle
- Accès administratif complet au service **DNS** du domaine
- Permet de gérer les zones, enregistrements et configuration DNS

## ⚠️ Sécurité
> 🔴 **Tier 0 — Escalade de privilèges connue**

- Vecteur d'**escalade de privilèges critique** : un membre DnsAdmins peut charger une DLL malveillante dans le service DNS (qui tourne sous SYSTEM sur les DCs)
- **CVE** : Technique documentée par Shay Ber (2017) — `dnscmd /config /serverlevelplugindll`
- Limiter au strict minimum les membres de ce groupe
- Surveiller l'utilisation de `dnscmd` par les membres (Event ID 4688)
- Envisager de déléguer la gestion DNS différemment si possible