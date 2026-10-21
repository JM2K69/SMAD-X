# 👥 RAS and IAS Servers Group

**Domain local** group created automatically when the domain is promoted.

## Role
- Members authorized to access **remote access properties** of user accounts
- Used by **NPS (Network Policy Server)**, **RADIUS** and **VPN** servers

## ⚠️ Security
> 🟠 **Tier 1 — Access to network authentication attributes**

- Limit to legitimate NPS/RADIUS servers only
- A compromised RAS server can intercept or manipulate VPN connections