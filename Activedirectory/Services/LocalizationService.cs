using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace SMADX.Services
{
    /// <summary>
    /// Service de gestion de la localisation de l'application
    /// </summary>
    public class LocalizationService : INotifyPropertyChanged
    {
        private static LocalizationService? _instance;
        private CultureInfo _currentCulture;
        private Dictionary<string, Dictionary<string, string>> _translations;

        public static LocalizationService Instance => _instance ??= new LocalizationService();

        public event PropertyChangedEventHandler? PropertyChanged;

        public CultureInfo CurrentCulture
        {
            get => _currentCulture;
            set
            {
                if (_currentCulture != value)
                {
                    _currentCulture = value;
                    CultureInfo.CurrentUICulture = value;

                    // Notifier TOUTES les propriétés individuellement pour forcer le rafraîchissement
                    OnPropertyChanged(nameof(CurrentCulture));

                    // Notifier toutes les clés de traduction
                    foreach (var key in _translations["fr-FR"].Keys)
                    {
                        OnPropertyChanged($"[{key}]");
                    }
                }
            }
        }

        public string this[string key]
        {
            get
            {
                var cultureName = _currentCulture.Name;
                if (_translations.TryGetValue(cultureName, out var cultureDictionary))
                {
                    if (cultureDictionary.TryGetValue(key, out var translation))
                    {
                        return translation;
                    }
                }

                // Fallback sur français si la clé n'existe pas
                if (_translations.TryGetValue("fr-FR", out var frenchDictionary))
                {
                    if (frenchDictionary.TryGetValue(key, out var translation))
                    {
                        return translation;
                    }
                }

                return $"[{key}]"; // Clé manquante
            }
        }

        private LocalizationService()
        {
            _currentCulture = CultureInfo.CurrentUICulture;
            _translations = new Dictionary<string, Dictionary<string, string>>();
            LoadTranslations();
        }

        private void LoadTranslations()
        {
            // Français
            _translations["fr-FR"] = new Dictionary<string, string>
            {
                // Menu Fichier
                ["Menu.File"] = "Fichier",
                ["Menu.File.NewDomain"] = "Nouveau domaine...",
                ["Menu.File.NewStructure"] = "Nouvelle structure (exemple)",
                ["Menu.File.Open"] = "Ouvrir...",
                ["Menu.File.Save"] = "Enregistrer...",
                ["Menu.File.ExportJson"] = "Exporter en JSON...",
                ["Menu.File.ExportPowerShell"] = "Exporter en PowerShell...",
                ["Menu.File.GenerateImportScript"] = "Générer le script d'import depuis AD...",
                ["Menu.File.Exit"] = "Quitter",

                // Menu Aide
                ["Menu.Help"] = "Aide",
                ["Menu.Help.About"] = "À propos...",

                // Menu Édition
                ["Menu.Edit"] = "Édition",
                ["Menu.Edit.Copy"] = "Copier",
                ["Menu.Edit.Paste"] = "Coller",
                ["Menu.Edit.Rename"] = "Renommer",
                ["Menu.Edit.Delete"] = "Supprimer",
                ["Menu.Edit.AddPSO"] = "Ajouter PSO",

                // Menu Affichage
                ["Menu.View"] = "Affichage",
                ["Menu.View.ExpandAll"] = "Développer tout",
                ["Menu.View.CollapseAll"] = "Réduire tout",
                ["Menu.View.Theme"] = "Thème",
                ["Menu.View.Theme.Dark"] = "Sombre",
                ["Menu.View.Theme.Light"] = "Clair",
                ["Menu.View.Language"] = "Langue",
                ["Menu.View.Language.French"] = "Français",
                ["Menu.View.Language.English"] = "English",

                // Menu Configuration
                ["Menu.Settings"] = "Configuration",
                ["Menu.Settings.ManageTiers"] = "Gérer les tiers...",

                // Toolbar
                ["Toolbar.OU"] = "OU",
                ["Toolbar.Container"] = "Conteneur",
                ["Toolbar.User"] = "Utilisateur",
                ["Toolbar.Group"] = "Groupe",
                ["Toolbar.Computer"] = "Ordinateur",
                ["Toolbar.GMSA"] = "GMSA",
                ["Toolbar.Policy"] = "Stratégie",
                ["Toolbar.PSO"] = "PSO",

                // Détails
                ["Details.Title"] = "Détails de l'objet",
                ["Details.Name"] = "Nom :",
                ["Details.Type"] = "Type :",
                ["Details.Tier"] = "Tier :",
                ["Details.DN"] = "Distinguished Name :",
                ["Details.Description"] = "Description (Markdown) :",
                ["Details.NoSelection"] = "Aucun objet sélectionné",
                ["Details.Actions"] = "Actions :",
                ["Details.Edit"] = "✏️ Éditer",
                ["Details.Preview"] = "📖 Prévisualiser",

                // Actions
                ["Action.Rename"] = "Renommer",
                ["Action.Copy"] = "Copier",
                ["Action.Delete"] = "Supprimer",
                ["Toolbar.Delete"] = "Supprimer",
                ["Toolbar.Relations"] = "Relations",
                ["Toolbar.Graph"] = "Graphe",
                ["Menu.View.ManageRelations"] = "🔗 Gérer les relations...",
                ["Menu.View.ShowGraph"] = "🕸️ Vue graphique des relations...",

                // TreeView
                ["TreeView.Title"] = "Structure Active Directory",

                // Types d'objets
                ["Type.Domain"] = "Domaine",
                ["Type.OU"] = "Unité d'organisation",
                ["Type.Container"] = "Conteneur",
                ["Type.User"] = "Utilisateur",
                ["Type.Group"] = "Groupe",
                ["Type.Computer"] = "Ordinateur",
                ["Type.GMSA"] = "Compte de service administré de groupe",
                ["Type.Policy"] = "Stratégie de groupe",
                ["Type.PSO"] = "Objet de paramètres de mot de passe",

                // Barre d'état
                ["Status.Ready"] = "Prêt",
                ["Status.DomainCreated"] = "Domaine {0} créé avec structure AD complète ({1})",
                ["Status.WithTiering"] = "avec tiering activé",
                ["Status.WithoutTiering"] = "sans tiering",
                ["Status.SampleLoaded"] = "Structure d'exemple chargée",
                ["Status.Saved"] = "Structure sauvegardée : {0}",
                ["Status.Loaded"] = "Structure chargée : {0}",
                ["Status.ExportedJson"] = "Exporté en JSON : {0}",
                ["Status.ExportedPowerShell"] = "Exporté en PowerShell : {0}",

                // Dialogues
                ["Dialog.NewDomain.Title"] = "Créer un nouveau domaine Active Directory",
                ["Dialog.NewDomain.Header"] = "Nouveau domaine Active Directory",
                ["Dialog.NewDomain.Description"] = "Créez une nouvelle structure Active Directory vierge avec tous les conteneurs, OUs et groupes par défaut.",
                ["Dialog.NewDomain.DomainName"] = "Nom du domaine (FQDN) :",
                ["Dialog.NewDomain.DomainPlaceholder"] = "ex: contoso.com, enterprise.local",
                ["Dialog.NewDomain.EnableTiering"] = "Activer le modèle de tiering (Tier 0/1/2)",
                ["Dialog.NewDomain.TieringTooltip"] = "Si coché, les utilisateurs, groupes et objets seront automatiquement affectés à des tiers selon leur rôle (Tier 0 : Comptes privilégiés, Tier 1 : Serveurs, Tier 2 : Postes de travail)",
                ["Dialog.NewDomain.StructurePreview"] = "Structure qui sera créée :",
                ["Dialog.NewDomain.Create"] = "Créer",
                ["Dialog.NewDomain.Cancel"] = "Annuler",
                ["Dialog.NewDomain.ValidationEmpty"] = "Le nom de domaine ne peut pas être vide.",
                ["Dialog.NewDomain.ValidationNoDot"] = "Le nom de domaine doit contenir au moins un point (ex: domaine.com)",
                ["Dialog.NewDomain.ValidationInvalidChars"] = "Le nom de domaine ne peut contenir que des lettres, chiffres, points et tirets.",

                // Fenêtre de configuration des tiers
                ["TierConfig.Title"] = "Configuration des Tiers",
                ["TierConfig.Header"] = "Gestion des Tiers de Sécurité",
                ["TierConfig.ExistingTiers"] = "Tiers existants",
                ["TierConfig.Level"] = "Niveau : {0}",
                ["TierConfig.Delete"] = "Supprimer",
                ["TierConfig.Reset"] = "Réinitialiser",
                ["TierConfig.AddNew"] = "Ajouter un nouveau tier",
                ["TierConfig.Name"] = "Nom :",
                ["TierConfig.NamePlaceholder"] = "Ex: Tier 3",
                ["TierConfig.Color"] = "Couleur (hex) :",
                ["TierConfig.ColorPlaceholder"] = "#RRGGBB",
                ["TierConfig.ColorSelect"] = "🎨 Sélectionner",
                ["TierConfig.ColorExamples"] = "Exemples : #FF0000 (rouge), #00FF00 (vert), #0000FF (bleu)",
                ["TierConfig.Description"] = "Description :",
                ["TierConfig.DescriptionPlaceholder"] = "Description du tier...",
                ["TierConfig.Priority"] = "Niveau de priorité :",
                ["TierConfig.PriorityHint"] = "Plus le niveau est bas, plus la priorité est élevée",
                ["TierConfig.Add"] = "Ajouter",
                ["TierConfig.Close"] = "Fermer",

                // Descriptions Markdown des objets AD
                ["Desc.Domain"] = "# Domaine {0}\n\nDomaine Active Directory créé avec SMAD-X.",
                ["Desc.Builtin"] = @"# Container Builtin

Contient les groupes de sécurité intégrés du domaine.

> **Note** : Ces groupes sont créés automatiquement lors de l'installation d'Active Directory.",
                ["Desc.Builtin.Administrators"] = "Membres de ce groupe ont un accès complet et illimité à l'ordinateur/domaine",
                ["Desc.Builtin.Users"] = "Les utilisateurs ne peuvent pas apporter des modifications accidentelles ou intentionnelles au système",
                ["Desc.Builtin.Guests"] = "Les invités ont les mêmes droits que les membres du groupe Utilisateurs par défaut",
                ["Desc.Builtin.ServerOperators"] = "Peuvent administrer les serveurs de domaine",
                ["Desc.Builtin.AccountOperators"] = "Peuvent administrer les comptes utilisateurs et groupes du domaine",
                ["Desc.Builtin.BackupOperators"] = "Peuvent sauvegarder et restaurer tous les fichiers sur les contrôleurs de domaine",
                ["Desc.Users"] = @"# Container Users

Container par défaut pour les utilisateurs et groupes du domaine.

> **Note** : Lors de la création d'un nouvel utilisateur sans spécifier d'emplacement, il est placé ici par défaut.",
                ["Desc.Users.Administrator"] = "Compte administrateur intégré pour administrer l'ordinateur/le domaine",
                ["Desc.Users.Guest"] = "Compte invité intégré pour l'accès invité à l'ordinateur/au domaine",
                ["Desc.Users.Krbtgt"] = @"# Compte de service Kerberos

Compte de service pour le centre de distribution de clés Kerberos.

> ⚠️ **Critique** : Ne jamais supprimer ou désactiver ce compte ! Il est essentiel au fonctionnement de Kerberos.",
                ["Desc.Users.DomainAdmins"] = "Administrateurs désignés du domaine",
                ["Desc.Users.DomainUsers"] = "Tous les utilisateurs du domaine",
                ["Desc.Users.DomainComputers"] = "Tous les postes de travail et serveurs joints au domaine",
                ["Desc.Users.DomainControllers"] = "Tous les contrôleurs de domaine du domaine",
                ["Desc.Users.SchemaAdmins"] = "Administrateurs du schéma désignés du domaine",
                ["Desc.Users.EnterpriseAdmins"] = "Administrateurs d'entreprise désignés de l'entreprise",
                ["Desc.Users.GroupPolicyCreatorOwners"] = "Membres de ce groupe peuvent modifier la stratégie de groupe pour le domaine",
                ["Desc.Users.ReadOnlyDCs"] = "Membres de ce groupe sont des contrôleurs de domaine en lecture seule dans le domaine",
                ["Desc.Users.DnsAdmins"] = "Groupe d'accès administratif DNS",
                ["Desc.Users.DefaultAccount"] = "Compte système géré par Windows",
                ["Desc.Users.WDAGUtilityAccount"] = "Compte utilisé par Windows Defender Application Guard",
                ["Desc.Users.CertPublishers"] = "Membres de ce groupe sont autorisés à publier des certificats dans l'annuaire",
                ["Desc.Users.RASandIAS"] = "Serveurs de ce groupe peuvent accéder aux propriétés d'accès à distance des utilisateurs",
                ["Desc.Users.AllowedRODCReplication"] = "Mots de passe de ce groupe peuvent être répliqués sur tous les RODC du domaine",
                ["Desc.Users.DeniedRODCReplication"] = "Mots de passe de ce groupe ne peuvent pas être répliqués sur les RODC du domaine",
                ["Desc.Users.DnsUpdateProxy"] = "Clients DNS autorisés à effectuer des mises à jour dynamiques DNS pour le compte d'autres clients",
                ["Desc.Users.CloneableDCs"] = "Membres de ce groupe qui sont des contrôleurs de domaine peuvent être clonés",
                ["Desc.Users.ProtectedUsers"] = "Membres de ce groupe bénéficient de protections supplémentaires contre les attaques de vol d'identifiants",
                ["Desc.Users.KeyAdmins"] = "Membres de ce groupe peuvent effectuer des actions administratives sur les objets clés du domaine",
                ["Desc.Users.EnterpriseKeyAdmins"] = "Membres de ce groupe peuvent effectuer des actions administratives sur les objets clés dans la forêt",
                ["Desc.Computers"] = @"# Container Computers

Container par défaut pour les ordinateurs joints au domaine.

> **Note** : Lors de la jonction d'un ordinateur au domaine sans spécifier d'emplacement, il est placé ici par défaut.",
                ["Desc.Computers.Workstation"] = @"# Poste de travail exemple

Ordinateur joint au domaine placé dans le container Computers par défaut.

> **Note** : Lors de la jonction d'un ordinateur au domaine, il est automatiquement placé dans ce container.",
                ["Desc.DomainControllersOU"] = @"# OU Domain Controllers

Unité d'organisation par défaut pour les contrôleurs de domaine.

> **Note** : Tous les contrôleurs de domaine sont automatiquement placés dans cette OU lors de leur promotion.",
                ["Desc.DomainControllers.DC"] = @"# Contrôleur de domaine principal

Premier contrôleur de domaine du domaine.

> **Rôles FSMO** : Ce DC héberge généralement tous les rôles FSMO lors de l'installation initiale :
> - Schema Master
> - Domain Naming Master
> - RID Master
> - PDC Emulator
> - Infrastructure Master",
                ["Desc.System"] = @"# Container System

Container système d'Active Directory contenant les objets de configuration et les paramètres système.

> **Note** : Ce container contient notamment les objets de configuration, les services, les stratégies de mots de passe, etc.",
                ["Desc.System.PSO"] = @"# Password Settings Container

Container système contenant tous les Password Settings Objects (PSO) du domaine.

> **Emplacement** : `CN=Password Settings Container,CN=System,DC=domaine,DC=com`

Les PSOs permettent de définir des politiques de mot de passe granulaires.",
                ["Desc.ForeignSecurityPrincipals"] = @"# Foreign Security Principals

Container pour les principaux de sécurité provenant de domaines externes approuvés.

> **Note** : Utilisé dans les environnements multi-domaines et multi-forêts.",

                ["Desc.GPOPoliciesContainer"] = @"# Container Policies (SYSVOL)

Ce container représente le dossier `SYSVOL\Policies` sur les contrôleurs de domaine.

Chaque GPO possède un sous-dossier identifié par son **GUID** dans :
```
\\<domaine>\SYSVOL\<domaine>\Policies\{GUID}
```",

                ["Desc.DefaultDomainPolicy"] = @"# 📋 Default Domain Policy

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
- **Version** : Incrémentée automatiquement à chaque modification",

                ["Desc.DefaultDomainControllersPolicy"] = @"# 📋 Default Domain Controllers Policy

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
- **Version** : Incrémentée automatiquement à chaque modification",

                // Fenêtre Relations
                ["Relations.Title"] = "🔗 Relations entre objets AD",
                ["Relations.TabMemberships"] = "👥 Appartenances aux groupes",
                ["Relations.TabGpoLinks"] = "📋 Liens GPO → OU",
                ["Relations.TabPsoLinks"] = "🔑 Sujets PSO",
                ["Relations.ColSource"] = "Source",
                ["Relations.ColType"] = "Type",
                ["Relations.ColTargetGroup"] = "→ Groupe",
                ["Relations.ColRelation"] = "Relation",
                ["Relations.ColGpo"] = "GPO",
                ["Relations.ColTargetOU"] = "→ OU cible",
                ["Relations.ColPso"] = "PSO",
                ["Relations.ColTargetPso"] = "→ Objet cible",
                ["Relations.LabelSource"] = "Objet source (user / groupe)",
                ["Relations.LabelTargetGroup"] = "Groupe cible",
                ["Relations.LabelGpo"] = "GPO",
                ["Relations.LabelTargetOU"] = "OU cible",
                ["Relations.LabelPso"] = "PSO",
                ["Relations.LabelTargetPso"] = "Objet cible (user, groupe)",
                ["Relations.ChooseObject"] = "Choisir un objet…",
                ["Relations.ChooseGroup"] = "Choisir un groupe…",
                ["Relations.ChooseGPO"] = "Choisir une GPO…",
                ["Relations.ChooseOU"] = "Choisir une OU…",
                ["Relations.ChoosePSO"] = "Choisir un PSO…",
                ["Relations.ChooseTarget"] = "Choisir un objet cible…",
                ["Relations.Add"] = "➕ Ajouter",
                ["Relations.Delete"] = "🗑️ Supprimer la sélection",
                ["Relations.Close"] = "Fermer",
                ["Relations.Status"] = "Relations : {0} appartenances  •  {1} liens GPO  •  {2} sujets PSO",

                // Fenêtre Graphe
                ["Graph.Title"] = "🕸️ Vue graphique des relations AD",
                ["Graph.FitView"] = "⊡ Ajuster",
                ["Graph.ZoomIn"] = "🔍+",
                ["Graph.ZoomOut"] = "🔍−",
                ["Graph.Relayout"] = "↺ Re-layout",
                ["Graph.Reload"] = "🔄 Recharger",
                ["Graph.Filters"] = "Filtres :",
                ["Graph.FilterMemberOf"] = "👥 MemberOf",
                ["Graph.FilterGpoLinks"] = "📋 GPO Links",
                ["Graph.FilterGpoInheritance"] = "⬇ Héritage GPO",
                ["Graph.FilterPso"] = "🔑 PSO",
                ["Graph.FilterHierarchy"] = "📂 Hiérarchie",
                ["Graph.FilterIsolated"] = "Isolés",
                ["Graph.FitViewTooltip"] = "Zoom pour tout voir",
                ["Graph.RelayoutTooltip"] = "Recalculer la disposition",
                ["Graph.ReloadTooltip"] = "Recharger depuis les données AD",
                ["Graph.Close"] = "✕ Fermer",

                // À propos
                ["About.Title"] = "À propos de SMAD-X",
                ["About.AppName"] = "SMAD-X",
                ["About.FullName"] = "Simuler, Documenter et Expérimenter Active Directory",
                ["About.Version"] = "Version 1.0.0",
                ["About.Description"] = "SMAD-X est un simulateur complet d'Active Directory conçu pour la formation, la documentation et l'expérimentation. Il permet de créer, visualiser et exporter des structures AD complètes sans nécessiter d'infrastructure réelle.",
                ["About.Features"] = "Fonctionnalités principales :",
                ["About.Feature1"] = "• Création de structures AD complètes avec containers, OUs, utilisateurs, groupes",
                ["About.Feature2"] = "• Modèle de tiering de sécurité (Tier 0/1/2) configurable",
                ["About.Feature3"] = "• Export en JSON et PowerShell pour documentation et déploiement",
                ["About.Feature4"] = "• Support multilingue (Français/English)",
                ["About.Feature5"] = "• Descriptions Markdown enrichies pour chaque objet",
                ["About.Feature6"] = "• Gestion des PSO (Password Settings Objects)",
                ["About.Copyright"] = "© 2025 SMAD-X - Tous droits réservés",
                ["About.Close"] = "Fermer",
            };

            // Anglais
            _translations["en-US"] = new Dictionary<string, string>
            {
                // Menu File
                ["Menu.File"] = "File",
                ["Menu.File.NewDomain"] = "New Domain...",
                ["Menu.File.NewStructure"] = "New Structure (sample)",
                ["Menu.File.Open"] = "Open...",
                ["Menu.File.Save"] = "Save...",
                ["Menu.File.ExportJson"] = "Export to JSON...",
                ["Menu.File.ExportPowerShell"] = "Export to PowerShell...",
                ["Menu.File.GenerateImportScript"] = "Generate AD Import Script...",
                ["Menu.File.Exit"] = "Exit",

                // Menu Help
                ["Menu.Help"] = "Help",
                ["Menu.Help.About"] = "About...",

                // Menu Edit
                ["Menu.Edit"] = "Edit",
                ["Menu.Edit.Copy"] = "Copy",
                ["Menu.Edit.Paste"] = "Paste",
                ["Menu.Edit.Rename"] = "Rename",
                ["Menu.Edit.Delete"] = "Delete",
                ["Menu.Edit.AddPSO"] = "Add PSO",

                // Menu View
                ["Menu.View"] = "View",
                ["Menu.View.ExpandAll"] = "Expand All",
                ["Menu.View.CollapseAll"] = "Collapse All",
                ["Menu.View.Theme"] = "Theme",
                ["Menu.View.Theme.Dark"] = "Dark",
                ["Menu.View.Theme.Light"] = "Light",
                ["Menu.View.Language"] = "Language",
                ["Menu.View.Language.French"] = "Français",
                ["Menu.View.Language.English"] = "English",

                // Menu Settings
                ["Menu.Settings"] = "Settings",
                ["Menu.Settings.ManageTiers"] = "Manage Tiers...",

                // Toolbar
                ["Toolbar.OU"] = "OU",
                ["Toolbar.Container"] = "Container",
                ["Toolbar.User"] = "User",
                ["Toolbar.Group"] = "Group",
                ["Toolbar.Computer"] = "Computer",
                ["Toolbar.GMSA"] = "GMSA",
                ["Toolbar.Policy"] = "Policy",
                ["Toolbar.PSO"] = "PSO",

                // Details
                ["Details.Title"] = "Object Details",
                ["Details.Name"] = "Name:",
                ["Details.Type"] = "Type:",
                ["Details.Tier"] = "Tier:",
                ["Details.DN"] = "Distinguished Name:",
                ["Details.Description"] = "Description (Markdown):",
                ["Details.NoSelection"] = "No object selected",
                ["Details.Actions"] = "Actions:",
                ["Details.Edit"] = "✏️ Edit",
                ["Details.Preview"] = "📖 Preview",

                // Actions
                ["Action.Rename"] = "Rename",
                ["Action.Copy"] = "Copy",
                ["Action.Delete"] = "Delete",
                ["Toolbar.Delete"] = "Delete",
                ["Toolbar.Relations"] = "Relations",
                ["Toolbar.Graph"] = "Graph",
                ["Menu.View.ManageRelations"] = "🔗 Manage Relations...",
                ["Menu.View.ShowGraph"] = "🕸️ Graph View...",

                // TreeView
                ["TreeView.Title"] = "Active Directory Structure",

                // Object types
                ["Type.Domain"] = "Domain",
                ["Type.OU"] = "Organizational Unit",
                ["Type.Container"] = "Container",
                ["Type.User"] = "User",
                ["Type.Group"] = "Group",
                ["Type.Computer"] = "Computer",
                ["Type.GMSA"] = "Group Managed Service Account",
                ["Type.Policy"] = "Group Policy",
                ["Type.PSO"] = "Password Settings Object",

                // Status bar
                ["Status.Ready"] = "Ready",
                ["Status.DomainCreated"] = "Domain {0} created with complete AD structure ({1})",
                ["Status.WithTiering"] = "with tiering enabled",
                ["Status.WithoutTiering"] = "without tiering",
                ["Status.SampleLoaded"] = "Sample structure loaded",
                ["Status.Saved"] = "Structure saved: {0}",
                ["Status.Loaded"] = "Structure loaded: {0}",
                ["Status.ExportedJson"] = "Exported to JSON: {0}",
                ["Status.ExportedPowerShell"] = "Exported to PowerShell: {0}",

                // Dialogs
                ["Dialog.NewDomain.Title"] = "Create new Active Directory Domain",
                ["Dialog.NewDomain.Header"] = "New Active Directory Domain",
                ["Dialog.NewDomain.Description"] = "Create a new blank Active Directory structure with all default containers, OUs and groups.",
                ["Dialog.NewDomain.DomainName"] = "Domain name (FQDN):",
                ["Dialog.NewDomain.DomainPlaceholder"] = "e.g.: contoso.com, enterprise.local",
                ["Dialog.NewDomain.EnableTiering"] = "Enable tiering model (Tier 0/1/2)",
                ["Dialog.NewDomain.TieringTooltip"] = "If checked, users, groups and objects will be automatically assigned to tiers according to their role (Tier 0: Privileged accounts, Tier 1: Servers, Tier 2: Workstations)",
                ["Dialog.NewDomain.StructurePreview"] = "Structure to be created:",
                ["Dialog.NewDomain.Create"] = "Create",
                ["Dialog.NewDomain.Cancel"] = "Cancel",
                ["Dialog.NewDomain.ValidationEmpty"] = "Domain name cannot be empty.",
                ["Dialog.NewDomain.ValidationNoDot"] = "Domain name must contain at least one dot (e.g.: domain.com)",
                ["Dialog.NewDomain.ValidationInvalidChars"] = "Domain name can only contain letters, digits, dots and hyphens.",

                // Tier Configuration Window
                ["TierConfig.Title"] = "Tier Configuration",
                ["TierConfig.Header"] = "Security Tiers Management",
                ["TierConfig.ExistingTiers"] = "Existing tiers",
                ["TierConfig.Level"] = "Level: {0}",
                ["TierConfig.Delete"] = "Delete",
                ["TierConfig.Reset"] = "Reset",
                ["TierConfig.AddNew"] = "Add new tier",
                ["TierConfig.Name"] = "Name:",
                ["TierConfig.NamePlaceholder"] = "E.g.: Tier 3",
                ["TierConfig.Color"] = "Color (hex):",
                ["TierConfig.ColorPlaceholder"] = "#RRGGBB",
                ["TierConfig.ColorSelect"] = "🎨 Select",
                ["TierConfig.ColorExamples"] = "Examples: #FF0000 (red), #00FF00 (green), #0000FF (blue)",
                ["TierConfig.Description"] = "Description:",
                ["TierConfig.DescriptionPlaceholder"] = "Tier description...",
                ["TierConfig.Priority"] = "Priority level:",
                ["TierConfig.PriorityHint"] = "Lower level means higher priority",
                ["TierConfig.Add"] = "Add",
                ["TierConfig.Close"] = "Close",

                // Markdown descriptions for AD objects
                ["Desc.Domain"] = "# Domain {0}\n\nActive Directory domain created with SMAD-X.",
                ["Desc.Builtin"] = @"# Builtin Container

Contains built-in security groups of the domain.

> **Note**: These groups are automatically created during Active Directory installation.",
                ["Desc.Builtin.Administrators"] = "Members of this group have complete and unrestricted access to the computer/domain",
                ["Desc.Builtin.Users"] = "Users are prevented from making accidental or intentional system-wide changes",
                ["Desc.Builtin.Guests"] = "Guests have the same rights as members of the Users group by default",
                ["Desc.Builtin.ServerOperators"] = "Can administer domain servers",
                ["Desc.Builtin.AccountOperators"] = "Can administer user accounts and groups in the domain",
                ["Desc.Builtin.BackupOperators"] = "Can back up and restore all files on domain controllers",
                ["Desc.Users"] = @"# Users Container

Default container for domain users and groups.

> **Note**: When creating a new user without specifying a location, it is placed here by default.",
                ["Desc.Users.Administrator"] = "Built-in administrator account for administering the computer/domain",
                ["Desc.Users.Guest"] = "Built-in guest account for guest access to the computer/domain",
                ["Desc.Users.Krbtgt"] = @"# Kerberos Service Account

Service account for the Kerberos Key Distribution Center.

> ⚠️ **Critical**: Never delete or disable this account! It is essential for Kerberos operation.",
                ["Desc.Users.DomainAdmins"] = "Designated administrators of the domain",
                ["Desc.Users.DomainUsers"] = "All domain users",
                ["Desc.Users.DomainComputers"] = "All workstations and servers joined to the domain",
                ["Desc.Users.DomainControllers"] = "All domain controllers in the domain",
                ["Desc.Users.SchemaAdmins"] = "Designated schema administrators of the domain",
                ["Desc.Users.EnterpriseAdmins"] = "Designated enterprise administrators of the enterprise",
                ["Desc.Users.GroupPolicyCreatorOwners"] = "Members of this group can modify group policy for the domain",
                ["Desc.Users.ReadOnlyDCs"] = "Members of this group are Read-Only Domain Controllers in the domain",
                ["Desc.Users.DnsAdmins"] = "DNS administrative access group",
                ["Desc.Users.DefaultAccount"] = "System-managed account used by Windows",
                ["Desc.Users.WDAGUtilityAccount"] = "Account used by Windows Defender Application Guard",
                ["Desc.Users.CertPublishers"] = "Members of this group are permitted to publish certificates to the directory",
                ["Desc.Users.RASandIAS"] = "Servers in this group can access remote access properties of users",
                ["Desc.Users.AllowedRODCReplication"] = "Passwords for members of this group can be replicated to all RODCs in the domain",
                ["Desc.Users.DeniedRODCReplication"] = "Passwords for members of this group cannot be replicated to any RODC in the domain",
                ["Desc.Users.DnsUpdateProxy"] = "DNS clients permitted to perform dynamic DNS updates on behalf of other clients",
                ["Desc.Users.CloneableDCs"] = "Members of this group that are domain controllers may be cloned",
                ["Desc.Users.ProtectedUsers"] = "Members of this group are afforded additional protections against credential theft attacks",
                ["Desc.Users.KeyAdmins"] = "Members of this group can perform administrative actions on key objects within the domain",
                ["Desc.Users.EnterpriseKeyAdmins"] = "Members of this group can perform administrative actions on key objects within the forest",
                ["Desc.Computers"] = @"# Computers Container

Default container for computers joined to the domain.

> **Note**: When joining a computer to the domain without specifying a location, it is placed here by default.",
                ["Desc.Computers.Workstation"] = @"# Sample Workstation

Computer joined to the domain placed in the default Computers container.

> **Note**: When joining a computer to the domain, it is automatically placed in this container.",
                ["Desc.DomainControllersOU"] = @"# Domain Controllers OU

Default organizational unit for domain controllers.

> **Note**: All domain controllers are automatically placed in this OU when promoted.",
                ["Desc.DomainControllers.DC"] = @"# Primary Domain Controller

First domain controller of the domain.

> **FSMO Roles**: This DC typically hosts all FSMO roles during initial installation:
> - Schema Master
> - Domain Naming Master
> - RID Master
> - PDC Emulator
> - Infrastructure Master",
                ["Desc.System"] = @"# System Container

Active Directory system container containing configuration objects and system settings.

> **Note**: This container holds configuration objects, services, password policies, etc.",
                ["Desc.System.PSO"] = @"# Password Settings Container

System container holding all Password Settings Objects (PSO) of the domain.

> **Location**: `CN=Password Settings Container,CN=System,DC=domain,DC=com`

PSOs allow defining granular password policies.",
                ["Desc.ForeignSecurityPrincipals"] = @"# Foreign Security Principals

Container for security principals from external trusted domains.

> **Note**: Used in multi-domain and multi-forest environments.",

                ["Desc.GPOPoliciesContainer"] = @"# Policies Container (SYSVOL)

This container represents the `SYSVOL\Policies` folder on domain controllers.

Each GPO has a subfolder identified by its **GUID** under:
```
\\<domain>\SYSVOL\<domain>\Policies\{GUID}
```",

                ["Desc.DefaultDomainPolicy"] = @"# 📋 Default Domain Policy

> ⚠️ **FIXED GUID**: `{31B2F340-016D-11D2-945F-00C04FB984F9}`

This GPO is **automatically created** when the first domain controller is promoted and applies to the **entire domain**.

## Default Managed Settings

### 🔑 Password Policy (Computer Configuration)
| Setting | Default Value |
|---|---|
| Minimum password length | 7 characters |
| Password complexity | Enabled |
| Password history | 24 passwords |
| Maximum password age | 42 days |
| Minimum password age | 1 day |

### 🔒 Account Lockout Policy
| Setting | Default Value |
|---|---|
| Lockout threshold | 0 (disabled) |
| Lockout duration | Not defined |

### 🎫 Kerberos Policy
| Setting | Default Value |
|---|---|
| Ticket lifetime | 10 hours |
| Service ticket lifetime | 600 minutes |
| Maximum clock skew | 5 minutes |

## ⚠️ Important Warnings
> ❌ **Never delete** this GPO — it breaks security policies for the entire domain.

> ⚠️ **Never disable** the link of this GPO on the domain.

> ✅ **Best practice**: Only modify password and Kerberos settings here. All other configuration should be done in dedicated GPOs.

## Technical Information
- **DN**: `CN={31B2F340-016D-11D2-945F-00C04FB984F9},CN=Policies,CN=System,DC=domain,DC=com`
- **SYSVOL**: `\\domain\SYSVOL\domain\Policies\{31B2F340-016D-11D2-945F-00C04FB984F9}`
- **Version**: Auto-incremented on each modification",

                ["Desc.DefaultDomainControllersPolicy"] = @"# 📋 Default Domain Controllers Policy

> ⚠️ **FIXED GUID**: `{6AC1786C-016F-11D2-945F-00C04fB984F9}`

This GPO is **automatically created** with the domain and applies only to the **Domain Controllers OU**.

## Default Managed Settings

### 🛡️ User Rights Assignment
| Setting | Default Value |
|---|---|
| Log on locally | Administrators, Account Operators, Backup Operators, Print Operators, Server Operators |
| Access network | Authenticated Users, Everyone |
| Shut down system | Administrators, Account Operators, Backup Operators, Print Operators, Server Operators |
| Manage audit and security log | Administrators |

### 🔍 Audit Policy
| Category | Default Value |
|---|---|
| Account logon | Success, Failure |
| Account management | Success |
| Directory service access | Success |
| Logon events | Success, Failure |
| Policy change | Success |
| Privilege use | Failure |
| Process tracking | None |
| System events | Success |

### 🔒 Security Options
- SMB server signing required: **Enabled**
- NTLMv2 only: **Configured**

## ⚠️ Important Warnings
> ❌ **Never delete** this GPO — it ensures minimum security for domain controllers.

> ⚠️ **Never disable** the link of this GPO on the Domain Controllers OU.

> ✅ **Best practice**: Add complementary hardening GPOs (CIS Benchmarks, etc.) rather than modifying this one.

> ℹ️ **RODC**: Read-Only Domain Controllers also inherit this GPO via the Domain Controllers OU.

## Technical Information
- **DN**: `CN={6AC1786C-016F-11D2-945F-00C04fB984F9},CN=Policies,CN=System,DC=domain,DC=com`
- **SYSVOL**: `\\domain\SYSVOL\domain\Policies\{6AC1786C-016F-11D2-945F-00C04fB984F9}`
- **Version**: Auto-incremented on each modification",

                // Relations Window
                ["Relations.Title"] = "🔗 AD Object Relations",
                ["Relations.TabMemberships"] = "👥 Group Memberships",
                ["Relations.TabGpoLinks"] = "📋 GPO Links → OU",
                ["Relations.TabPsoLinks"] = "🔑 PSO Subjects",
                ["Relations.ColSource"] = "Source",
                ["Relations.ColType"] = "Type",
                ["Relations.ColTargetGroup"] = "→ Group",
                ["Relations.ColRelation"] = "Relation",
                ["Relations.ColGpo"] = "GPO",
                ["Relations.ColTargetOU"] = "→ Target OU",
                ["Relations.ColPso"] = "PSO",
                ["Relations.ColTargetPso"] = "→ Target Object",
                ["Relations.LabelSource"] = "Source object (user / group)",
                ["Relations.LabelTargetGroup"] = "Target group",
                ["Relations.LabelGpo"] = "GPO",
                ["Relations.LabelTargetOU"] = "Target OU",
                ["Relations.LabelPso"] = "PSO",
                ["Relations.LabelTargetPso"] = "Target object (user, group)",
                ["Relations.ChooseObject"] = "Choose an object…",
                ["Relations.ChooseGroup"] = "Choose a group…",
                ["Relations.ChooseGPO"] = "Choose a GPO…",
                ["Relations.ChooseOU"] = "Choose an OU…",
                ["Relations.ChoosePSO"] = "Choose a PSO…",
                ["Relations.ChooseTarget"] = "Choose a target object…",
                ["Relations.Add"] = "➕ Add",
                ["Relations.Delete"] = "🗑️ Delete selection",
                ["Relations.Close"] = "Close",
                ["Relations.Status"] = "Relations: {0} memberships  •  {1} GPO links  •  {2} PSO subjects",

                // Graph Window
                ["Graph.Title"] = "🕸️ AD Relations Graph View",
                ["Graph.FitView"] = "⊡ Fit",
                ["Graph.ZoomIn"] = "🔍+",
                ["Graph.ZoomOut"] = "🔍−",
                ["Graph.Relayout"] = "↺ Re-layout",
                ["Graph.Reload"] = "🔄 Reload",
                ["Graph.Filters"] = "Filters:",
                ["Graph.FilterMemberOf"] = "👥 MemberOf",
                ["Graph.FilterGpoLinks"] = "📋 GPO Links",
                ["Graph.FilterGpoInheritance"] = "⬇ GPO Inheritance",
                ["Graph.FilterPso"] = "🔑 PSO",
                ["Graph.FilterHierarchy"] = "📂 Hierarchy",
                ["Graph.FilterIsolated"] = "Isolated",
                ["Graph.FitViewTooltip"] = "Zoom to fit all",
                ["Graph.RelayoutTooltip"] = "Recalculate layout",
                ["Graph.ReloadTooltip"] = "Reload from AD data",
                ["Graph.Close"] = "✕ Close",

                // About
                ["About.Title"] = "About SMAD-X",
                ["About.AppName"] = "SMAD-X",
                ["About.FullName"] = "Simulate, Document and eXperiment Active Directory",
                ["About.Version"] = "Version 1.0.0",
                ["About.Description"] = "SMAD-X is a comprehensive Active Directory simulator designed for training, documentation and experimentation. It allows you to create, visualize and export complete AD structures without requiring a real infrastructure.",
                ["About.Features"] = "Key features:",
                ["About.Feature1"] = "• Creation of complete AD structures with containers, OUs, users, groups",
                ["About.Feature2"] = "• Configurable security tiering model (Tier 0/1/2)",
                ["About.Feature3"] = "• Export to JSON and PowerShell for documentation and deployment",
                ["About.Feature4"] = "• Multilingual support (Français/English)",
                ["About.Feature5"] = "• Rich Markdown descriptions for each object",
                ["About.Feature6"] = "• PSO (Password Settings Objects) management",
                ["About.Copyright"] = "© 2025 SMAD-X - All rights reserved",
                ["About.Close"] = "Close",
            };
        }

        public void SetLanguage(string cultureName)
        {
            CurrentCulture = new CultureInfo(cultureName);
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
