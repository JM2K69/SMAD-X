# 👥 Cert Publishers Group

**Domain local** group created automatically when the domain is promoted.

## Role
- Members authorized to **publish certificates** in Active Directory (`userCertificate` attribute)
- Typically: enterprise **Certificate Authority (CA)** servers

## ⚠️ Security
> 🟠 **Tier 1 — Linked to PKI infrastructure**

- Limit membership to CA servers only
- Compromising an enterprise CA allows issuing fraudulent certificates
- Monitor modifications — Event ID 4728, 4729
- See **ESC4** and other PKI attack techniques documented in **Certify/Certipy**