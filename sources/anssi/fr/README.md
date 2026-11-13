# ANSSI — Points de contrôle Active Directory

> [Toutes les sources](../../ReadmeFR.md) · English: [../en/README.md](../en/README.md)

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais

## Fiches

| Fiche | Titre | Niveau ANSSI | Cité par SMAD-X |
|---|---|---|---|
| [ANSSI-PA-099](ANSSI-PA-099.md) | Recommandations pour l’administration sécurisée des SI reposant sur AD | — | PRIV-05 · ACL-02 · TIER-01 · TIER-02 · TIER-03 · TIER-04 · TIER-05 · TIER-06 · TIER-07 · TIER-08 · TIER-09 |
| [info_spn_present](info_spn_present.md) | Comptes utilisateurs avec SPN | — | KRB-02 |
| [info_user_accounts_dormant](info_user_accounts_dormant.md) | Comptes actifs inutilisés | — | ACC-02 |
| [vuln_delegation_a2d2](vuln_delegation_a2d2.md) | Délégation d'authentification contrainte vers un service privilégié | 1 | DLG-03 |
| [vuln_delegation_t2a4d](vuln_delegation_t2a4d.md) | Délégation d'authentification contrainte avec transition de protocole vers un service privilégié | 1 | DLG-02 |
| [vuln_delegation_t4d](vuln_delegation_t4d.md) | Délégation d'authentification non contrainte | 1 | DLG-01 |
| [vuln_dnsadmins](vuln_dnsadmins.md) | Permissions dangereuses sur le groupe DnsAdmins | 1 | PRIV-04 |
| [vuln_dont_expire](vuln_dont_expire.md) | Comptes dont le mot de passe n'expire jamais | 2 | PWD-02 |
| [vuln_dont_expire_priv](vuln_dont_expire_priv.md) | Comptes privilégiés dont le mot de passe n'expire jamais | 1 | PWD-01 |
| [vuln_group_loop](vuln_group_loop.md) | Présences de boucles d'imbrication de groupes | 4 | GRP-01 |
| [vuln_guest](vuln_guest.md) | Compte Invité actif | 2 | ACC-01 |
| [vuln_kerberos_properties_preauth](vuln_kerberos_properties_preauth.md) | Comptes sans préauthentification Kerberos | 2 | KRB-04 |
| [vuln_kerberos_properties_preauth_priv](vuln_kerberos_properties_preauth_priv.md) | Comptes privilégiés sans préauthentification Kerberos | 1 | KRB-03 |
| [vuln_krbtgt](vuln_krbtgt.md) | Mot de passe du compte krbtgt inchangé depuis plus d'un an | 2 | KRB-05 |
| [vuln_owner](vuln_owner.md) | Objets ayant un propriétaire inadapté | 3 | ACL-01 |
| [vuln_password_change_dc_no_change](vuln_password_change_dc_no_change.md) | Contrôleurs de domaine dont le mot de passe de compte d'ordinateur est inchangé depuis plus de 45 jours | 1 | PWD-05 |
| [vuln_password_change_msa_no_change_90](vuln_password_change_msa_no_change_90.md) | Comptes de service managés dont le mot de passe de compte est inchangé depuis plus de 90 jours | 2 | PWD-07 |
| [vuln_password_change_priv](vuln_password_change_priv.md) | Comptes privilégiés dont le mot de passe est inchangé depuis plus de 3 ans | 1 | PWD-04 |
| [vuln_password_change_server_no_change_90](vuln_password_change_server_no_change_90.md) | Serveurs dont le mot de passe de compte d'ordinateur est inchangé depuis plus de 90 jours | 2 | PWD-06 |
| [vuln_permissions_adminsdholder](vuln_permissions_adminsdholder.md) | Permissions dangereuses sur l'objet adminSDHolder | 1, 2 | ACL-03 |
| [vuln_permissions_dc](vuln_permissions_dc.md) | Permissions dangereuses sur les objets contrôleurs de domaine (chemins de contrôle) | 1, 2 | ACL-03 |
| [vuln_permissions_gpo_container_priv](vuln_permissions_gpo_container_priv.md) | Permissions dangereuses sur des conteneurs d'objets privilégiés (chemins de contrôle) | 2 | ACL-02 |
| [vuln_permissions_gpo_priv](vuln_permissions_gpo_priv.md) | Permissions dangereuses sur les objets de GPO s'appliquant aux membres des groupes privilégiés (chemins de contrôle) | 1 | TIER-02 |
| [vuln_permissions_naming_context](vuln_permissions_naming_context.md) | Permissions dangereuses sur la racine des naming contexts (chemins de contrôle) | 1, 2 | ACL-03 |
| [vuln_privileged_members_no_admincount](vuln_privileged_members_no_admincount.md) | Comptes membres de groupes privilégiés ayant un attribut adminCount vide ou valant 0 | 2 | PRIV-01 |
| [vuln_privileged_members_password](vuln_privileged_members_password.md) | Comptes membres de groupes privilégiés avec une mauvaise politique de mot de passe | 2 | PWD-09 |
| [vuln_privileged_members_perm](vuln_privileged_members_perm.md) | Permissions dangereuses sur des membres de groupes privilégiés (chemins de contrôle) | 1, 2 | ACL-02 · TIER-03 |
| [vuln_protected_users](vuln_protected_users.md) | Comptes privilégiés non membres du groupe Protected Users | 3 | DLG-04 |
| [vuln_silo_priv](vuln_silo_priv.md) | Membres des groupes privilégiés hors silo d'authentification | 4 | TIER-04 · TIER-09 |
| [vuln_spn_priv](vuln_spn_priv.md) | Comptes privilégiés avec SPN | 1 | KRB-01 |
| [vuln_user_accounts_machineaccountquota](vuln_user_accounts_machineaccountquota.md) | Ajout de machines au domaine non limité | 4 | ACC-03 |
| [warning_admincount](warning_admincount.md) | Comptes ayant la propriété adminCount | 1, 3 | PRIV-02 |
| [warning_privileged_members_not_empty](warning_privileged_members_not_empty.md) | Groupes privilégiés non vides | 3 | PRIV-03 |

Le PDF de chaque fiche est à côté d'elle, sous le même nom, une fois généré par `dotnet run --project tools/SmadxSources -- pdf`.
