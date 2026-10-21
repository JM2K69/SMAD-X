# 👤 John Doe (jdoe) — Developer (Tier 2)

Standard user account for an organization developer.

## Role
- Access to development resources (code repositories, dev servers, tickets)
- Member of **Domain Users** and optionally a **Developers** group
- No local administrator rights on workstation

## ⚠️ Security
> 🟢 **Tier 2**

- Do not grant local administrator rights (use dedicated groups via GPO if needed)
- Raise awareness of development risks: secrets in code, malicious dependencies
- Monitor access to sensitive resources outside the development scope
- If access to production environments is needed: create a separate Tier 1 account