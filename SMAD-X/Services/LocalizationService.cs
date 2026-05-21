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
                ["Menu.View.ManageRelations"] = "Gérer les relations...",
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
                ["Details.MemberOf"] = "👥 Membre de (groupes)",
                ["Details.LinkedGPOs"] = "📋 GPO liées",
                ["Details.PSOAppliesTo"] = "🔑 Appliqué à",

                // Actions
                ["Action.Rename"] = "Renommer",
                ["Action.Copy"] = "Copier",
                ["Action.Delete"] = "Supprimer",
                ["Action.Manage"] = "⚙ Gérer",
                ["Toolbar.Delete"] = "Supprimer",

                // Context Menu
                ["ContextMenu.AddOU"] = "Ajouter une OU",
                ["ContextMenu.AddContainer"] = "Ajouter un Container",
                ["ContextMenu.AddUser"] = "Ajouter un Utilisateur",
                ["ContextMenu.AddGroup"] = "Ajouter un Groupe",
                ["ContextMenu.AddComputer"] = "Ajouter un Ordinateur",
                ["ContextMenu.AddGMSA"] = "Ajouter un GMSA",
                ["ContextMenu.AddPolicy"] = "Ajouter une GPO",
                ["ContextMenu.AddPSO"] = "Ajouter un PSO",
                ["ContextMenu.Rename"] = "Renommer",
                ["ContextMenu.Copy"] = "Copier",
                ["ContextMenu.Paste"] = "Coller",
                ["ContextMenu.Delete"] = "Supprimer",
                ["ContextMenu.ManageRelations"] = "Gérer les relations",
                ["ContextMenu.AddToGroup"] = "Ajouter au groupe",
                ["ContextMenu.AddMember"] = "Ajouter des membres",
                ["ContextMenu.AddGroupToGroup"] = "Ajouter à un groupe (imbrication)",
                ["Toolbar.Relations"] = "Relations",
                ["Toolbar.Graph"] = "Graphe",

                // Tooltips toolbar
                ["Tooltip.AddOU"] = "Ajouter une Unité Organisationnelle",
                ["Tooltip.AddContainer"] = "Ajouter un Conteneur",
                ["Tooltip.AddUser"] = "Ajouter un Utilisateur",
                ["Tooltip.AddGroup"] = "Ajouter un Groupe",
                ["Tooltip.AddComputer"] = "Ajouter un Ordinateur",
                ["Tooltip.AddGMSA"] = "Ajouter un GMSA",
                ["Tooltip.AddPolicy"] = "Ajouter une Stratégie de Groupe (GPO)",
                ["Tooltip.AddPSO"] = "Ajouter un Password Settings Object",
                ["Tooltip.Delete"] = "Supprimer l'objet sélectionné",
                ["Tooltip.Relations"] = "Gérer les relations entre objets",
                ["Tooltip.Graph"] = "Afficher la vue graphique des relations",
                ["Tooltip.ExportPNG"] = "Exporter l'arborescence en image PNG",
                ["Tooltip.ClearSearch"] = "Effacer la recherche",
                ["Menu.View.ShowGraph"] = "🕸️ Vue graphique des relations...",

                // TreeView
                ["TreeView.Title"] = "Structure Active Directory",
                ["TreeView.SearchPlaceholder"] = "Rechercher (nom, type, description)...",

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
                ["Desc.Users.Administrator"] = @"# 👤 Compte Administrator

Compte administrateur **intégré** créé automatiquement lors de la promotion du domaine.

## Rôle
- Accès complet et illimité à toutes les ressources du domaine
- Seul compte qui ne peut pas être verrouillé par la politique de verrouillage de compte
- Membre permanent du groupe **Domain Admins** et **Administrators**

## Appartenance par défaut
| Groupe | Portée |
|---|---|
| Administrators | Domaine local |
| Domain Admins | Global |
| Group Policy Creator Owners | Global |
| Schema Admins (forêt racine) | Universel |
| Enterprise Admins (forêt racine) | Universel |

## ⚠️ Sécurité
> 🔴 **Tier 0 — Compte critique**

- **Renommer** ce compte pour ne pas exposer le nom prévisible `Administrator`
- **Désactiver** ce compte en production et créer un compte nommé dédié pour l'administration
- Activer la **politique de verrouillage** pour les attaques par force brute (via Fine-Grained Password Policy)
- Ce compte est une cible privilégiée des attaques **Pass-the-Hash** et **Mimikatz**
- Surveiller avec des alertes sur toute ouverture de session de ce compte
- Activer **Protected Users** (Windows Server 2012 R2+) ne s'applique pas à Administrator — gérer manuellement",

                ["Desc.Users.Guest"] = @"# 👤 Compte Guest

Compte invité **intégré** créé automatiquement lors de la promotion du domaine.

## Rôle
- Permet un accès anonyme et limité au domaine sans mot de passe
- Droits équivalents au groupe **Guests** (très restreints)
- Ne peut pas changer son mot de passe ni accéder aux paramètres du système

## Appartenance par défaut
| Groupe | Portée |
|---|---|
| Guests | Domaine local |
| Domain Guests | Global |

## ⚠️ Sécurité
> 🔴 **Désactiver immédiatement en production**

- Ce compte est **désactivé par défaut** depuis Windows Server 2008 — vérifier qu'il le reste
- Ne jamais activer ce compte dans un environnement de production
- Vecteur d'attaque classique pour l'accès non authentifié
- Surveiller toute tentative d'activation ou d'utilisation de ce compte (Event ID 4722, 4624)",

                ["Desc.Users.Krbtgt"] = @"# 🔑 Compte krbtgt

Compte de service **Kerberos Key Distribution Center (KDC)** créé automatiquement lors de la promotion du domaine.

## Rôle
- Utilisé exclusivement par le service **KDC** pour signer et chiffrer les tickets Kerberos (TGT)
- Son mot de passe sert de clé de chiffrement pour tous les **Ticket Granting Tickets** du domaine
- Désactivé par défaut — ne jamais activer

## Appartenance par défaut
| Groupe | Portée |
|---|---|
| Domain Users | Global |

## ⚠️ Sécurité
> 🔴 **Tier 0 — Compte ultra-critique**

- **Ne jamais supprimer** ce compte — cela rend le domaine inutilisable
- **Ne jamais activer** ce compte interactivement
- La compromission de ce compte permet de créer des **Golden Tickets** (attaque Mimikatz)
- **Renouveler le mot de passe DEUX FOIS** après une compromission suspectée (deux resets consécutifs nécessaires pour invalider les tickets existants)
- Renouvellement préventif recommandé **tous les 180 jours** en production
- Surveiller les modifications du mot de passe (Event ID 4723, 4724)
- Depuis Windows Server 2016 : utiliser le **RODC krbtgt account** séparé pour les RODCs",

                ["Desc.Users.DomainAdmins"] = @"# 👥 Groupe Domain Admins

Groupe **global** créé automatiquement lors de la promotion du domaine.

## Rôle
- Administrateurs désignés du domaine
- Membres automatiquement ajoutés au groupe **Administrators** de chaque machine jointe au domaine
- Contrôle total sur tous les objets du domaine

## Appartenance par défaut
Membres : **Administrator**

## ⚠️ Sécurité
> 🔴 **Tier 0 — Groupe ultra-privilégié**

- Limiter le nombre de membres au strict minimum (principe du moindre privilège)
- Ne jamais utiliser un compte Domain Admin pour les tâches quotidiennes
- Utiliser des comptes dédiés à l'administration (PAW — Privileged Access Workstation)
- Activer la surveillance de toute modification de ce groupe (Event ID 4728, 4729)
- Les membres de Domain Admins sont membres de **Administrators** sur **toutes** les machines du domaine — vecteur de mouvement latéral majeur",

                ["Desc.Users.DomainUsers"] = @"# 👥 Groupe Domain Users

Groupe **global** créé automatiquement lors de la promotion du domaine.

## Rôle
- Contient **tous** les comptes utilisateurs du domaine (ajout automatique)
- Utilisé comme groupe de base pour les permissions sur les ressources partagées
- Membre du groupe **Users** local de chaque machine jointe

## ⚠️ Sécurité
> 🟢 **Tier 2 — Groupe standard**

- Éviter d'accorder des permissions élevées à ce groupe — il contient tous les utilisateurs
- Surveiller les ressources accessibles par ce groupe (audit des partages, des GPOs)
- Utile pour définir des politiques s'appliquant à tous les utilisateurs du domaine",

                ["Desc.Users.DomainComputers"] = @"# 👥 Groupe Domain Computers

Groupe **global** créé automatiquement lors de la promotion du domaine.

## Rôle
- Contient **tous** les ordinateurs joints au domaine (hors contrôleurs de domaine)
- Ajout automatique lors de la jonction d'un poste au domaine

## ⚠️ Sécurité
> 🟢 **Tier 2 — Groupe standard**

- Peut être utilisé pour appliquer des politiques à l'ensemble des postes
- Surveiller les ajouts inattendus (jonction de machines non autorisées)",

                ["Desc.Users.DomainControllers"] = @"# 👥 Groupe Domain Controllers

Groupe **global** créé automatiquement lors de la promotion du domaine.

## Rôle
- Contient **tous** les contrôleurs de domaine (DC) du domaine
- Ajout automatique lors de la promotion d'un serveur en DC

## ⚠️ Sécurité
> 🔴 **Tier 0 — Groupe critique**

- Surveiller toute modification (ajout/suppression de DC) — Event ID 4728, 4729
- Un contrôleur de domaine compromis compromet l'intégralité du domaine",

                ["Desc.Users.SchemaAdmins"] = @"# 👥 Groupe Schema Admins

Groupe **universel** créé automatiquement. Existe uniquement dans le **domaine racine de la forêt**.

## Rôle
- Seuls membres autorisés à modifier le **schéma Active Directory** (structure des classes et attributs)
- Modifications du schéma sont irréversibles et s'appliquent à toute la forêt

## Appartenance par défaut
Membres : **Administrator** (domaine racine uniquement)

## ⚠️ Sécurité
> 🔴 **Tier 0 — Groupe ultra-critique (forêt entière)**

- **Maintenir vide** en permanence — n'ajouter des membres que le temps d'une modification de schéma planifiée
- Toute modification du schéma doit être testée en environnement de test d'abord
- Surveiller toute modification de ce groupe — Event ID 4728, 4729
- Activer un processus de change management strict pour les modifications de schéma",

                ["Desc.Users.EnterpriseAdmins"] = @"# 👥 Groupe Enterprise Admins

Groupe **universel** créé automatiquement. Existe uniquement dans le **domaine racine de la forêt**.

## Rôle
- Administrateurs de l'entreprise avec contrôle sur **tous les domaines de la forêt**
- Peut ajouter/supprimer des domaines de la forêt
- Automatiquement membre de **Administrators** dans chaque domaine de la forêt

## Appartenance par défaut
Membres : **Administrator** (domaine racine uniquement)

## ⚠️ Sécurité
> 🔴 **Tier 0 — Groupe ultra-critique (forêt entière)**

- **Maintenir vide** en permanence — n'ajouter des membres que pour des opérations de niveau forêt
- Vecteur d'attaque transversal à tous les domaines de la forêt
- Surveiller toute modification — Event ID 4728, 4729
- Les membres ont un accès administrateur sur **tous** les contrôleurs de domaine de la forêt",

                ["Desc.Users.GroupPolicyCreatorOwners"] = @"# 👥 Groupe Group Policy Creator Owners

Groupe **global** créé automatiquement lors de la promotion du domaine.

## Rôle
- Membres autorisés à créer et modifier des **GPOs** dans le domaine
- Un membre peut modifier uniquement les GPOs qu'il a créées (sauf Domain Admins)

## Appartenance par défaut
Membres : **Administrator**

## ⚠️ Sécurité
> 🟠 **Tier 1 — Groupe à surveiller**

- Limiter les membres — la création de GPOs malveillantes est un vecteur d'attaque courant
- Surveiller la création de nouvelles GPOs et leurs liaisons (Event ID 5136, 5137)
- Toute GPO liée à l'OU Domain Controllers est potentiellement dangereuse",

                ["Desc.Users.ReadOnlyDCs"] = @"# 👥 Groupe Read-Only Domain Controllers

Groupe **global** créé automatiquement lors de la promotion du domaine.

## Rôle
- Contient les **RODC** (Read-Only Domain Controllers) du domaine
- Les RODCs répliquent uniquement les objets autorisés via la **Password Replication Policy**

## ⚠️ Sécurité
> 🔴 **Tier 0 — Groupe critique**

- Surveiller les ajouts à ce groupe
- La compromission d'un RODC expose uniquement les comptes dont le mot de passe est mis en cache sur ce RODC
- Configurer soigneusement la **Allowed RODC Password Replication Group** pour limiter l'exposition",

                ["Desc.Users.DnsAdmins"] = @"# 👥 Groupe DnsAdmins

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
- Envisager de déléguer la gestion DNS différemment si possible",

                ["Desc.Users.DefaultAccount"] = @"# 👤 Compte DefaultAccount

Compte **système géré** créé automatiquement par Windows.

## Rôle
- Compte interne utilisé par le système Windows pour certains processus
- Désactivé par défaut
- Géré automatiquement par Windows — ne pas modifier

## ⚠️ Sécurité
> 🟡 **Maintenir désactivé**

- Ne jamais activer ce compte manuellement
- Surveiller toute modification (activation, changement de mot de passe)",

                ["Desc.Users.WDAGUtilityAccount"] = @"# 👤 Compte WDAGUtilityAccount

Compte système pour **Windows Defender Application Guard (WDAG)**.

## Rôle
- Utilisé par la fonctionnalité WDAG pour isoler les sessions de navigation dans un conteneur Hyper-V
- Géré automatiquement par Windows — ne pas modifier
- Désactivé si WDAG n'est pas utilisé

## ⚠️ Sécurité
> 🟡 **Compte système — ne pas modifier**

- Ne jamais activer ou modifier ce compte manuellement
- Sa présence est normale dans les domaines Windows 10/11 et Windows Server 2016+",

                ["Desc.Users.CertPublishers"] = @"# 👥 Groupe Cert Publishers

Groupe **domaine local** créé automatiquement lors de la promotion du domaine.

## Rôle
- Membres autorisés à **publier des certificats** dans Active Directory (attribut `userCertificate`)
- Typiquement : les serveurs **Certificate Authority (CA)** enterprise

## ⚠️ Sécurité
> 🟠 **Tier 1 — Lié à l'infrastructure PKI**

- Limiter les membres aux serveurs CA uniquement
- La compromission d'un CA enterprise permet d'émettre des certificats frauduleux
- Surveiller les modifications de ce groupe — Event ID 4728, 4729
- Voir **ESC4** et autres techniques d'attaque PKI documentées dans **Certify/Certipy**",

                ["Desc.Users.RASandIAS"] = @"# 👥 Groupe RAS and IAS Servers

Groupe **domaine local** créé automatiquement lors de la promotion du domaine.

## Rôle
- Membres autorisés à accéder aux **propriétés d'accès réseau** des comptes utilisateurs
- Utilisé par les serveurs **NPS (Network Policy Server)**, **RADIUS** et **VPN**

## ⚠️ Sécurité
> 🟠 **Tier 1 — Accès aux attributs d'authentification réseau**

- Limiter aux serveurs NPS/RADIUS légitimes
- Un serveur RAS compromis peut intercepter ou manipuler les connexions VPN",

                ["Desc.Users.AllowedRODCReplication"] = @"# 👥 Groupe Allowed RODC Password Replication Group

Groupe **domaine local** créé automatiquement lors de la promotion du domaine.

## Rôle
- Définit les comptes dont les **mots de passe PEUVENT être mis en cache** sur les RODCs
- Vide par défaut — aucun mot de passe n'est répliqué sur les RODCs par défaut

## ⚠️ Sécurité
> 🔴 **Tier 0 — Contrôle de la réplication RODC**

- **Ne jamais ajouter** de comptes Tier 0 (admins, krbtgt, service accounts critiques)
- La compromission d'un RODC expose uniquement les comptes dont le hash est présent dans son cache
- Utiliser le groupe **Denied RODC Password Replication Group** pour protéger les comptes sensibles
- Auditer régulièrement les comptes mis en cache sur chaque RODC via `Get-ADDomainControllerPasswordReplicationPolicy`",

                ["Desc.Users.DeniedRODCReplication"] = @"# 👥 Groupe Denied RODC Password Replication Group

Groupe **domaine local** créé automatiquement lors de la promotion du domaine.

## Rôle
- Définit les comptes dont les **mots de passe NE PEUVENT PAS être mis en cache** sur les RODCs
- Protège les comptes critiques contre l'exposition en cas de compromission d'un RODC

## Membres par défaut
`krbtgt`, `Domain Admins`, `Schema Admins`, `Enterprise Admins`, `Group Policy Creator Owners`, `Read-Only Domain Controllers`

## ⚠️ Sécurité
> 🟢 **Bonne pratique — maintenir à jour**

- Ajouter tous les comptes de service critiques et comptes Tier 0 à ce groupe
- Ne jamais retirer les membres par défaut
- Auditer régulièrement la liste pour s'assurer qu'elle est exhaustive",

                ["Desc.Users.DnsUpdateProxy"] = @"# 👥 Groupe DnsUpdateProxy

Groupe **global** créé automatiquement lors de la promotion du domaine.

## Rôle
- Membres autorisés à **enregistrer des enregistrements DNS** pour le compte d'autres clients
- Utilisé typiquement par les serveurs **DHCP** pour la mise à jour dynamique DNS

## ⚠️ Sécurité
> 🟠 **Tier 1 — Risque de squatting DNS**

- **Limiter** les membres aux seuls serveurs DHCP autorisés
- Les enregistrements créés par ce groupe sont initialement sans propriétaire — vecteur de **DNS hijacking**
- Configurer le serveur DHCP avec un compte dédié plutôt que d'utiliser ce groupe si possible
- Si utilisé, activer le **DNS dynamic update credentials** sur le serveur DHCP",

                ["Desc.Users.CloneableDCs"] = @"# 👥 Groupe Cloneable Domain Controllers

Groupe **global** créé automatiquement lors de la promotion du domaine (Windows Server 2012+).

## Rôle
- Membres (DCs) qui peuvent être **clonés** pour déployer rapidement des contrôleurs de domaine supplémentaires
- Fonctionnalité de clonage basée sur Hyper-V

## ⚠️ Sécurité
> 🔴 **Tier 0 — Fonctionnalité de clonage de DC**

- Limiter aux DCs qui doivent réellement être clonés
- Un clone de DC hérite des secrets du DC source — processus à encadrer strictement
- Vérifier que l'hôte Hyper-V est sécurisé avant tout clonage",

                ["Desc.Users.ProtectedUsers"] = @"# 👥 Groupe Protected Users

Groupe **universel** créé automatiquement (Windows Server 2012 R2+).

## Rôle
- Les membres bénéficient de **protections Kerberos renforcées** automatiquement :
  - Pas de délégation Kerberos
  - Pas de RC4 pour l'authentification (Kerberos AES uniquement)
  - Pas de mise en cache des credentials NTLM
  - TGT réduit à 4 heures (non renouvelable)
  - Pas d'authentification NTLM, Digest, CredSSP

## ⚠️ Sécurité
> 🟢 **Bonne pratique — ajouter les comptes sensibles**

- **Ajouter** tous les comptes d'administration Tier 0 et Tier 1
- **Tester** avant d'ajouter des comptes de service — incompatible avec certains protocoles legacy
- **Ne pas ajouter** : comptes de service utilisant NTLM, délégation Kerberos, ou authentification avec mot de passe en clair
- Protège contre **Pass-the-Hash**, **Pass-the-Ticket**, **Overpass-the-Hash**",

                ["Desc.Users.KeyAdmins"] = @"# 👥 Groupe Key Admins

Groupe **global** créé automatiquement (Windows Server 2016+).

## Rôle
- Membres autorisés à effectuer des actions administratives sur les **attributs msDS-KeyCredentialLink**
- Utilisé pour la gestion des **Windows Hello for Business** et des clés FIDO2 au niveau du domaine

## ⚠️ Sécurité
> 🔴 **Tier 0 — Risque Shadow Credentials**

- Vecteur d'attaque **Shadow Credentials** : un membre peut ajouter des credentials alternatifs à n'importe quel compte du domaine
- Attaque documentée : `Whisker`, `pyWhisker`
- Surveiller les modifications de l'attribut `msDS-KeyCredentialLink` — Event ID 5136
- Limiter les membres au strict minimum",

                ["Desc.Users.EnterpriseKeyAdmins"] = @"# 👥 Groupe Enterprise Key Admins

Groupe **universel** créé automatiquement (Windows Server 2016+). Domaine racine de la forêt.

## Rôle
- Équivalent de **Key Admins** mais avec portée sur **tous les domaines de la forêt**
- Gestion des attributs `msDS-KeyCredentialLink` au niveau forêt

## ⚠️ Sécurité
> 🔴 **Tier 0 — Risque Shadow Credentials (forêt entière)**

- Même vecteur d'attaque que **Key Admins** mais avec portée forêt
- **Maintenir vide** en permanence sauf besoin opérationnel spécifique
- Surveiller toute modification — Event ID 4728, 4729 et 5136",
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
                ["Relations.TabUserGroup"] = "🖥️ Objets → Groupe",
                ["Relations.TabGroupGroup"] = "👥 Groupe → Groupe",
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
                ["Relations.LabelUserSource"] = "Utilisateur",
                ["Relations.LabelGroupMember"] = "Groupe membre",
                ["Relations.LabelGroupParent"] = "Groupe parent (cible)",
                ["Relations.ChooseUser"] = "Choisir un utilisateur…",
                ["Relations.ColUser"] = "Utilisateur",
                ["Relations.ColGroup"] = "Groupe",
                ["Relations.ColGroupMember"] = "Groupe membre",
                ["Relations.ColGroupParent"] = "Groupe parent",
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
                ["About.FullName"] = "Simuler, Modéliser et Auditer Active Directory eXpert",
                ["About.Version"] = "Version 0.3.4",
                ["About.Description"] = "SMAD-X est un simulateur expert d'Active Directory conçu pour la formation, la documentation et l'expérimentation. Il génère une structure AD fidèle à une installation fraîche, avec tous les objets par défaut (Builtin, Users, Computers, System, GPOs, PSOs), et permet de la visualiser, modifier et exporter sans infrastructure réelle.",
                ["About.Features"] = "Fonctionnalités principales :",
                ["About.Feature1"] = "• Structure AD par défaut complète et fidèle (containers Builtin, Users, Computers, System)",
                ["About.Feature2"] = "• Vue graphe de relations interactive (force-directed) avec filtres par type et tier",
                ["About.Feature3"] = "• Gestion des GPO (liaisons), PSO (Password Settings Objects) et relations MemberOf",
                ["About.Feature4"] = "• Export JSON natif et scripts PowerShell prêts à déployer",
                ["About.Feature5"] = "• Modèle de tiering Microsoft (Tier 0/1/2) avec code couleur configurable",
                ["About.Feature6"] = "• Support multilingue (Français/English) et descriptions Markdown enrichies",
                ["About.Copyright"] = "© 2025 SMAD-X - Tous droits réservés",
                ["About.Close"] = "Fermer",

                // ── Descriptions structure exemple (CreateSampleStructure) ──
                ["Desc.Sample.Domain"] = @"# Domaine contoso.com

Domaine Active Directory de démonstration créé par SMAD-X.

## Présentation
Ce domaine illustre une architecture AD typique d'une organisation de taille intermédiaire, segmentée selon le **modèle de tiering Microsoft** (Tier 0 / Tier 1 / Tier 2).

## Structure
| OU / Container | Rôle |
|---|---|
| Admin | Comptes et groupes d'administration Tier 0 |
| Servers | Serveurs d'infrastructure Tier 1 |
| Users | Comptes utilisateurs standards Tier 2 |
| Workstations | Postes de travail Tier 2 |
| Domain Controllers | Contrôleurs de domaine Tier 0 |

## ⚠️ Sécurité
> Cette structure est fournie à titre pédagogique. Adapter au contexte réel avant déploiement.",

                ["Desc.Sample.AdminOU"] = @"# 🏛️ OU Administration (Tier 0)

Unité d'organisation contenant les **comptes et groupes d'administration de plus haut niveau** du domaine.

## Rôle
- Héberge les comptes administrateurs nominatifs (non-partagés) utilisés uniquement pour les tâches d'administration de domaine
- Contient les groupes d'administration Tier 0
- Cible de GPOs de durcissement spécifiques aux comptes privilégiés

## ⚠️ Sécurité
> 🔴 **Tier 0 — Zone critique**

- Appliquer une **GPO de durcissement stricte** sur cette OU (restrictions logon, audit intensifié)
- Bloquer l'héritage GPO et ne lier que des GPOs validées
- Accès en lecture/écriture sur cette OU : **Domain Admins uniquement**
- Surveiller tout objet créé ou modifié dans cette OU (Event ID 5137, 5136)
- Ces comptes doivent être utilisés exclusivement depuis des **PAW (Privileged Access Workstation)**",

                ["Desc.Sample.AdminUser"] = @"# 👤 Compte Administrateur (Tier 0)

Compte administrateur **nominatif** dédié à l'administration du domaine.

## Rôle
- Utilisé uniquement pour les tâches d'administration Active Directory nécessitant des droits Domain Admins
- Distinct du compte `Administrator` intégré du domaine
- Ne doit jamais être utilisé pour les tâches quotidiennes

## ⚠️ Sécurité
> 🔴 **Tier 0 — Compte à haute valeur**

- Utiliser exclusivement depuis une **PAW (Privileged Access Workstation)**
- Activer **MFA** (Windows Hello for Business, carte à puce)
- Ajouter au groupe **Protected Users** pour bloquer NTLM et délégation Kerberos
- Surveiller toute ouverture de session (Event ID 4624, 4625, 4648)
- Ne jamais utiliser ce compte pour naviguer sur Internet ou lire des e-mails
- Rotation du mot de passe tous les 60 jours minimum",

                ["Desc.Sample.AdminDomainAdmins"] = @"# 👥 Groupe Domain Admins (OU Admin)

Instance locale du groupe **Domain Admins** dans l'OU Administration.

## Rôle
- Regroupe les comptes administrateurs nominatifs ayant les droits Domain Admins
- Utilisé pour déléguer l'administration du domaine à des personnes identifiées

## ⚠️ Sécurité
> 🔴 **Tier 0 — Groupe ultra-privilégié**

- Maintenir le nombre de membres **au minimum absolu** (idéalement 2-3 comptes de breakglass)
- Auditer les membres mensuellement
- Tout ajout à ce groupe doit passer par un processus de change management
- Surveiller Event ID 4728 (ajout membre) et 4729 (suppression membre)",

                ["Desc.Sample.ServersOU"] = @"# 🖥️ OU Servers (Tier 1)

Unité d'organisation contenant les **serveurs d'infrastructure** du domaine.

## Rôle
- Héberge les serveurs de fichiers, serveurs d'applications, serveurs d'impression, etc.
- Séparé des postes utilisateurs (Tier 2) et des contrôleurs de domaine (Tier 0)
- Cible de GPOs de configuration et de durcissement des serveurs

## ⚠️ Sécurité
> 🟠 **Tier 1 — Infrastructure**

- Appliquer les **CIS Benchmarks** ou recommandations ANSSI pour Windows Server via GPO
- Bloquer les comptes Tier 2 (utilisateurs standards) de se connecter aux serveurs Tier 1
- Activer **Windows Defender Credential Guard** sur tous les serveurs
- Surveiller les connexions interactives (Event ID 4624 type 2 et 10)
- Déployer un **EDR** sur tous les serveurs",

                ["Desc.Sample.FileServer"] = @"# 💾 SRV-FILE-01 — Serveur de fichiers (Tier 1)

Serveur de fichiers principal de l'organisation.

## Rôle
- Héberge les partages réseau de l'organisation (données métier, profils itinérants, dossiers de travail)
- Utilise des **ACLs NTFS** et des **Share Permissions** pour le contrôle d'accès
- Partages typiques : `\\SRV-FILE-01\Data`, `\\SRV-FILE-01\Profiles$`, `\\SRV-FILE-01\Home$`

## ⚠️ Sécurité
> 🟠 **Tier 1 — Données sensibles**

- Activer **SMB Signing** obligatoire (prévention des attaques MITM / Relay)
- Désactiver **SMBv1** (vulnérable à EternalBlue / WannaCry)
- Surveiller les accès aux partages (Event ID 5140, 5145) pour détecter les ransomwares
- Déployer un **honeypot file** dans chaque partage (fichier leurre alertant sur accès anormal)
- Implémenter le **principe du moindre privilège** sur les partages et les ACLs NTFS
- Sauvegardes régulières avec des **sauvegardes immuables** hors ligne",

                ["Desc.Sample.AppServer"] = @"# ⚙️ SRV-APP-01 — Serveur d'applications (Tier 1)

Serveur hébergeant les applications métier de l'organisation.

## Rôle
- Exécute les applications d'entreprise (ERP, intranet, services web internes, etc.)
- Utilise des **comptes de service gMSA** pour les identités applicatives
- Communique avec les serveurs de base de données Tier 1

## ⚠️ Sécurité
> 🟠 **Tier 1 — Applications critiques**

- Utiliser des **gMSA** (Group Managed Service Accounts) pour tous les comptes de service — jamais de comptes utilisateurs avec mot de passe statique
- Activer **Windows Defender Application Control (WDAC)** pour restreindre les exécutables autorisés
- Isoler les applications dans des **comptes de service dédiés** avec le strict minimum de permissions
- Appliquer les mises à jour de sécurité mensuellement (Patch Tuesday)
- Surveiller les processus inhabituels (Event ID 4688 avec commandline logging activé)",

                ["Desc.Sample.UsersOU"] = @"# 👤 OU Users (Tier 2)

Unité d'organisation contenant les **comptes utilisateurs standards** de l'organisation.

## Rôle
- Héberge les comptes des employés pour leurs usages quotidiens (messagerie, fichiers, applications)
- Cible des GPOs de configuration des postes de travail et des restrictions utilisateurs
- Peut être subdivisée par département (RH, Finance, IT, Direction…)

## ⚠️ Sécurité
> 🟢 **Tier 2 — Utilisateurs standards**

- Appliquer le **principe du moindre privilège** : aucun compte de cette OU ne doit être administrateur local
- Activer **Windows Defender Credential Guard** pour protéger les credentials en mémoire
- Déployer une GPO de **restriction de scripts** (SRP ou WDAC) pour limiter l'exécution de PowerShell, WScript, etc.
- Former les utilisateurs aux **attaques de phishing** et d'ingénierie sociale
- Surveiller les connexions hors horaires habituels et les volumes de données inhabituels",

                ["Desc.Sample.UserJdoe"] = @"# 👤 John Doe (jdoe) — Développeur (Tier 2)

Compte utilisateur standard pour un développeur de l'organisation.

## Rôle
- Accès aux ressources de développement (dépôts de code, serveurs de dev, tickets)
- Membre du groupe **Domain Users** et éventuellement d'un groupe **Developers**
- Pas de droits administrateurs locaux sur son poste de travail

## ⚠️ Sécurité
> 🟢 **Tier 2**

- Ne pas accorder de droits administrateurs locaux (utiliser des groupes dédiés via GPO si nécessaire)
- Sensibiliser aux risques liés au développement : secrets dans le code, dépendances malveillantes
- Surveiller les accès aux ressources sensibles hors périmètre développement
- Si accès à des environnements de production : créer un compte séparé Tier 1",

                ["Desc.Sample.UserAsmith"] = @"# 👤 Alice Smith (asmith) — Manager (Tier 2)

Compte utilisateur standard pour une manager de l'organisation.

## Rôle
- Accès aux ressources de management (fichiers de direction, outils RH, reporting)
- Destinataire probable d'attaques de **spear-phishing** et de **Business Email Compromise (BEC)**
- Peut avoir accès à des données sensibles (RH, finance, stratégie)

## ⚠️ Sécurité
> 🟢 **Tier 2 — Cible à risque élevé**

- Activer **MFA** sur la messagerie et les applications cloud (Microsoft 365, etc.)
- Former spécifiquement aux attaques BEC (usurpation de direction, virements frauduleux)
- Surveiller les accès aux documents financiers et RH (DLP — Data Loss Prevention)
- Accès conditionnel depuis les postes managés uniquement (Intune/Entra ID Conditional Access)",

                ["Desc.Sample.WorkstationsOU"] = @"# 💻 OU Workstations (Tier 2)

Unité d'organisation contenant les **postes de travail des utilisateurs** de l'organisation.

## Rôle
- Héberge les ordinateurs portables et fixes des employés
- Cible des GPOs de configuration, de sécurité et de déploiement logiciel
- Point d'entrée principal des attaquants dans un réseau d'entreprise

## ⚠️ Sécurité
> 🟢 **Tier 2 — Premier vecteur d'intrusion**

- Appliquer les **CIS Benchmarks** ou ANSSI pour Windows 10/11 via GPO
- Activer **BitLocker** sur tous les postes (chiffrement disque)
- Désactiver **LLMNR** et **NetBIOS over TCP/IP** (prévention Responder / MITM)
- Restreindre les ports USB et médias amovibles (via GPO ou solution MDM)
- Déployer un **EDR** sur tous les postes
- Activer **Windows Defender Credential Guard** et **Device Guard**
- Surveiller les connexions à des partages réseau inhabituels (mouvement latéral)",

                ["Desc.Sample.Workstation1"] = @"# 💻 WKS-001 — Poste de John Doe (Tier 2)

Poste de travail assigné au développeur John Doe.

## Rôle
- Poste de travail standard Windows 10/11 joint au domaine
- Reçoit les GPOs de l'OU Workstations (configuration, sécurité, déploiement)
- Accès aux ressources réseau via les credentials de jdoe

## ⚠️ Sécurité
> 🟢 **Tier 2**

- BitLocker activé avec stockage de la clé de récupération dans AD
- Pas de droits administrateurs locaux pour jdoe
- Connexions aux ressources Tier 1 uniquement via les groupes autorisés
- EDR déployé et supervision SOC",

                ["Desc.Sample.DomainControllersOU"] = @"# 🏛️ OU Domain Controllers (Tier 0)

Unité d'organisation système contenant tous les **contrôleurs de domaine**.

## Rôle
- Créée automatiquement lors de la promotion du premier DC
- Tous les DCs y sont automatiquement placés lors de leur promotion
- Cible exclusive de la **Default Domain Controllers Policy** et des GPOs de durcissement DC

## ⚠️ Sécurité
> 🔴 **Tier 0 — Zone la plus critique du domaine**

- La **Default Domain Controllers Policy** doit rester liée à cette OU et ne jamais être supprimée
- Appliquer des GPOs de durcissement supplémentaires : CIS DC Benchmark, ANSSI AD
- Interdire toute connexion interactive de comptes Tier 1 ou Tier 2 sur les DCs
- Activer l'audit avancé sur tous les DCs (connexions, modifications AD, Kerberos)
- Surveiller les tentatives d'exploitation Kerberos (AS-REP Roasting, Kerberoasting) via les Event IDs 4768, 4769, 4771
- Sauvegardes de l'état du système (System State) planifiées et testées régulièrement",

                ["Desc.Sample.DC01"] = @"# 🖧 DC01 — Contrôleur de domaine principal (Tier 0)

Premier contrôleur de domaine du domaine contoso.com.

## Rôle
- Héberge l'ensemble des **rôles FSMO** lors de l'installation initiale :
  - **Schema Master** : contrôle les modifications du schéma AD
  - **Domain Naming Master** : gère l'ajout/suppression de domaines dans la forêt
  - **RID Master** : distribue les pools de RIDs pour la création d'objets de sécurité
  - **PDC Emulator** : synchronisation de l'heure, gestion des verrouillages de compte, compatibilité NT4
  - **Infrastructure Master** : maintient les références inter-domaines

## ⚠️ Sécurité
> 🔴 **Tier 0 — Infrastructure AD critique**

- Ne jamais installer d'applications tierces sur un DC (surface d'attaque minimale)
- Activer **Windows Defender Credential Guard** sur le DC
- Restreindre les connexions réseau entrant/sortant au strict nécessaire (pare-feu Windows)
- Surveiller les Event IDs critiques : 4662 (accès objet AD), 4769 (ticket Kerberos), 4776 (NTLM)
- Mettre en place un **DC de secours** dans un site/VLAN séparé
- Inclure dans un plan de **Disaster Recovery** avec sauvegardes System State testées",

                ["Desc.Sample.GMSA"] = @"# 🔐 svc-webapp — Group Managed Service Account (Tier 1)

Compte de service géré de groupe pour l'application web de l'organisation.

## Rôle
- Identité de service utilisée par l'application web hébergée sur SRV-APP-01
- Le mot de passe est **géré automatiquement par AD** (rotation tous les 30 jours, transparent pour l'application)
- Accessible uniquement aux serveurs autorisés via l'attribut `msDS-GroupMSAMembership`
- Aucune connexion interactive possible

## Avantages par rapport aux comptes de service classiques
| Critère | Compte utilisateur | gMSA |
|---|---|---|
| Rotation du mot de passe | Manuelle | Automatique |
| Connexion interactive | Possible | Impossible |
| Usage multi-serveurs | Risqué | Natif |
| Gestion centralisée | Non | Oui |

## ⚠️ Sécurité
> 🟠 **Tier 1 — Compte de service**

- Contrôler strictement les serveurs autorisés à utiliser ce gMSA (`msDS-GroupMSAMembership`)
- Appliquer le **principe du moindre privilège** : uniquement les permissions nécessaires à l'application
- Surveiller l'utilisation de ce compte sur des serveurs non autorisés (Event ID 4624)
- Documenter les ressources accédées par ce compte",

                ["Desc.Sample.SystemContainer"] = @"# ⚙️ Container System

Container système d'Active Directory contenant les objets de configuration et les paramètres système.

## Contenu typique
- `Password Settings Container` : Objets PSO (Fine-Grained Password Policies)
- `Policies` : GPOs du domaine (stockage objet AD, hors SYSVOL)
- `WMI Filters` : Filtres WMI pour le ciblage des GPOs
- `AdminSDHolder` : Modèle de sécurité pour les objets protégés

## ⚠️ Sécurité
> 🔴 **Tier 0 — Ne pas modifier manuellement**

- **AdminSDHolder** : surveiller les modifications de ses ACLs — tout objet y ressemblant sera propagé aux groupes protégés (Domain Admins, etc.) toutes les 60 minutes par le processus SDProp
- Ne jamais supprimer ou déplacer les objets système de ce container",

                ["Desc.Sample.PSOContainer"] = @"# 🔑 Password Settings Container

Container système contenant tous les **Password Settings Objects (PSO)** du domaine.

## Rôle
- Permet de définir des **politiques de mot de passe granulaires** par groupe ou utilisateur (Fine-Grained Password Policy — FGPP)
- Disponible depuis Windows Server 2008 (niveau fonctionnel de domaine 2008+)
- Priorité : le PSO avec la plus faible valeur `msDS-PasswordSettingsPrecedence` s'applique en cas de conflit

## ⚠️ Sécurité
> 🔴 **Tier 0 — Politiques de mots de passe**

- Créer un PSO strict pour les comptes Tier 0 (longueur ≥ 16, rotation ≤ 60 jours, verrouillage après 3 tentatives)
- Le PSO l'emporte sur la Default Domain Policy pour les comptes ciblés
- Surveiller les modifications des PSOs (Event ID 5136)",

                ["Desc.Sample.PSOAdmin"] = @"# 🔑 PSO-Tier0-Admins — Politique mots de passe administrateurs

Password Settings Object appliqué aux **comptes d'administration Tier 0**.

## Paramètres de sécurité renforcés
| Paramètre | Valeur |
|---|---|
| Longueur minimale | **16 caractères** |
| Complexité | Activée |
| Historique | 24 mots de passe |
| Âge maximum | **60 jours** |
| Âge minimum | 2 jours |
| Seuil de verrouillage | **3 tentatives** |
| Durée de verrouillage | **1 heure** (ou déverrouillage manuel) |

## Objets ciblés
- Groupe **Domain Admins**
- Compte **Administrator**

## ⚠️ Sécurité
> 🔴 **Tier 0 — Politique critique**

- Préférence : **10** (priorité haute sur la Default Domain Policy)
- Envisager des **passphrases** (ex. : 4 mots aléatoires ≥ 20 caractères) pour faciliter la mémorisation tout en maintenant la sécurité
- Coupler avec l'utilisation d'un **gestionnaire de mots de passe d'entreprise** (CyberArk, BeyondTrust, etc.)
- Ce PSO doit cibler des **groupes**, pas des utilisateurs individuels",

                ["Desc.Sample.PSOUsers"] = @"# 🔑 PSO-Standard-Users — Politique mots de passe utilisateurs

Password Settings Object appliqué aux **comptes utilisateurs standards Tier 2**.

## Paramètres de sécurité standard
| Paramètre | Valeur |
|---|---|
| Longueur minimale | **12 caractères** |
| Complexité | Activée |
| Historique | 12 mots de passe |
| Âge maximum | **90 jours** |
| Âge minimum | 1 jour |
| Seuil de verrouillage | **5 tentatives** |
| Durée de verrouillage | **30 minutes** |

## Objets ciblés
- Utilisateurs **jdoe**, **asmith**

## ⚠️ Sécurité
> 🟢 **Tier 2 — Politique standard**

- Préférence : **50** (priorité inférieure à PSO-Tier0-Admins)
- Activer le **self-service de réinitialisation** (SSPR) pour réduire la charge du helpdesk
- Envisager une intégration avec **Microsoft Entra ID Password Protection** pour bloquer les mots de passe courants/compromis (HaveIBeenPwned)",
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
                ["Menu.View.ManageRelations"] = "Manage Relations...",
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
                ["Details.MemberOf"] = "👥 Member of (groups)",
                ["Details.LinkedGPOs"] = "📋 Linked GPOs",
                ["Details.PSOAppliesTo"] = "🔑 Applies to",

                // Actions
                ["Action.Rename"] = "Rename",
                ["Action.Copy"] = "Copy",
                ["Action.Delete"] = "Delete",
                ["Action.Manage"] = "⚙ Manage",
                ["Toolbar.Delete"] = "Delete",

                // Context Menu
                ["ContextMenu.AddOU"] = "Add OU",
                ["ContextMenu.AddContainer"] = "Add Container",
                ["ContextMenu.AddUser"] = "Add User",
                ["ContextMenu.AddGroup"] = "Add Group",
                ["ContextMenu.AddComputer"] = "Add Computer",
                ["ContextMenu.AddGMSA"] = "Add GMSA",
                ["ContextMenu.AddPolicy"] = "Add GPO",
                ["ContextMenu.AddPSO"] = "Add PSO",
                ["ContextMenu.Rename"] = "Rename",
                ["ContextMenu.Copy"] = "Copy",
                ["ContextMenu.Paste"] = "Paste",
                ["ContextMenu.Delete"] = "Delete",
                ["ContextMenu.ManageRelations"] = "Manage Relations",
                ["ContextMenu.AddToGroup"] = "Add to Group",
                ["ContextMenu.AddMember"] = "Add Members",
                ["ContextMenu.AddGroupToGroup"] = "Add to Group (nesting)",
                ["Toolbar.Relations"] = "Relations",
                ["Toolbar.Graph"] = "Graph",

                // Tooltips toolbar
                ["Tooltip.AddOU"] = "Add an Organizational Unit",
                ["Tooltip.AddContainer"] = "Add a Container",
                ["Tooltip.AddUser"] = "Add a User",
                ["Tooltip.AddGroup"] = "Add a Group",
                ["Tooltip.AddComputer"] = "Add a Computer",
                ["Tooltip.AddGMSA"] = "Add a Group Managed Service Account",
                ["Tooltip.AddPolicy"] = "Add a Group Policy Object (GPO)",
                ["Tooltip.AddPSO"] = "Add a Password Settings Object",
                ["Tooltip.Delete"] = "Delete the selected object",
                ["Tooltip.Relations"] = "Manage object relations",
                ["Tooltip.Graph"] = "Show graph view of relations",
                ["Tooltip.ExportPNG"] = "Export tree as PNG image",
                ["Tooltip.ClearSearch"] = "Clear search",
                ["Menu.View.ShowGraph"] = "🕸️ Graph View...",

                // TreeView
                ["TreeView.Title"] = "Active Directory Structure",
                ["TreeView.SearchPlaceholder"] = "Search (name, type, description)...",
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
                ["Desc.Users.Administrator"] = @"# 👤 Administrator Account

**Built-in** administrator account created automatically when the domain is promoted.

## Role
- Full and unrestricted access to all domain resources
- The only account that cannot be locked out by the account lockout policy
- Permanent member of **Domain Admins** and **Administrators**

## Default Group Membership
| Group | Scope |
|---|---|
| Administrators | Domain local |
| Domain Admins | Global |
| Group Policy Creator Owners | Global |
| Schema Admins (forest root) | Universal |
| Enterprise Admins (forest root) | Universal |

## ⚠️ Security
> 🔴 **Tier 0 — Critical account**

- **Rename** this account to avoid exposing the predictable `Administrator` name
- **Disable** this account in production and create a dedicated named account for administration
- Enable **lockout policy** against brute-force attacks (via Fine-Grained Password Policy)
- This account is a prime target for **Pass-the-Hash** and **Mimikatz** attacks
- Monitor with alerts on any logon event for this account
- **Protected Users** group (Windows Server 2012 R2+) does not apply to Administrator — manage manually",

                ["Desc.Users.Guest"] = @"# 👤 Guest Account

**Built-in** guest account created automatically when the domain is promoted.

## Role
- Allows anonymous and limited access to the domain without a password
- Rights equivalent to the **Guests** group (very restricted)
- Cannot change its own password or access system settings

## Default Group Membership
| Group | Scope |
|---|---|
| Guests | Domain local |
| Domain Guests | Global |

## ⚠️ Security
> 🔴 **Disable immediately in production**

- This account is **disabled by default** since Windows Server 2008 — verify it stays disabled
- Never enable this account in a production environment
- Classic attack vector for unauthenticated access
- Monitor any activation or usage attempt (Event ID 4722, 4624)",

                ["Desc.Users.Krbtgt"] = @"# 🔑 krbtgt Account

**Kerberos Key Distribution Center (KDC)** service account created automatically when the domain is promoted.

## Role
- Used exclusively by the **KDC** service to sign and encrypt Kerberos tickets (TGT)
- Its password serves as the encryption key for all **Ticket Granting Tickets** in the domain
- Disabled by default — never enable

## Default Group Membership
| Group | Scope |
|---|---|
| Domain Users | Global |

## ⚠️ Security
> 🔴 **Tier 0 — Ultra-critical account**

- **Never delete** this account — it renders the domain unusable
- **Never enable** this account interactively
- Compromising this account allows creating **Golden Tickets** (Mimikatz attack)
- **Reset the password TWICE** after a suspected compromise (two consecutive resets required to invalidate existing tickets)
- Preventive rotation recommended **every 180 days** in production
- Monitor password changes (Event ID 4723, 4724)
- Since Windows Server 2016: use the separate **RODC krbtgt account** for RODCs",

                ["Desc.Users.DomainAdmins"] = @"# 👥 Domain Admins Group

**Global** group created automatically when the domain is promoted.

## Role
- Designated administrators of the domain
- Members are automatically added to the **Administrators** group on every domain-joined machine
- Full control over all objects in the domain

## Default Membership
Members: **Administrator**

## ⚠️ Security
> 🔴 **Tier 0 — Ultra-privileged group**

- Limit membership to the bare minimum (principle of least privilege)
- Never use a Domain Admin account for day-to-day tasks
- Use dedicated administration accounts (PAW — Privileged Access Workstation)
- Enable monitoring for any modification (Event ID 4728, 4729)
- Domain Admin members are **Administrators** on **every** machine in the domain — major lateral movement vector",

                ["Desc.Users.DomainUsers"] = @"# 👥 Domain Users Group

**Global** group created automatically when the domain is promoted.

## Role
- Contains **all** domain user accounts (added automatically)
- Used as the base group for permissions on shared resources
- Member of the local **Users** group on every domain-joined machine

## ⚠️ Security
> 🟢 **Tier 2 — Standard group**

- Avoid granting elevated permissions to this group — it contains all users
- Audit resources accessible by this group (shares, GPOs)
- Useful for applying policies to all domain users",

                ["Desc.Users.DomainComputers"] = @"# 👥 Domain Computers Group

**Global** group created automatically when the domain is promoted.

## Role
- Contains **all** computers joined to the domain (excluding domain controllers)
- Automatically added when a workstation joins the domain

## ⚠️ Security
> 🟢 **Tier 2 — Standard group**

- Can be used to apply policies to all workstations
- Monitor unexpected additions (unauthorized machine joins)",

                ["Desc.Users.DomainControllers"] = @"# 👥 Domain Controllers Group

**Global** group created automatically when the domain is promoted.

## Role
- Contains **all** domain controllers (DCs) in the domain
- Automatically added when a server is promoted to DC

## ⚠️ Security
> 🔴 **Tier 0 — Critical group**

- Monitor any modification (DC added/removed) — Event ID 4728, 4729
- A compromised domain controller compromises the entire domain",

                ["Desc.Users.SchemaAdmins"] = @"# 👥 Schema Admins Group

**Universal** group created automatically. Exists only in the **forest root domain**.

## Role
- Only members authorized to modify the **Active Directory schema** (class/attribute structure)
- Schema modifications are irreversible and apply to the entire forest

## Default Membership
Members: **Administrator** (forest root only)

## ⚠️ Security
> 🔴 **Tier 0 — Ultra-critical group (entire forest)**

- **Keep empty** at all times — only add members for the duration of a planned schema modification
- All schema changes must be tested in a test environment first
- Monitor any modification — Event ID 4728, 4729
- Enforce strict change management for schema modifications",

                ["Desc.Users.EnterpriseAdmins"] = @"# 👥 Enterprise Admins Group

**Universal** group created automatically. Exists only in the **forest root domain**.

## Role
- Enterprise administrators with control over **all domains in the forest**
- Can add/remove domains from the forest
- Automatically member of **Administrators** in every domain in the forest

## Default Membership
Members: **Administrator** (forest root only)

## ⚠️ Security
> 🔴 **Tier 0 — Ultra-critical group (entire forest)**

- **Keep empty** at all times — only add members for forest-level operations
- Cross-forest attack vector affecting all domains
- Monitor any modification — Event ID 4728, 4729
- Members have administrator access on **all** domain controllers in the forest",

                ["Desc.Users.GroupPolicyCreatorOwners"] = @"# 👥 Group Policy Creator Owners

**Global** group created automatically when the domain is promoted.

## Role
- Members authorized to create and modify **GPOs** in the domain
- A member can only modify GPOs they created (except Domain Admins)

## Default Membership
Members: **Administrator**

## ⚠️ Security
> 🟠 **Tier 1 — Group to monitor**

- Limit members — creating malicious GPOs is a common attack vector
- Monitor GPO creation and linking (Event ID 5136, 5137)
- Any GPO linked to the Domain Controllers OU is potentially dangerous",

                ["Desc.Users.ReadOnlyDCs"] = @"# 👥 Read-Only Domain Controllers Group

**Global** group created automatically when the domain is promoted.

## Role
- Contains **RODCs** (Read-Only Domain Controllers) in the domain
- RODCs replicate only objects authorized via the **Password Replication Policy**

## ⚠️ Security
> 🔴 **Tier 0 — Critical group**

- Monitor additions to this group
- Compromising a RODC exposes only the accounts whose passwords are cached on that RODC
- Carefully configure the **Allowed RODC Password Replication Group** to limit exposure",

                ["Desc.Users.DnsAdmins"] = @"# 👥 DnsAdmins Group

**Domain local** group created automatically when the DNS role is installed.

## Role
- Full administrative access to the domain **DNS** service
- Allows managing zones, records and DNS configuration

## ⚠️ Security
> 🔴 **Tier 0 — Known privilege escalation path**

- **Critical privilege escalation** vector: a DnsAdmins member can load a malicious DLL into the DNS service (which runs as SYSTEM on DCs)
- **CVE**: Documented technique by Shay Ber (2017) — `dnscmd /config /serverlevelplugindll`
- Minimize membership in this group
- Monitor use of `dnscmd` by members (Event ID 4688)
- Consider delegating DNS management differently if possible",

                ["Desc.Users.DefaultAccount"] = @"# 👤 DefaultAccount

**System-managed** account created automatically by Windows.

## Role
- Internal account used by Windows for certain processes
- Disabled by default
- Managed automatically by Windows — do not modify

## ⚠️ Security
> 🟡 **Keep disabled**

- Never enable this account manually
- Monitor any modification (activation, password change)",

                ["Desc.Users.WDAGUtilityAccount"] = @"# 👤 WDAGUtilityAccount

System account for **Windows Defender Application Guard (WDAG)**.

## Role
- Used by the WDAG feature to isolate browsing sessions in a Hyper-V container
- Managed automatically by Windows — do not modify
- Disabled if WDAG is not in use

## ⚠️ Security
> 🟡 **System account — do not modify**

- Never enable or modify this account manually
- Its presence is normal in Windows 10/11 and Windows Server 2016+ domains",

                ["Desc.Users.CertPublishers"] = @"# 👥 Cert Publishers Group

**Domain local** group created automatically when the domain is promoted.

## Role
- Members authorized to **publish certificates** in Active Directory (`userCertificate` attribute)
- Typically: enterprise **Certificate Authority (CA)** servers

## ⚠️ Security
> 🟠 **Tier 1 — Linked to PKI infrastructure**

- Limit membership to CA servers only
- Compromising an enterprise CA allows issuing fraudulent certificates
- Monitor modifications — Event ID 4728, 4729
- See **ESC4** and other PKI attack techniques documented in **Certify/Certipy**",

                ["Desc.Users.RASandIAS"] = @"# 👥 RAS and IAS Servers Group

**Domain local** group created automatically when the domain is promoted.

## Role
- Members authorized to access **remote access properties** of user accounts
- Used by **NPS (Network Policy Server)**, **RADIUS** and **VPN** servers

## ⚠️ Security
> 🟠 **Tier 1 — Access to network authentication attributes**

- Limit to legitimate NPS/RADIUS servers only
- A compromised RAS server can intercept or manipulate VPN connections",

                ["Desc.Users.AllowedRODCReplication"] = @"# 👥 Allowed RODC Password Replication Group

**Domain local** group created automatically when the domain is promoted.

## Role
- Defines accounts whose **passwords CAN be cached** on RODCs
- Empty by default — no passwords are replicated to RODCs by default

## ⚠️ Security
> 🔴 **Tier 0 — Controls RODC replication**

- **Never add** Tier 0 accounts (admins, krbtgt, critical service accounts)
- Compromising a RODC only exposes accounts whose hash is in its cache
- Use the **Denied RODC Password Replication Group** to protect sensitive accounts
- Regularly audit cached accounts on each RODC via `Get-ADDomainControllerPasswordReplicationPolicy`",

                ["Desc.Users.DeniedRODCReplication"] = @"# 👥 Denied RODC Password Replication Group

**Domain local** group created automatically when the domain is promoted.

## Role
- Defines accounts whose **passwords CANNOT be cached** on RODCs
- Protects critical accounts from exposure in case of RODC compromise

## Default Members
`krbtgt`, `Domain Admins`, `Schema Admins`, `Enterprise Admins`, `Group Policy Creator Owners`, `Read-Only Domain Controllers`

## ⚠️ Security
> 🟢 **Best practice — keep up to date**

- Add all critical service accounts and Tier 0 accounts to this group
- Never remove the default members
- Regularly audit the list to ensure it is comprehensive",

                ["Desc.Users.DnsUpdateProxy"] = @"# 👥 DnsUpdateProxy Group

**Global** group created automatically when the domain is promoted.

## Role
- Members authorized to **register DNS records** on behalf of other clients
- Typically used by **DHCP** servers for dynamic DNS updates

## ⚠️ Security
> 🟠 **Tier 1 — DNS squatting risk**

- **Limit** members to authorized DHCP servers only
- Records created by this group are initially ownerless — vector for **DNS hijacking**
- Configure the DHCP server with a dedicated account rather than using this group if possible
- If used, enable **DNS dynamic update credentials** on the DHCP server",

                ["Desc.Users.CloneableDCs"] = @"# 👥 Cloneable Domain Controllers Group

**Global** group created automatically when the domain is promoted (Windows Server 2012+).

## Role
- Members (DCs) that can be **cloned** to rapidly deploy additional domain controllers
- Cloning feature based on Hyper-V

## ⚠️ Security
> 🔴 **Tier 0 — DC cloning feature**

- Limit to DCs that actually need to be cloned
- A DC clone inherits the source DC's secrets — process must be strictly controlled
- Verify that the Hyper-V host is secured before any cloning",

                ["Desc.Users.ProtectedUsers"] = @"# 👥 Protected Users Group

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
- Protects against **Pass-the-Hash**, **Pass-the-Ticket**, **Overpass-the-Hash**",

                ["Desc.Users.KeyAdmins"] = @"# 👥 Key Admins Group

**Global** group created automatically (Windows Server 2016+).

## Role
- Members authorized to perform administrative actions on **msDS-KeyCredentialLink** attributes
- Used for managing **Windows Hello for Business** and FIDO2 keys at the domain level

## ⚠️ Security
> 🔴 **Tier 0 — Shadow Credentials risk**

- **Shadow Credentials** attack vector: a member can add alternative credentials to any domain account
- Documented attack: `Whisker`, `pyWhisker`
- Monitor modifications to the `msDS-KeyCredentialLink` attribute — Event ID 5136
- Minimize membership",

                ["Desc.Users.EnterpriseKeyAdmins"] = @"# 👥 Enterprise Key Admins Group

**Universal** group created automatically (Windows Server 2016+). Forest root domain.

## Role
- Equivalent of **Key Admins** but with scope across **all forest domains**
- Manages `msDS-KeyCredentialLink` attributes at forest level

## ⚠️ Security
> 🔴 **Tier 0 — Shadow Credentials risk (entire forest)**

- Same attack vector as **Key Admins** but with forest-wide scope
- **Keep empty** at all times unless there is a specific operational need
- Monitor any modification — Event ID 4728, 4729 and 5136",
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
                ["Relations.TabUserGroup"] = "🖥️ Objects → Group",
                ["Relations.TabGroupGroup"] = "👥 Group → Group",
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
                ["Relations.LabelUserSource"] = "User",
                ["Relations.LabelGroupMember"] = "Member group",
                ["Relations.LabelGroupParent"] = "Parent group (target)",
                ["Relations.ChooseUser"] = "Choose a user…",
                ["Relations.ColUser"] = "User",
                ["Relations.ColGroup"] = "Group",
                ["Relations.ColGroupMember"] = "Member group",
                ["Relations.ColGroupParent"] = "Parent group",
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
                ["About.FullName"] = "Simulate, Model and Audit Active Directory eXpert",
                ["About.Version"] = "Version 0.3.4",
                ["About.Description"] = "SMAD-X is an expert Active Directory simulator designed for training, documentation and experimentation. It generates an AD structure faithful to a fresh installation, with all default objects (Builtin, Users, Computers, System, GPOs, PSOs), and lets you visualize, edit and export it without any real infrastructure.",
                ["About.Features"] = "Key features:",
                ["About.Feature1"] = "• Complete default AD structure faithful to a fresh domain (Builtin, Users, Computers, System containers)",
                ["About.Feature2"] = "• Interactive force-directed graph view with type and tier filters",
                ["About.Feature3"] = "• GPO link management, PSO (Password Settings Objects) and MemberOf relationships",
                ["About.Feature4"] = "• Native JSON export and ready-to-deploy PowerShell scripts",
                ["About.Feature5"] = "• Microsoft tiering model (Tier 0/1/2) with configurable color coding",
                ["About.Feature6"] = "• Multilingual support (Français/English) and rich Markdown descriptions",
                ["About.Copyright"] = "© 2025 SMAD-X - All rights reserved",
                ["About.Close"] = "Close",

                // ── Sample structure descriptions (CreateSampleStructure) ──
                ["Desc.Sample.Domain"] = @"# Domain contoso.com

Demonstration Active Directory domain created by SMAD-X.

## Overview
This domain illustrates a typical AD architecture for a mid-size organization, segmented according to the **Microsoft tiering model** (Tier 0 / Tier 1 / Tier 2).

## Structure
| OU / Container | Role |
|---|---|
| Admin | Tier 0 administration accounts and groups |
| Servers | Tier 1 infrastructure servers |
| Users | Tier 2 standard user accounts |
| Workstations | Tier 2 workstations |
| Domain Controllers | Tier 0 domain controllers |

## ⚠️ Security
> This structure is provided for educational purposes. Adapt to real context before deployment.",

                ["Desc.Sample.AdminOU"] = @"# 🏛️ Admin OU (Tier 0)

Organizational unit containing the **top-level administration accounts and groups** of the domain.

## Role
- Hosts named (non-shared) administrator accounts used exclusively for domain administration tasks
- Contains Tier 0 administration groups
- Target of hardening GPOs specific to privileged accounts

## ⚠️ Security
> 🔴 **Tier 0 — Critical zone**

- Apply a **strict hardening GPO** to this OU (logon restrictions, enhanced auditing)
- Block GPO inheritance and only link validated GPOs
- Read/write access to this OU: **Domain Admins only**
- Monitor any object created or modified in this OU (Event ID 5137, 5136)
- These accounts must be used exclusively from **PAW (Privileged Access Workstation)**",

                ["Desc.Sample.AdminUser"] = @"# 👤 Administrator Account (Tier 0)

**Named** administrator account dedicated to domain administration.

## Role
- Used only for Active Directory administration tasks requiring Domain Admins rights
- Distinct from the domain's built-in `Administrator` account
- Must never be used for day-to-day tasks

## ⚠️ Security
> 🔴 **Tier 0 — High-value account**

- Use exclusively from a **PAW (Privileged Access Workstation)**
- Enable **MFA** (Windows Hello for Business, smart card)
- Add to **Protected Users** group to block NTLM and Kerberos delegation
- Monitor all logon events (Event ID 4624, 4625, 4648)
- Never use this account to browse the internet or read emails
- Rotate password every 60 days minimum",

                ["Desc.Sample.AdminDomainAdmins"] = @"# 👥 Domain Admins Group (Admin OU)

Local instance of the **Domain Admins** group in the Admin OU.

## Role
- Groups named administrator accounts holding Domain Admins rights
- Used to delegate domain administration to identified individuals

## ⚠️ Security
> 🔴 **Tier 0 — Ultra-privileged group**

- Keep membership at the **absolute minimum** (ideally 2–3 break-glass accounts)
- Audit members monthly
- Any addition must go through a change management process
- Monitor Event ID 4728 (member added) and 4729 (member removed)",

                ["Desc.Sample.ServersOU"] = @"# 🖥️ Servers OU (Tier 1)

Organizational unit containing the **infrastructure servers** of the domain.

## Role
- Hosts file servers, application servers, print servers, etc.
- Separated from user workstations (Tier 2) and domain controllers (Tier 0)
- Target of server configuration and hardening GPOs

## ⚠️ Security
> 🟠 **Tier 1 — Infrastructure**

- Apply **CIS Benchmarks** or equivalent for Windows Server via GPO
- Block Tier 2 accounts (standard users) from logging into Tier 1 servers
- Enable **Windows Defender Credential Guard** on all servers
- Monitor interactive logons (Event ID 4624 type 2 and 10)
- Deploy an **EDR** on all servers",

                ["Desc.Sample.FileServer"] = @"# 💾 SRV-FILE-01 — File Server (Tier 1)

Primary file server of the organization.

## Role
- Hosts organization network shares (business data, roaming profiles, work folders)
- Uses **NTFS ACLs** and **Share Permissions** for access control
- Typical shares: `\\SRV-FILE-01\Data`, `\\SRV-FILE-01\Profiles$`, `\\SRV-FILE-01\Home$`

## ⚠️ Security
> 🟠 **Tier 1 — Sensitive data**

- Enforce **SMB Signing** (prevents MITM / Relay attacks)
- Disable **SMBv1** (vulnerable to EternalBlue / WannaCry)
- Monitor share access (Event ID 5140, 5145) to detect ransomware
- Deploy a **honeypot file** in each share (decoy file alerting on abnormal access)
- Implement **least privilege** on shares and NTFS ACLs
- Regular backups with **immutable offline backups**",

                ["Desc.Sample.AppServer"] = @"# ⚙️ SRV-APP-01 — Application Server (Tier 1)

Server hosting the organization's business applications.

## Role
- Runs enterprise applications (ERP, intranet, internal web services, etc.)
- Uses **gMSA accounts** for application identities
- Communicates with Tier 1 database servers

## ⚠️ Security
> 🟠 **Tier 1 — Critical applications**

- Use **gMSA** (Group Managed Service Accounts) for all service accounts — never user accounts with static passwords
- Enable **Windows Defender Application Control (WDAC)** to restrict allowed executables
- Isolate applications in **dedicated service accounts** with the minimum required permissions
- Apply security updates monthly (Patch Tuesday)
- Monitor unusual processes (Event ID 4688 with commandline logging enabled)",

                ["Desc.Sample.UsersOU"] = @"# 👤 Users OU (Tier 2)

Organizational unit containing the **standard user accounts** of the organization.

## Role
- Hosts employee accounts for daily use (email, files, applications)
- Target of workstation configuration GPOs and user restrictions
- Can be subdivided by department (HR, Finance, IT, Management…)

## ⚠️ Security
> 🟢 **Tier 2 — Standard users**

- Apply **least privilege**: no account in this OU should be a local administrator
- Enable **Windows Defender Credential Guard** to protect in-memory credentials
- Deploy a **script restriction GPO** (SRP or WDAC) to limit PowerShell, WScript execution, etc.
- Train users on **phishing** and social engineering attacks
- Monitor logons outside normal hours and unusual data volumes",

                ["Desc.Sample.UserJdoe"] = @"# 👤 John Doe (jdoe) — Developer (Tier 2)

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
- If access to production environments is needed: create a separate Tier 1 account",

                ["Desc.Sample.UserAsmith"] = @"# 👤 Alice Smith (asmith) — Manager (Tier 2)

Standard user account for an organization manager.

## Role
- Access to management resources (executive files, HR tools, reporting)
- Likely target of **spear-phishing** and **Business Email Compromise (BEC)** attacks
- May have access to sensitive data (HR, finance, strategy)

## ⚠️ Security
> 🟢 **Tier 2 — High-risk target**

- Enable **MFA** on email and cloud applications (Microsoft 365, etc.)
- Specifically train on BEC attacks (executive impersonation, fraudulent wire transfers)
- Monitor access to financial and HR documents (DLP — Data Loss Prevention)
- Conditional access from managed devices only (Intune/Entra ID Conditional Access)",

                ["Desc.Sample.WorkstationsOU"] = @"# 💻 Workstations OU (Tier 2)

Organizational unit containing the **user workstations** of the organization.

## Role
- Hosts employees' laptops and desktops
- Target of configuration, security and software deployment GPOs
- Primary entry point for attackers in a corporate network

## ⚠️ Security
> 🟢 **Tier 2 — Primary intrusion vector**

- Apply **CIS Benchmarks** or equivalent for Windows 10/11 via GPO
- Enable **BitLocker** on all workstations (disk encryption)
- Disable **LLMNR** and **NetBIOS over TCP/IP** (prevents Responder / MITM)
- Restrict USB ports and removable media (via GPO or MDM solution)
- Deploy an **EDR** on all workstations
- Enable **Windows Defender Credential Guard** and **Device Guard**
- Monitor connections to unusual network shares (lateral movement)",

                ["Desc.Sample.Workstation1"] = @"# 💻 WKS-001 — John Doe's Workstation (Tier 2)

Workstation assigned to developer John Doe.

## Role
- Standard Windows 10/11 workstation joined to the domain
- Receives GPOs from the Workstations OU (configuration, security, deployment)
- Accesses network resources using jdoe's credentials

## ⚠️ Security
> 🟢 **Tier 2**

- BitLocker enabled with recovery key stored in AD
- No local administrator rights for jdoe
- Connections to Tier 1 resources only via authorized groups
- EDR deployed and SOC monitoring active",

                ["Desc.Sample.DomainControllersOU"] = @"# 🏛️ Domain Controllers OU (Tier 0)

System organizational unit containing all **domain controllers**.

## Role
- Automatically created when the first DC is promoted
- All DCs are automatically placed here upon promotion
- Exclusive target of the **Default Domain Controllers Policy** and DC hardening GPOs

## ⚠️ Security
> 🔴 **Tier 0 — Most critical zone of the domain**

- The **Default Domain Controllers Policy** must remain linked and must never be deleted
- Apply additional hardening GPOs: CIS DC Benchmark, etc.
- Prohibit interactive logons by Tier 1 or Tier 2 accounts on DCs
- Enable advanced auditing on all DCs (logons, AD modifications, Kerberos)
- Monitor Kerberos exploitation attempts (AS-REP Roasting, Kerberoasting) via Event IDs 4768, 4769, 4771
- Schedule and test regular System State backups",

                ["Desc.Sample.DC01"] = @"# 🖧 DC01 — Primary Domain Controller (Tier 0)

First domain controller of the contoso.com domain.

## Role
- Hosts all **FSMO roles** during initial installation:
  - **Schema Master**: controls AD schema modifications
  - **Domain Naming Master**: manages domain add/removal in the forest
  - **RID Master**: distributes RID pools for security object creation
  - **PDC Emulator**: time synchronization, account lockout management, NT4 compatibility
  - **Infrastructure Master**: maintains cross-domain references

## ⚠️ Security
> 🔴 **Tier 0 — Critical AD infrastructure**

- Never install third-party applications on a DC (minimize attack surface)
- Enable **Windows Defender Credential Guard** on the DC
- Restrict inbound/outbound network connections to the strict minimum (Windows Firewall)
- Monitor critical Event IDs: 4662 (AD object access), 4769 (Kerberos ticket), 4776 (NTLM)
- Set up a **standby DC** in a separate site/VLAN
- Include in a **Disaster Recovery plan** with tested System State backups",

                ["Desc.Sample.GMSA"] = @"# 🔐 svc-webapp — Group Managed Service Account (Tier 1)

Group Managed Service Account for the organization's web application.

## Role
- Service identity used by the web application hosted on SRV-APP-01
- Password is **automatically managed by AD** (rotated every 30 days, transparent to the application)
- Accessible only to authorized servers via the `msDS-GroupMSAMembership` attribute
- No interactive logon possible

## Advantages over classic service accounts
| Criterion | User account | gMSA |
|---|---|---|
| Password rotation | Manual | Automatic |
| Interactive logon | Possible | Impossible |
| Multi-server usage | Risky | Native |
| Centralized management | No | Yes |

## ⚠️ Security
> 🟠 **Tier 1 — Service account**

- Strictly control servers authorized to use this gMSA (`msDS-GroupMSAMembership`)
- Apply **least privilege**: only the permissions required by the application
- Monitor use of this account on unauthorized servers (Event ID 4624)
- Document resources accessed by this account",

                ["Desc.Sample.SystemContainer"] = @"# ⚙️ System Container

Active Directory system container holding configuration objects and system settings.

## Typical content
- `Password Settings Container`: PSO objects (Fine-Grained Password Policies)
- `Policies`: domain GPOs (AD object storage, outside SYSVOL)
- `WMI Filters`: WMI filters for GPO targeting
- `AdminSDHolder`: security template for protected objects

## ⚠️ Security
> 🔴 **Tier 0 — Do not modify manually**

- **AdminSDHolder**: monitor ACL modifications — any object matching it will be propagated to protected groups (Domain Admins, etc.) every 60 minutes by the SDProp process
- Never delete or move system objects from this container",

                ["Desc.Sample.PSOContainer"] = @"# 🔑 Password Settings Container

System container holding all **Password Settings Objects (PSO)** of the domain.

## Role
- Allows defining **granular password policies** per group or user (Fine-Grained Password Policy — FGPP)
- Available since Windows Server 2008 (domain functional level 2008+)
- Priority: the PSO with the lowest `msDS-PasswordSettingsPrecedence` value applies in case of conflict

## ⚠️ Security
> 🔴 **Tier 0 — Password policies**

- Create a strict PSO for Tier 0 accounts (length ≥ 16, rotation ≤ 60 days, lockout after 3 attempts)
- The PSO overrides the Default Domain Policy for targeted accounts
- Monitor PSO modifications (Event ID 5136)",

                ["Desc.Sample.PSOAdmin"] = @"# 🔑 PSO-Tier0-Admins — Admin Password Policy

Password Settings Object applied to **Tier 0 administration accounts**.

## Enhanced Security Settings
| Parameter | Value |
|---|---|
| Minimum length | **16 characters** |
| Complexity | Enabled |
| History | 24 passwords |
| Maximum age | **60 days** |
| Minimum age | 2 days |
| Lockout threshold | **3 attempts** |
| Lockout duration | **1 hour** (or manual unlock) |

## Targeted Objects
- **Domain Admins** group
- **Administrator** account

## ⚠️ Security
> 🔴 **Tier 0 — Critical policy**

- Precedence: **10** (high priority over Default Domain Policy)
- Consider **passphrases** (e.g.: 4 random words ≥ 20 characters) for memorability while maintaining security
- Pair with an **enterprise password manager** (CyberArk, BeyondTrust, etc.)
- This PSO should target **groups**, not individual users",

                ["Desc.Sample.PSOUsers"] = @"# 🔑 PSO-Standard-Users — Standard User Password Policy

Password Settings Object applied to **Tier 2 standard user accounts**.

## Standard Security Settings
| Parameter | Value |
|---|---|
| Minimum length | **12 characters** |
| Complexity | Enabled |
| History | 12 passwords |
| Maximum age | **90 days** |
| Minimum age | 1 day |
| Lockout threshold | **5 attempts** |
| Lockout duration | **30 minutes** |

## Targeted Objects
- Users **jdoe**, **asmith**

## ⚠️ Security
> 🟢 **Tier 2 — Standard policy**

- Precedence: **50** (lower priority than PSO-Tier0-Admins)
- Enable **self-service password reset (SSPR)** to reduce helpdesk load
- Consider integrating **Microsoft Entra ID Password Protection** to block common/compromised passwords (HaveIBeenPwned)",
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
