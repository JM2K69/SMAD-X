# 👥 Protected Users Group

**Universal** group created automatically (Windows Server 2012 R2+).

## Role
- Members automatically benefit from **enhanced Kerberos protections**:
  - No Kerberos delegation
  - No RC4 for authentication (Kerberos AES only)
  - No NTLM credential caching
  - TGT reduced to 4 hours (non-renewable)
  - No NTLM, Digest, CredSSP authentication

## ⚠️ Security
> 🟢 **Best practice — add sensitive accounts**

- **Add** all Tier 0 and Tier 1 administration accounts
- **Test** before adding service accounts — incompatible with some legacy protocols
- **Do not add**: service accounts using NTLM, Kerberos delegation, or plaintext password authentication
- Protects against **Pass-the-Hash**, **Pass-the-Ticket**, **Overpass-the-Hash**