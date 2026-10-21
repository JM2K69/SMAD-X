# 📋 Default Domain Policy

> ⚠️ **GUID FIXE** : `{31B2F340-016D-11D2-945F-00C04FB984F9}`

Cette GPO est **créée automatiquement** lors de la promotion du premier contrôleur de domaine et s'applique à l'**ensemble du domaine**.

## Paramètres par défaut gérés

### 🔑 Politique de mots de passe (Computer Configuration)
| Paramètre | Valeur par défaut |
|---|---|
| Longueur minimale | 7 caractères |
| Complexité | Activée |
| Historique | 24 mots de passe |
| Âge maximum | 42 jours |
| Âge minimum | 1 jour |

### 🔒 Politique de verrouillage de compte
| Paramètre | Valeur par défaut |
|---|---|
| Seuil de verrouillage | 0 (désactivé) |
| Durée de verrouillage | Non défini |

### 🎫 Politique Kerberos
| Paramètre | Valeur par défaut |
|---|---|
| Durée de vie du ticket | 10 heures |
| Durée de vie du ticket de service | 600 minutes |
| Tolérance d'horloge max | 5 minutes |

## ⚠️ Avertissements importants
> ❌ **Ne jamais supprimer** cette GPO — cela casse les politiques de sécurité de tout le domaine.

> ⚠️ **Ne pas désactiver** le lien de cette GPO sur le domaine.

> ✅ **Bonne pratique** : Ne modifier que les paramètres de mot de passe et Kerberos ici. Toute autre configuration doit être faite dans des GPOs dédiées.

## Informations techniques
- **DN** : `CN={31B2F340-016D-11D2-945F-00C04FB984F9},CN=Policies,CN=System,DC=domaine,DC=com`
- **SYSVOL** : `\\domaine\SYSVOL\domaine\Policies\{31B2F340-016D-11D2-945F-00C04FB984F9}`
- **Version** : Incrémentée automatiquement à chaque modification