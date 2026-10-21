# 📋 Default Domain Controllers Policy

> ⚠️ **GUID FIXE** : `{6AC1786C-016F-11D2-945F-00C04fB984F9}`

Cette GPO est **créée automatiquement** avec le domaine et s'applique uniquement à l'**OU Domain Controllers**.

## Paramètres par défaut gérés

### 🛡️ Droits utilisateur (User Rights Assignment)
| Paramètre | Valeur par défaut |
|---|---|
| Ouvrir une session localement | Administrators, Account Operators, Backup Operators, Print Operators, Server Operators |
| Accès réseau | Authenticated Users, Everyone |
| Fermer le système | Administrators, Account Operators, Backup Operators, Print Operators, Server Operators |
| Gérer les journaux d'audit | Administrators |

### 🔍 Audit Policy
| Catégorie | Valeur par défaut |
|---|---|
| Connexion de compte | Succès, Échec |
| Gestion des comptes | Succès |
| Accès au service d'annuaire | Succès |
| Connexion | Succès, Échec |
| Changements de politique | Succès |
| Utilisation des privilèges | Échec |
| Suivi des processus | Aucun |
| Événements système | Succès |

### 🔒 Options de sécurité
- Signature SMB requise côté serveur : **Activé**
- NTLMv2 uniquement : **Configuré**

## ⚠️ Avertissements importants
> ❌ **Ne jamais supprimer** cette GPO — elle garantit la sécurité minimale des contrôleurs de domaine.

> ⚠️ **Ne pas désactiver** le lien de cette GPO sur l'OU Domain Controllers.

> ✅ **Bonne pratique** : Ajouter des GPOs complémentaires pour le durcissement (CIS Benchmarks, ANSSI) plutôt que de modifier celle-ci.

> ℹ️ **RODC** : Les Read-Only Domain Controllers héritent également de cette GPO via l'OU Domain Controllers.

## Informations techniques
- **DN** : `CN={6AC1786C-016F-11D2-945F-00C04fB984F9},CN=Policies,CN=System,DC=domaine,DC=com`
- **SYSVOL** : `\\domaine\SYSVOL\domaine\Policies\{6AC1786C-016F-11D2-945F-00C04fB984F9}`
- **Version** : Incrémentée automatiquement à chaque modification