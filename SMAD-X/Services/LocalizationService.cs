using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;

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

        public string this[string key] => Translate(key, _currentCulture.Name);

        /// <summary>Traduction dans une culture donnée (repli sur le français), sans changer la langue courante.</summary>
        public string Translate(string key, string cultureName)
        {
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

        private LocalizationService()
        {
            _currentCulture = CultureInfo.CurrentUICulture;
            _translations = new Dictionary<string, Dictionary<string, string>>();
            LoadTranslations();
            LoadMarkdownContent();
        }

        /// <summary>Préfixe des descriptions Markdown embarquées (voir Content/ et SMAD-X.csproj).</summary>
        private const string ContentPrefix = "Content/";

        /// <summary>
        /// Charge les descriptions pédagogiques (clés Desc.*) depuis les fichiers Content/{culture}/{clé}.md.
        /// Ils sont embarqués dans l'exécutable ; un dossier Content/ placé à côté de l'exécutable
        /// les remplace fichier par fichier, ce qui permet à un enseignant d'adapter le contenu sans recompiler.
        /// </summary>
        private void LoadMarkdownContent()
        {
            var assembly = typeof(LocalizationService).Assembly;
            foreach (var resource in assembly.GetManifestResourceNames())
            {
                var path = resource.Replace('\\', '/');
                if (!path.StartsWith(ContentPrefix, StringComparison.Ordinal) || !path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                    continue;

                using var stream = assembly.GetManifestResourceStream(resource)!;
                using var reader = new StreamReader(stream);
                AddMarkdown(path[ContentPrefix.Length..], reader.ReadToEnd());
            }

            var overrideDir = Path.Combine(AppContext.BaseDirectory, "Content");
            if (!Directory.Exists(overrideDir))
                return;

            foreach (var file in Directory.EnumerateFiles(overrideDir, "*.md", SearchOption.AllDirectories))
            {
                try
                {
                    AddMarkdown(Path.GetRelativePath(overrideDir, file).Replace('\\', '/'), File.ReadAllText(file));
                }
                catch (IOException)
                {
                    // Fichier illisible : on garde la version embarquée
                }
            }
        }

        /// <summary>Enregistre "fr-FR/Desc.Users.md" sous la clé Desc.Users de la culture fr-FR.</summary>
        private void AddMarkdown(string relativePath, string content)
        {
            var slash = relativePath.IndexOf('/');
            if (slash <= 0)
                return;

            var culture = relativePath[..slash];
            var key = Path.GetFileNameWithoutExtension(relativePath[(slash + 1)..]);
            if (!_translations.TryGetValue(culture, out var dictionary))
                _translations[culture] = dictionary = new Dictionary<string, string>();

            // Les fichiers peuvent avoir été extraits en CRLF par git : le rendu attend des \n
            dictionary[key] = content.Replace("\r\n", "\n");
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
                ["Menu.Help.Shortcuts"] = "Raccourcis clavier...",

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
                ["Details.Security"] = "Sécurité",
                ["Security.AccountDisabled"] = "Compte désactivé",
                ["Security.PasswordNotRequired"] = "Mot de passe non requis",
                ["Security.PasswordNeverExpires"] = "Le mot de passe n'expire jamais",
                ["Security.DontRequirePreauth"] = "Pré-authentification Kerberos non requise (AS-REP roasting)",
                ["Security.TrustedForDelegation"] = "Délégation non contrainte",
                ["Security.TrustedToAuthForDelegation"] = "Délégation contrainte avec transition de protocole",
                ["Security.NotDelegated"] = "Compte sensible, ne peut pas être délégué",
                ["Security.AdminCount"] = "adminCount = 1 (protégé par AdminSDHolder)",
                ["Security.Spn"] = "SPN (un par ligne) :",
                ["Security.AllowedToDelegateTo"] = "Délégation contrainte vers (un service par ligne) :",
                ["Security.GroupScope"] = "Étendue du groupe :",
                ["Security.GroupCategory"] = "Type de groupe :",
                ["Security.Owner"] = "Propriétaire :",
                ["Security.PwdLastSet"] = "Mot de passe changé le (pwdLastSet) :",
                ["Security.LastLogon"] = "Dernière connexion (lastLogonTimestamp) :",

                // Chemins d'attaque
                ["Menu.Analyse.AttackPaths"] = "Chemins d'attaque",
                ["AttackPath.Title"] = "Chemins d'attaque vers le contrôle du domaine",
                ["AttackPath.Intro"] = "Qui peut prendre le contrôle du domaine, et par où ? Chaque étape est un droit réel dans l'AD simulé. Les comptes de Tier 0 sont masqués par défaut : leur accès est attendu.",
                ["AttackPath.ShowTier0"] = "Inclure les comptes de Tier 0",
                ["AttackPath.Sources"] = "Sources",
                ["AttackPath.Steps"] = "Étapes du chemin",
                ["AttackPath.ShowInGraph"] = "Afficher dans le graphe",
                ["AttackPath.Refresh"] = "Actualiser",
                ["AttackPath.None"] = "Aucun chemin vers le contrôle du domaine depuis un compte hors Tier 0 : bravo !",
                ["AttackPath.Summary"] = "{0} source(s) peuvent atteindre le contrôle du domaine (dont {1} hors Tier 0).",
                ["AttackPath.StepCount"] = "{0} étape(s)",
                ["AttackPath.AnyUser"] = "N'importe quel utilisateur du domaine",
                ["AttackPath.Kind.MemberOf"] = "Membre de",
                ["AttackPath.Kind.PrivilegedGroup"] = "Groupe privilégié",
                ["AttackPath.Kind.DomainController"] = "Contrôleur de domaine",
                ["AttackPath.Kind.Owner"] = "Propriétaire",
                ["AttackPath.Kind.FullControl"] = "Contrôle total",
                ["AttackPath.Kind.ResetPassword"] = "Réinitialisation du mot de passe",
                ["AttackPath.Kind.WriteMembers"] = "Modification des membres",
                ["AttackPath.Kind.GpoLink"] = "GPO liée",
                ["AttackPath.Kind.Kerberoasting"] = "Kerberoasting",
                ["AttackPath.Kind.AsRepRoasting"] = "AS-REP roasting",
                ["AttackPath.Kind.EmptyPassword"] = "Mot de passe vide",
                ["AttackPath.Kind.UnconstrainedDelegation"] = "Délégation non contrainte",
                ["AttackPath.Kind.ConstrainedDelegationToDc"] = "Délégation vers un DC",
                ["AttackPath.Edge.MemberOf"] = "{0} est membre de {1} et hérite de ses droits.",
                ["AttackPath.Edge.PrivilegedGroup"] = "Les membres de {0} contrôlent le domaine (directement ou par des chemins d'élévation connus).",
                ["AttackPath.Edge.DomainController"] = "Contrôler {0}, c'est pouvoir extraire toutes les clés du domaine (DCSync).",
                ["AttackPath.Edge.Owner"] = "{0} est propriétaire de {1} : il peut réécrire ses permissions et s'en donner le contrôle total.",
                ["AttackPath.Edge.FullControl"] = "{0} a le contrôle total sur {1}.",
                ["AttackPath.Edge.FullControlInherited"] = "{0} a le contrôle total sur {1}, hérité par {2}.",
                ["AttackPath.Edge.ResetPassword"] = "{0} peut réinitialiser le mot de passe de {1} (délégation sur {2}).",
                ["AttackPath.Edge.WriteMembers"] = "{0} peut modifier les membres de {1} (délégation sur {2}) et s'y ajouter.",
                ["AttackPath.Edge.GpoLink"] = "La GPO {0} est liée à {1} : qui la modifie exécute du code sur {2}.",
                ["AttackPath.Edge.Kerberoasting"] = "Tout utilisateur peut demander un ticket de service pour {0} et casser son mot de passe hors ligne.",
                ["AttackPath.Edge.AsRepRoasting"] = "{0} n'exige pas la préauthentification Kerberos : son mot de passe peut être cassé hors ligne.",
                ["AttackPath.Edge.EmptyPassword"] = "{0} accepte un mot de passe vide.",
                ["AttackPath.Edge.Unconstrained"] = "{0} est en délégation non contrainte : un DC forcé à s'y authentifier y laisse son TGT.",
                ["AttackPath.Edge.ConstrainedToDc"] = "{0} peut déléguer vers {1}, un service d'un contrôleur de domaine.",

                // Annuler / rétablir, avant / après
                ["Menu.Edit.Undo"] = "Annuler",
                ["Menu.Edit.Redo"] = "Rétablir",
                ["Status.Undone"] = "Modification annulée.",
                ["Status.Redone"] = "Modification rétablie.",
                ["Menu.Analyse.Comparison"] = "Avant / après...",
                ["Menu.Analyse.MarkBaseline"] = "Poser le point de référence ici",
                ["Comparison.Title"] = "Avant / après",
                ["Comparison.Score"] = "Score d'audit",
                ["Comparison.Refresh"] = "Actualiser",
                ["Comparison.Fixed"] = "Constats corrigés",
                ["Comparison.New"] = "Nouveaux constats",
                ["Comparison.Changes"] = "Changements",
                ["Comparison.Summary"] = "Depuis le point de référence : {0} changement(s), {1} constat(s) corrigé(s), {2} nouveau(x) constat(s).",
                ["Comparison.BaselineSet"] = "Point de référence posé : « Avant / après » comparera désormais à l'état actuel.",

                // Édition des délégations
                ["Delegation.AddLabel"] = "Déléguer sur l'OU sélectionnée :",
                ["Delegation.Trustee"] = "Mandataire",
                ["Delegation.Add"] = "Ajouter",
                ["Delegation.Remove"] = "Retirer la délégation sélectionnée",

                // Exercices
                ["Menu.Labs"] = "Exercices",
                ["Menu.Labs.Open"] = "Ouvrir un exercice...",
                ["Menu.Labs.BuiltIn"] = "Exercices fournis",
                ["Menu.Labs.Create"] = "Créer un exercice à partir du domaine actuel...",
                ["FileType.Lab"] = "Exercice SMAD-X",
                ["Lab.Open.Title"] = "Ouvrir un exercice",
                ["Lab.Create.Title"] = "Enregistrer le nouvel exercice",
                ["Lab.Created"] = "Exercice créé : {0} ({1} objectif(s) proposés d'après l'audit). Rédigez l'énoncé dans le fichier.",
                ["Lab.Started"] = "Exercice « {0} » : domaine de départ chargé.",
                ["Lab.Level.Beginner"] = "Débutant",
                ["Lab.Level.Intermediate"] = "Intermédiaire",
                ["Lab.Level.Advanced"] = "Avancé",
                ["Lab.Duration"] = "Durée indicative : {0} min",
                ["Lab.Score"] = "{0} / {1} points",
                ["Lab.Objectives"] = "Objectifs",
                ["Lab.Hint"] = "Indice",
                ["Lab.Check"] = "Vérifier",
                ["Lab.Restart"] = "Recommencer",
                ["Lab.ExportResult"] = "Exporter le résultat",
                ["Lab.StudentName"] = "Nom de l'apprenant :",
                ["Lab.Completed"] = "Bravo, tous les objectifs sont atteints !",
                ["Lab.Export.Title"] = "Exporter le compte rendu de l'exercice",
                ["Lab.Report.Student"] = "Apprenant",
                ["Lab.Report.Lab"] = "Exercice",
                ["Lab.Report.Date"] = "Date",
                ["Lab.Report.Score"] = "Score",
                ["Lab.Report.Hints"] = "Indices consultés",
                ["Lab.Report.Audit"] = "Score d'audit final",
                ["Lab.Report.Objective"] = "Objectif",
                ["Lab.Report.Points"] = "Points",
                ["Lab.Report.Feedback"] = "Commentaire",
                ["Lab.Report.HintUsed"] = "indice consulté",
                ["Lab.Feedback.Ok"] = "Objectif atteint.",
                ["Lab.Feedback.Score"] = "Score d'audit actuel : {0}, il faut au moins {1}.",
                ["Lab.Feedback.Missing"] = "L'objet « {0} » est introuvable dans le domaine.",
                ["Lab.Feedback.StillPresent"] = "L'objet « {0} » existe encore.",
                ["Lab.Feedback.RuleRemaining"] = "Constat {0} encore présent sur : {1}.",
                ["Lab.Feedback.FindingsRemaining"] = "{0} constat(s) encore présent(s) : {1}.",
                ["Lab.Feedback.NotMember"] = "« {0} » n'est pas membre de « {1} ».",
                ["Lab.Feedback.StillMember"] = "« {0} » est encore membre de « {1} » (directement ou par imbrication).",
                ["Lab.Feedback.FlagMissing"] = "L'option {1} n'est pas activée sur « {0} ».",
                ["Lab.Feedback.FlagPresent"] = "L'option {1} est encore activée sur « {0} ».",
                ["Lab.Feedback.Tier"] = "« {0} » est classé en « {1} », attendu : « {2} ».",
                ["Lab.Feedback.DelegationMissing"] = "« {0} » n'a pas de délégation {1} sur « {2} ».",
                ["Lab.Feedback.NoDelegation"] = "« {0} » n'a aucune délégation.",
                ["Lab.Feedback.DelegationExtra"] = "« {0} » a des droits en trop : {1}.",
                ["Lab.Feedback.Modified"] = "« {0} » a été modifié : {1}.",
                ["Lab.Feedback.Removed"] = "supprimé",
                ["Lab.Feedback.UnknownCheck"] = "Vérification inconnue : {0}.",

                // Erreur inattendue
                ["Error.Title"] = "Erreur inattendue",
                ["Error.Intro"] = "Une erreur inattendue s'est produite. L'application continue de fonctionner, mais l'action en cours n'a pas abouti.",
                ["Error.Details"] = "Détails techniques",
                ["Error.LogFile"] = "Journal : {0}",
                ["Error.MoreErrors"] = "+ {0} autre(s) erreur(s) depuis, voir le journal.",
                ["Error.Copy"] = "Copier le détail",
                ["Error.OpenLog"] = "Ouvrir le dossier du journal",
                ["Error.Close"] = "Fermer",

                // Audit de sécurité
                ["Menu.Analyse.Audit"] = "Audit de sécurité",
                ["Audit.Window.Title"] = "Audit de sécurité",
                ["Audit.Run"] = "Relancer l'audit",
                ["Audit.Score"] = "Score",
                ["Audit.AnssiLevel"] = "Niveau ANSSI estimé",
                ["Audit.AnssiLevel.Note"] = "Niveau du point de contrôle ANSSI le plus critique en échec (5 = aucun). Estimation pédagogique, pas le calcul officiel de l'ANSSI.",
                ["Audit.Summary"] = "{0} constat(s), {1} règle(s) en échec sur {2} évaluées — date de référence {3:d}",
                ["Audit.AnssiLevel.Item"] = "Point ANSSI de niveau {0}",
                ["Audit.NoFinding"] = "Aucun constat : toutes les règles sont respectées.",
                ["Audit.Filter.All"] = "Toutes les gravités",
                ["Audit.Severity.Critical"] = "Critique",
                ["Audit.Severity.High"] = "Élevée",
                ["Audit.Severity.Medium"] = "Moyenne",
                ["Audit.Severity.Low"] = "Faible",
                ["Audit.Severity.Info"] = "Info",
                ["Audit.Column.Severity"] = "Gravité",
                ["Audit.Column.Rule"] = "Règle",
                ["Audit.Column.Object"] = "Objet",
                ["Audit.Column.Detail"] = "Détail",
                ["Audit.References"] = "Références",
                ["Audit.SelectInTree"] = "Afficher dans l'arbre",
                ["Audit.Badge.Tooltip"] = "{0} constat(s) d'audit, le plus grave : {1}",
                ["Audit.KRB-01.Detail"] = "SPN : {0}",
                ["Audit.KRB-02.Detail"] = "SPN : {0}",
                ["Audit.KRB-03.Detail"] = "Préauthentification Kerberos désactivée sur un compte privilégié",
                ["Audit.KRB-04.Detail"] = "Préauthentification Kerberos désactivée",
                ["Audit.KRB-05.Detail"] = "Mot de passe inchangé depuis {0} jours",
                ["Audit.DLG-01.Detail"] = "Délégation non contrainte activée",
                ["Audit.DLG-02.Detail"] = "Délégation avec transition de protocole vers {0} (contrôleur de domaine {1})",
                ["Audit.DLG-03.Detail"] = "Délégation vers {0} (contrôleur de domaine {1})",
                ["Audit.DLG-04.Detail"] = "Ni membre de Protected Users, ni marqué « compte sensible »",
                ["Audit.PWD-01.Detail"] = "Le mot de passe n'expire jamais",
                ["Audit.PWD-02.Detail"] = "Le mot de passe n'expire jamais",
                ["Audit.PWD-03.Detail"] = "Mot de passe non requis (PASSWD_NOTREQD)",
                ["Audit.PWD-04.Detail"] = "Mot de passe inchangé depuis {0} jours",
                ["Audit.PWD-05.Detail"] = "Mot de passe machine inchangé depuis {0} jours",
                ["Audit.PWD-06.Detail"] = "Mot de passe machine inchangé depuis {0} jours",
                ["Audit.PWD-07.Detail"] = "Mot de passe du gMSA inchangé depuis {0} jours",
                ["Audit.ACC-01.Detail"] = "Le compte Invité est activé",
                ["Audit.ACC-02.Detail"] = "Aucune connexion depuis {0} jours (dernière : {1})",
                ["Audit.PRIV-01.Detail"] = "Membre d'un groupe protégé avec adminCount à 0",
                ["Audit.PRIV-02.Detail"] = "adminCount = 1 sans appartenance à un groupe protégé",
                ["Audit.PRIV-03.Detail"] = "Membres : {0}",
                ["Audit.PRIV-04.Detail"] = "Membre de DnsAdmins sans être administrateur du domaine",
                ["Audit.PRIV-05.Detail"] = "Groupe protégé classé en {0}",
                ["Audit.ACL-01.Detail"] = "Propriétaire : {0}",
                ["Audit.TIER-01.Detail"] = "Objet de {0} membre de {1} ({2})",
                ["Audit.TIER-02.Detail"] = "GPO de {0} liée à l'OU {1} ({2})",
                ["Audit.TIER-03.Detail"] = "{0} ({1}) a le droit {2} sur cet objet de {3}",
                ["Audit.GRP-01.Detail"] = "Le groupe est membre de lui-même par imbrication",
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
                ["Status.GeneratedImportScript"] = "Script d'import généré : {0}  —  Exécutez-le sur votre contrôleur de domaine",
                ["Status.ErrorSave"] = "Erreur lors de la sauvegarde",
                ["Status.ErrorLoad"] = "Erreur lors du chargement",
                ["Status.Loaded.Migrated"] = " — format v{0} converti en v{1} : enregistrez pour mettre le fichier à jour",
                ["Status.ErrorLoadTooNew"] = "Ce fichier vient d'une version plus récente de SMAD-X (format v{0}, cette version lit jusqu'au format v{1}). Mettez SMAD-X à jour pour l'ouvrir.",
                ["Status.ErrorLoadInvalid"] = "Ce fichier n'est pas un document SMAD-X valide : {0}",
                ["Status.ErrorExportJson"] = "Erreur lors de l'export JSON",
                ["Status.ErrorExportPowerShell"] = "Erreur lors de l'export PowerShell",
                ["Status.ErrorGenerateImportScript"] = "Erreur lors de la génération du script d'import",
                ["Status.Error"] = "Erreur : {0}",

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

                // Boîtes de dialogue fichiers
                ["Dialog.Save.Title"] = "Sauvegarder la structure AD",
                ["Dialog.Load.Title"] = "Charger une structure AD",
                ["Dialog.ExportJson.Title"] = "Exporter la structure en JSON",
                ["Dialog.ExportPowerShell.Title"] = "Exporter en script PowerShell",
                ["Dialog.GenerateImportScript.Title"] = "Générer le script PowerShell d'import depuis AD",
                ["Dialog.ExportPng.Title"] = "Exporter l'arborescence en PNG",

                // Types de fichiers
                ["FileType.AllFiles"] = "Tous les fichiers",
                ["FileType.SMADXJSON"] = "SMAD-X JSON",
                ["FileType.JSON"] = "Fichiers JSON",
                ["FileType.PowerShell"] = "PowerShell",
                ["FileType.ImagePNG"] = "Image PNG",

                // Fenêtre des raccourcis clavier
                ["Shortcut.WindowTitle"] = "Raccourcis clavier",
                ["Shortcut.CloseButton"] = "Fermer",
                ["Shortcut.Header"] = "Raccourcis clavier disponibles",
                ["Shortcut.Tip"] = "Astuce : Vous pouvez aussi utiliser les menus et les boutons de la barre d'outils.",
                ["Shortcut.Ctrl_N"] = "Nouvelle structure",
                ["Shortcut.Ctrl_O"] = "Ouvrir un fichier",
                ["Shortcut.Ctrl_S"] = "Enregistrer",
                ["Shortcut.Ctrl_C"] = "Copier l'objet sélectionné",
                ["Shortcut.Ctrl_V"] = "Coller l'objet copié",
                ["Shortcut.F2"] = "Renommer l'objet sélectionné",
                ["Shortcut.Delete"] = "Supprimer l'objet sélectionné",

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
                ["Graph.FilterDelegations"] = "🔐 Délégations",
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
                ["About.Version"] = "Version 0.5.1",
                ["About.Description"] = "SMAD-X est un simulateur expert d'Active Directory conçu pour la formation, la documentation et l'expérimentation. Il génère une structure AD fidèle à une installation fraîche, gère la topologie des sites (sites, sous-réseaux, liens de réplication), et permet de tout visualiser, documenter et exporter sans infrastructure réelle.",
                ["About.Features"] = "Fonctionnalités principales :",
                ["About.Feature1"] = "• Structure AD par défaut complète et fidèle (Builtin, Users, Computers, System, GPOs, PSOs)",
                ["About.Feature2"] = "• Vue graphe de relations interactive (force-directed) avec filtres par type et tier",
                ["About.Feature3"] = "• Gestion des GPO (liaisons), PSO (Password Settings Objects) et relations MemberOf",
                ["About.Feature4"] = "• Export JSON natif et scripts PowerShell prêts à déployer (structure + import AD réel)",
                ["About.Feature5"] = "• Topologie des sites AD (sites, sous-réseaux, DCs, liens de réplication, GPOs liées)",
                ["About.Feature6"] = "• Export de rapports de documentation (Markdown, DOCX, PDF) avec sélection de thème",
                ["About.Feature7"] = "• Modèle de tiering Microsoft (Tier 0/1/2) et visualiseur de délégations avec export CSV",
                ["About.Feature8"] = "• Support multilingue (Français/English), thème Clair/Sombre, descriptions Markdown enrichies",
                ["About.Copyright"] = "© 2025-2026 SMAD-X - Tous droits réservés",
                ["About.Close"] = "Fermer",

                // Sites AD
                ["Menu.Sites"] = "Sites AD",
                ["Menu.Sites.View"] = "Voir les sites...",
                ["Sites.Window.Title"] = "Topologie des sites Active Directory",
                ["Sites.Tab.Table"] = "Tableau",
                ["Sites.Tab.Graph"] = "Graphe",
                ["Sites.Column.Name"] = "Nom",
                ["Sites.Column.Description"] = "Description",
                ["Sites.Column.Location"] = "Localisation",
                ["Sites.Column.Subnets"] = "Sous-réseaux",
                ["Sites.Column.DCs"] = "Contrôleurs de domaine",
                ["Sites.Column.Cost"] = "Coût",
                ["Sites.Column.Interval"] = "Intervalle (min)",
                ["Sites.Column.Transport"] = "Transport",
                ["Sites.Column.LinkedSites"] = "Sites liés",
                ["Sites.NoData"] = "Aucun site disponible — utilisez le script PowerShell pour importer la topologie.",
                ["Sites.Add"] = "Ajouter un site",
                ["Sites.Edit"] = "Modifier",
                ["Sites.Delete"] = "Supprimer",
                ["Sites.AddLink"] = "Ajouter un lien",
                ["Sites.Section.Sites"] = "Sites",
                ["Sites.Section.Links"] = "Liens de réplication",
                ["Sites.Label.Sites"] = "Sites",
                ["Sites.Label.Links"] = "Liens",
                ["Sites.Filter.Placeholder"] = "Filtrer par nom ou localisation…",
                ["Sites.Column.GPOs"] = "GPOs",
                ["Sites.Column.LinkName"] = "Nom du lien",
                ["Sites.Column.LinkSites"] = "Sites",
                ["Sites.Detail.Selected"] = "📍 Site sélectionné :",
                ["Sites.Detail.Subnets"] = "🌐  Sous-réseaux",
                ["Sites.Detail.DCs"] = "🖥  Contrôleurs de domaine",
                ["Sites.Detail.GPOs"] = "📄  GPOs liées",
                ["Sites.Status"] = "{0} site(s) — {1} lien(s)",
                ["Sites.NoSites"] = "Aucun site — importez la topologie via le script PowerShell (Fichier › Générer le script d'import)",

                // Export rapport
                ["Menu.File.ExportReport"] = "Exporter le rapport de documentation...",
                ["Menu.File.ImportDescription"] = "Importer une description (.md)...",
                ["Report.Dialog.Title"] = "Exporter le rapport de documentation",
                ["Report.Format.Markdown"] = "Markdown (.md)",
                ["Report.Format.Docx"] = "Word (.docx)",
                ["Report.Format.Pdf"] = "PDF (.pdf)",
                ["Report.Format.Label"] = "Format :",
                ["Report.Scope.Label"] = "Portée :",
                ["Report.Scope.Combined"] = "Domaine complet (combiné)",
                ["Report.Scope.Individual"] = "Élément individuel",
                ["Report.Scope.Object.Label"] = "Éléments documentés :",
                ["Report.Scope.TypeFilter"] = "Filtrer par type :",
                ["Report.Scope.TypeAll"] = "Tous les types",
                ["Report.Scope.SelectAll"] = "Tout sélectionner",
                ["Report.Scope.SelectNone"] = "Tout désélectionner",
                ["Report.Scope.SelectedCount"] = "{0} élément(s) sélectionné(s)",
                ["Report.Export.FolderTitle"] = "Choisir le dossier de destination",
                ["Report.Export.MultiDone"] = "{0} fichier(s) exporté(s) dans :",
                ["Report.Button.Export"] = "Exporter",
                ["Report.Button.Cancel"] = "Annuler",
                ["Report.Success"] = "Rapport exporté avec succès :",
                ["Report.Error"] = "Erreur lors de l'export du rapport :",
                ["Report.Theme.Label"] = "Thème :",
                ["Report.Theme.Plain"] = "Plain",
                ["Report.Theme.WordLike"] = "WordLike",
                ["Report.Theme.TechnicalDocument"] = "Technical Document",
                ["Report.Theme.GitHubLike"] = "GitHub",
                ["Report.Theme.Compact"] = "Compact",
                ["Report.Theme.Report"] = "Report",
                ["Report.Import.Title"] = "Importer une description Markdown",
                ["Report.Import.Success"] = "Description importée avec succès",
                ["Report.Import.Error"] = "Erreur lors de l'import de la description",
                ["Report.Import.NoSelection"] = "Aucun objet sélectionné — sélectionnez un objet dans l'arborescence avant d'importer",
                ["FileType.Markdown"] = "Fichier Markdown",
                ["FileType.Word"] = "Document Word",
                ["FileType.Pdf"] = "Document PDF",
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
                ["Menu.Help.Shortcuts"] = "Keyboard shortcuts...",

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
                ["Details.Security"] = "Security",
                ["Security.AccountDisabled"] = "Account is disabled",
                ["Security.PasswordNotRequired"] = "Password not required",
                ["Security.PasswordNeverExpires"] = "Password never expires",
                ["Security.DontRequirePreauth"] = "Do not require Kerberos preauthentication (AS-REP roasting)",
                ["Security.TrustedForDelegation"] = "Unconstrained delegation",
                ["Security.TrustedToAuthForDelegation"] = "Constrained delegation with protocol transition",
                ["Security.NotDelegated"] = "Account is sensitive and cannot be delegated",
                ["Security.AdminCount"] = "adminCount = 1 (protected by AdminSDHolder)",
                ["Security.Spn"] = "SPNs (one per line):",
                ["Security.AllowedToDelegateTo"] = "Constrained delegation to (one service per line):",
                ["Security.GroupScope"] = "Group scope:",
                ["Security.GroupCategory"] = "Group type:",
                ["Security.Owner"] = "Owner:",
                ["Security.PwdLastSet"] = "Password last set (pwdLastSet):",
                ["Security.LastLogon"] = "Last logon (lastLogonTimestamp):",

                // Attack paths
                ["Menu.Analyse.AttackPaths"] = "Attack paths",
                ["AttackPath.Title"] = "Attack paths to domain control",
                ["AttackPath.Intro"] = "Who can take control of the domain, and how? Each step is a real right in the simulated AD. Tier 0 accounts are hidden by default: their access is expected.",
                ["AttackPath.ShowTier0"] = "Include Tier 0 accounts",
                ["AttackPath.Sources"] = "Sources",
                ["AttackPath.Steps"] = "Path steps",
                ["AttackPath.ShowInGraph"] = "Show in graph",
                ["AttackPath.Refresh"] = "Refresh",
                ["AttackPath.None"] = "No path to domain control from an account outside Tier 0: well done!",
                ["AttackPath.Summary"] = "{0} source(s) can reach domain control ({1} outside Tier 0).",
                ["AttackPath.StepCount"] = "{0} step(s)",
                ["AttackPath.AnyUser"] = "Any domain user",
                ["AttackPath.Kind.MemberOf"] = "Member of",
                ["AttackPath.Kind.PrivilegedGroup"] = "Privileged group",
                ["AttackPath.Kind.DomainController"] = "Domain controller",
                ["AttackPath.Kind.Owner"] = "Owner",
                ["AttackPath.Kind.FullControl"] = "Full control",
                ["AttackPath.Kind.ResetPassword"] = "Password reset",
                ["AttackPath.Kind.WriteMembers"] = "Write members",
                ["AttackPath.Kind.GpoLink"] = "Linked GPO",
                ["AttackPath.Kind.Kerberoasting"] = "Kerberoasting",
                ["AttackPath.Kind.AsRepRoasting"] = "AS-REP roasting",
                ["AttackPath.Kind.EmptyPassword"] = "Empty password",
                ["AttackPath.Kind.UnconstrainedDelegation"] = "Unconstrained delegation",
                ["AttackPath.Kind.ConstrainedDelegationToDc"] = "Delegation to a DC",
                ["AttackPath.Edge.MemberOf"] = "{0} is a member of {1} and inherits its rights.",
                ["AttackPath.Edge.PrivilegedGroup"] = "Members of {0} control the domain (directly or through known escalation paths).",
                ["AttackPath.Edge.DomainController"] = "Controlling {0} means being able to extract every key of the domain (DCSync).",
                ["AttackPath.Edge.Owner"] = "{0} owns {1}: it can rewrite its permissions and grant itself full control.",
                ["AttackPath.Edge.FullControl"] = "{0} has full control over {1}.",
                ["AttackPath.Edge.FullControlInherited"] = "{0} has full control over {1}, inherited by {2}.",
                ["AttackPath.Edge.ResetPassword"] = "{0} can reset the password of {1} (delegation on {2}).",
                ["AttackPath.Edge.WriteMembers"] = "{0} can change the members of {1} (delegation on {2}) and add itself.",
                ["AttackPath.Edge.GpoLink"] = "GPO {0} is linked to {1}: whoever edits it runs code on {2}.",
                ["AttackPath.Edge.Kerberoasting"] = "Any user can request a service ticket for {0} and crack its password offline.",
                ["AttackPath.Edge.AsRepRoasting"] = "{0} does not require Kerberos preauthentication: its password can be cracked offline.",
                ["AttackPath.Edge.EmptyPassword"] = "{0} accepts an empty password.",
                ["AttackPath.Edge.Unconstrained"] = "{0} uses unconstrained delegation: a DC coerced into authenticating to it leaves its TGT there.",
                ["AttackPath.Edge.ConstrainedToDc"] = "{0} can delegate to {1}, a domain controller service.",

                // Undo / redo, before / after
                ["Menu.Edit.Undo"] = "Undo",
                ["Menu.Edit.Redo"] = "Redo",
                ["Status.Undone"] = "Change undone.",
                ["Status.Redone"] = "Change redone.",
                ["Menu.Analyse.Comparison"] = "Before / after...",
                ["Menu.Analyse.MarkBaseline"] = "Set the reference point here",
                ["Comparison.Title"] = "Before / after",
                ["Comparison.Score"] = "Audit score",
                ["Comparison.Refresh"] = "Refresh",
                ["Comparison.Fixed"] = "Fixed findings",
                ["Comparison.New"] = "New findings",
                ["Comparison.Changes"] = "Changes",
                ["Comparison.Summary"] = "Since the reference point: {0} change(s), {1} fixed finding(s), {2} new finding(s).",
                ["Comparison.BaselineSet"] = "Reference point set: \"Before / after\" will now compare with the current state.",

                // Delegation editing
                ["Delegation.AddLabel"] = "Delegate on the selected OU:",
                ["Delegation.Trustee"] = "Trustee",
                ["Delegation.Add"] = "Add",
                ["Delegation.Remove"] = "Remove selected delegation",

                // Labs
                ["Menu.Labs"] = "Labs",
                ["Menu.Labs.Open"] = "Open a lab...",
                ["Menu.Labs.BuiltIn"] = "Built-in labs",
                ["Menu.Labs.Create"] = "Create a lab from the current domain...",
                ["FileType.Lab"] = "SMAD-X lab",
                ["Lab.Open.Title"] = "Open a lab",
                ["Lab.Create.Title"] = "Save the new lab",
                ["Lab.Created"] = "Lab created: {0} ({1} objective(s) proposed from the audit). Write the statement in the file.",
                ["Lab.Started"] = "Lab \"{0}\": starting domain loaded.",
                ["Lab.Level.Beginner"] = "Beginner",
                ["Lab.Level.Intermediate"] = "Intermediate",
                ["Lab.Level.Advanced"] = "Advanced",
                ["Lab.Duration"] = "Suggested duration: {0} min",
                ["Lab.Score"] = "{0} / {1} points",
                ["Lab.Objectives"] = "Objectives",
                ["Lab.Hint"] = "Hint",
                ["Lab.Check"] = "Check",
                ["Lab.Restart"] = "Start over",
                ["Lab.ExportResult"] = "Export result",
                ["Lab.StudentName"] = "Learner name:",
                ["Lab.Completed"] = "Well done, every objective is met!",
                ["Lab.Export.Title"] = "Export the lab report",
                ["Lab.Report.Student"] = "Learner",
                ["Lab.Report.Lab"] = "Lab",
                ["Lab.Report.Date"] = "Date",
                ["Lab.Report.Score"] = "Score",
                ["Lab.Report.Hints"] = "Hints viewed",
                ["Lab.Report.Audit"] = "Final audit score",
                ["Lab.Report.Objective"] = "Objective",
                ["Lab.Report.Points"] = "Points",
                ["Lab.Report.Feedback"] = "Comment",
                ["Lab.Report.HintUsed"] = "hint viewed",
                ["Lab.Feedback.Ok"] = "Objective met.",
                ["Lab.Feedback.Score"] = "Current audit score: {0}, at least {1} is required.",
                ["Lab.Feedback.Missing"] = "Object \"{0}\" cannot be found in the domain.",
                ["Lab.Feedback.StillPresent"] = "Object \"{0}\" still exists.",
                ["Lab.Feedback.RuleRemaining"] = "Finding {0} is still present on: {1}.",
                ["Lab.Feedback.FindingsRemaining"] = "{0} finding(s) still present: {1}.",
                ["Lab.Feedback.NotMember"] = "\"{0}\" is not a member of \"{1}\".",
                ["Lab.Feedback.StillMember"] = "\"{0}\" is still a member of \"{1}\" (directly or through nesting).",
                ["Lab.Feedback.FlagMissing"] = "Option {1} is not enabled on \"{0}\".",
                ["Lab.Feedback.FlagPresent"] = "Option {1} is still enabled on \"{0}\".",
                ["Lab.Feedback.Tier"] = "\"{0}\" is classified as \"{1}\", expected: \"{2}\".",
                ["Lab.Feedback.DelegationMissing"] = "\"{0}\" has no {1} delegation on \"{2}\".",
                ["Lab.Feedback.NoDelegation"] = "\"{0}\" has no delegation.",
                ["Lab.Feedback.DelegationExtra"] = "\"{0}\" has extra rights: {1}.",
                ["Lab.Feedback.Modified"] = "\"{0}\" was modified: {1}.",
                ["Lab.Feedback.Removed"] = "deleted",
                ["Lab.Feedback.UnknownCheck"] = "Unknown check: {0}.",

                // Unexpected error
                ["Error.Title"] = "Unexpected error",
                ["Error.Intro"] = "An unexpected error occurred. The application keeps running, but the current action did not complete.",
                ["Error.Details"] = "Technical details",
                ["Error.LogFile"] = "Log: {0}",
                ["Error.MoreErrors"] = "+ {0} more error(s) since then, see the log.",
                ["Error.Copy"] = "Copy details",
                ["Error.OpenLog"] = "Open log folder",
                ["Error.Close"] = "Close",

                // Security audit
                ["Menu.Analyse.Audit"] = "Security audit",
                ["Audit.Window.Title"] = "Security audit",
                ["Audit.Run"] = "Run audit again",
                ["Audit.Score"] = "Score",
                ["Audit.AnssiLevel"] = "Estimated ANSSI level",
                ["Audit.AnssiLevel.Note"] = "Level of the most critical failed ANSSI control point (5 = none). Teaching estimate, not ANSSI's official computation.",
                ["Audit.Summary"] = "{0} finding(s), {1} failed rule(s) out of {2} evaluated — reference date {3:d}",
                ["Audit.AnssiLevel.Item"] = "ANSSI level {0} control point",
                ["Audit.NoFinding"] = "No finding: every rule is satisfied.",
                ["Audit.Filter.All"] = "All severities",
                ["Audit.Severity.Critical"] = "Critical",
                ["Audit.Severity.High"] = "High",
                ["Audit.Severity.Medium"] = "Medium",
                ["Audit.Severity.Low"] = "Low",
                ["Audit.Severity.Info"] = "Info",
                ["Audit.Column.Severity"] = "Severity",
                ["Audit.Column.Rule"] = "Rule",
                ["Audit.Column.Object"] = "Object",
                ["Audit.Column.Detail"] = "Detail",
                ["Audit.References"] = "References",
                ["Audit.SelectInTree"] = "Show in tree",
                ["Audit.Badge.Tooltip"] = "{0} audit finding(s), most severe: {1}",
                ["Audit.KRB-01.Detail"] = "SPN: {0}",
                ["Audit.KRB-02.Detail"] = "SPN: {0}",
                ["Audit.KRB-03.Detail"] = "Kerberos preauthentication disabled on a privileged account",
                ["Audit.KRB-04.Detail"] = "Kerberos preauthentication disabled",
                ["Audit.KRB-05.Detail"] = "Password unchanged for {0} days",
                ["Audit.DLG-01.Detail"] = "Unconstrained delegation enabled",
                ["Audit.DLG-02.Detail"] = "Delegation with protocol transition to {0} (domain controller {1})",
                ["Audit.DLG-03.Detail"] = "Delegation to {0} (domain controller {1})",
                ["Audit.DLG-04.Detail"] = "Neither in Protected Users nor marked as sensitive",
                ["Audit.PWD-01.Detail"] = "Password never expires",
                ["Audit.PWD-02.Detail"] = "Password never expires",
                ["Audit.PWD-03.Detail"] = "Password not required (PASSWD_NOTREQD)",
                ["Audit.PWD-04.Detail"] = "Password unchanged for {0} days",
                ["Audit.PWD-05.Detail"] = "Machine password unchanged for {0} days",
                ["Audit.PWD-06.Detail"] = "Machine password unchanged for {0} days",
                ["Audit.PWD-07.Detail"] = "gMSA password unchanged for {0} days",
                ["Audit.ACC-01.Detail"] = "The Guest account is enabled",
                ["Audit.ACC-02.Detail"] = "No logon for {0} days (last: {1})",
                ["Audit.PRIV-01.Detail"] = "Member of a protected group with adminCount 0",
                ["Audit.PRIV-02.Detail"] = "adminCount = 1 without membership in a protected group",
                ["Audit.PRIV-03.Detail"] = "Members: {0}",
                ["Audit.PRIV-04.Detail"] = "Member of DnsAdmins without being a domain administrator",
                ["Audit.PRIV-05.Detail"] = "Protected group classified as {0}",
                ["Audit.ACL-01.Detail"] = "Owner: {0}",
                ["Audit.TIER-01.Detail"] = "{0} object member of {1} ({2})",
                ["Audit.TIER-02.Detail"] = "{0} GPO linked to OU {1} ({2})",
                ["Audit.TIER-03.Detail"] = "{0} ({1}) has the right {2} on this {3} object",
                ["Audit.GRP-01.Detail"] = "The group is a member of itself through nesting",
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
                ["Status.GeneratedImportScript"] = "Import script generated: {0}  —  Run it on your domain controller",
                ["Status.ErrorSave"] = "Error saving structure",
                ["Status.ErrorLoad"] = "Error loading structure",
                ["Status.Loaded.Migrated"] = " — format v{0} converted to v{1}: save to update the file",
                ["Status.ErrorLoadTooNew"] = "This file comes from a newer version of SMAD-X (format v{0}; this version reads up to format v{1}). Update SMAD-X to open it.",
                ["Status.ErrorLoadInvalid"] = "This file is not a valid SMAD-X document: {0}",
                ["Status.ErrorExportJson"] = "Error exporting to JSON",
                ["Status.ErrorExportPowerShell"] = "Error exporting to PowerShell",
                ["Status.ErrorGenerateImportScript"] = "Error generating import script",
                ["Status.Error"] = "Error: {0}",

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

                // File dialogs
                ["Dialog.Save.Title"] = "Save AD Structure",
                ["Dialog.Load.Title"] = "Load AD Structure",
                ["Dialog.ExportJson.Title"] = "Export Structure to JSON",
                ["Dialog.ExportPowerShell.Title"] = "Export to PowerShell Script",
                ["Dialog.GenerateImportScript.Title"] = "Generate PowerShell AD Import Script",
                ["Dialog.ExportPng.Title"] = "Export Tree to PNG",

                // File types
                ["FileType.AllFiles"] = "All Files",
                ["FileType.SMADXJSON"] = "SMAD-X JSON",
                ["FileType.JSON"] = "JSON Files",
                ["FileType.PowerShell"] = "PowerShell",
                ["FileType.ImagePNG"] = "PNG Image",

                // Keyboard shortcuts window
                ["Shortcut.WindowTitle"] = "Keyboard Shortcuts",
                ["Shortcut.CloseButton"] = "Close",
                ["Shortcut.Header"] = "Available keyboard shortcuts",
                ["Shortcut.Tip"] = "Tip: You can also use the menus and toolbar buttons.",
                ["Shortcut.Ctrl_N"] = "New structure",
                ["Shortcut.Ctrl_O"] = "Open file",
                ["Shortcut.Ctrl_S"] = "Save",
                ["Shortcut.Ctrl_C"] = "Copy selected object",
                ["Shortcut.Ctrl_V"] = "Paste copied object",
                ["Shortcut.F2"] = "Rename selected object",
                ["Shortcut.Delete"] = "Delete selected object",

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
                ["Graph.FilterDelegations"] = "🔐 Delegations",
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
                ["About.Version"] = "Version 0.5.1",
                ["About.Description"] = "SMAD-X is an expert Active Directory simulator designed for training, documentation and experimentation. It generates an AD structure faithful to a fresh installation, manages site topology (sites, subnets, replication links), and lets you visualize, document and export everything without any real infrastructure.",
                ["About.Features"] = "Key features:",
                ["About.Feature1"] = "• Complete default AD structure faithful to a fresh domain (Builtin, Users, Computers, System, GPOs, PSOs)",
                ["About.Feature2"] = "• Interactive force-directed graph view with type and tier filters",
                ["About.Feature3"] = "• GPO link management, PSO (Password Settings Objects) and MemberOf relationships",
                ["About.Feature4"] = "• Native JSON export and ready-to-deploy PowerShell scripts (structure + real AD import)",
                ["About.Feature5"] = "• AD Sites topology (sites, subnets, DCs, replication links, linked GPOs)",
                ["About.Feature6"] = "• Documentation report export (Markdown, DOCX, PDF) with theme selection",
                ["About.Feature7"] = "• Microsoft tiering model (Tier 0/1/2) and delegations viewer with CSV export",
                ["About.Feature8"] = "• Multilingual support (Français/English), Light/Dark theme, rich Markdown descriptions",
                ["About.Copyright"] = "© 2025-2026 SMAD-X - All rights reserved",
                ["About.Close"] = "Close",

                // AD Sites
                ["Menu.Sites"] = "AD Sites",
                ["Menu.Sites.View"] = "View sites...",
                ["Sites.Window.Title"] = "Active Directory Sites Topology",
                ["Sites.Tab.Table"] = "Table",
                ["Sites.Tab.Graph"] = "Graph",
                ["Sites.Column.Name"] = "Name",
                ["Sites.Column.Description"] = "Description",
                ["Sites.Column.Location"] = "Location",
                ["Sites.Column.Subnets"] = "Subnets",
                ["Sites.Column.DCs"] = "Domain Controllers",
                ["Sites.Column.Cost"] = "Cost",
                ["Sites.Column.Interval"] = "Interval (min)",
                ["Sites.Column.Transport"] = "Transport",
                ["Sites.Column.LinkedSites"] = "Linked Sites",
                ["Sites.NoData"] = "No sites available — use the PowerShell script to import the topology.",
                ["Sites.Add"] = "Add site",
                ["Sites.Edit"] = "Edit",
                ["Sites.Delete"] = "Delete",
                ["Sites.AddLink"] = "Add link",
                ["Sites.Section.Sites"] = "Sites",
                ["Sites.Section.Links"] = "Replication Links",
                ["Sites.Label.Sites"] = "Sites",
                ["Sites.Label.Links"] = "Links",
                ["Sites.Filter.Placeholder"] = "Filter by name or location…",
                ["Sites.Column.GPOs"] = "GPOs",
                ["Sites.Column.LinkName"] = "Link name",
                ["Sites.Column.LinkSites"] = "Sites",
                ["Sites.Detail.Selected"] = "📍 Selected site:",
                ["Sites.Detail.Subnets"] = "🌐  Subnets",
                ["Sites.Detail.DCs"] = "🖥  Domain Controllers",
                ["Sites.Detail.GPOs"] = "📄  Linked GPOs",
                ["Sites.Status"] = "{0} site(s) — {1} link(s)",
                ["Sites.NoSites"] = "No sites — import topology via PowerShell script (File › Generate import script)",

                // Report export
                ["Menu.File.ExportReport"] = "Export Documentation Report...",
                ["Menu.File.ImportDescription"] = "Import description (.md)...",
                ["Report.Dialog.Title"] = "Export Documentation Report",
                ["Report.Format.Markdown"] = "Markdown (.md)",
                ["Report.Format.Docx"] = "Word (.docx)",
                ["Report.Format.Pdf"] = "PDF (.pdf)",
                ["Report.Format.Label"] = "Format:",
                ["Report.Scope.Label"] = "Scope:",
                ["Report.Scope.Combined"] = "Full domain (combined)",
                ["Report.Scope.Individual"] = "Individual element",
                ["Report.Scope.Object.Label"] = "Documented elements:",
                ["Report.Scope.TypeFilter"] = "Filter by type:",
                ["Report.Scope.TypeAll"] = "All types",
                ["Report.Scope.SelectAll"] = "Select all",
                ["Report.Scope.SelectNone"] = "Deselect all",
                ["Report.Scope.SelectedCount"] = "{0} element(s) selected",
                ["Report.Export.FolderTitle"] = "Choose destination folder",
                ["Report.Export.MultiDone"] = "{0} file(s) exported to:",
                ["Report.Button.Export"] = "Export",
                ["Report.Button.Cancel"] = "Cancel",
                ["Report.Success"] = "Report exported successfully:",
                ["Report.Error"] = "Error exporting report:",
                ["Report.Theme.Label"] = "Theme:",
                ["Report.Theme.Plain"] = "Plain",
                ["Report.Theme.WordLike"] = "WordLike",
                ["Report.Theme.TechnicalDocument"] = "Technical Document",
                ["Report.Theme.GitHubLike"] = "GitHub",
                ["Report.Theme.Compact"] = "Compact",
                ["Report.Theme.Report"] = "Report",
                ["Report.Import.Title"] = "Import Markdown Description",
                ["Report.Import.Success"] = "Description imported successfully",
                ["Report.Import.Error"] = "Error importing description",
                ["Report.Import.NoSelection"] = "No object selected — select an object in the tree before importing",
                ["FileType.Markdown"] = "Markdown file",
                ["FileType.Word"] = "Word document",
                ["FileType.Pdf"] = "PDF document",
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
