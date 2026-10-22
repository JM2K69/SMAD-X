# 👥 Key Admins Group

**Global** group created automatically (Windows Server 2016+).

## Role
- Members authorized to perform administrative actions on **msDS-KeyCredentialLink** attributes
- Used for managing **Windows Hello for Business** and FIDO2 keys at the domain level

## ⚠️ Security
> 🔴 **Tier 0 — Shadow Credentials risk**

- **Shadow Credentials** attack vector: a member can add alternative credentials to any domain account
- Documented attack: `Whisker`, `pyWhisker`
- Monitor modifications to the `msDS-KeyCredentialLink` attribute — Event ID 5136
- Minimize membership