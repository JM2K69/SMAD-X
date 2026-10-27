using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace SMADX.Studio
{
    /// <summary>
    /// Textes de l'interface de Studio, en français et en anglais. Les vues s'y lient par indexeur
    /// (<c>{Binding [Menu.File], Source={StaticResource S}}</c>) ; changer de langue rafraîchit tous les textes.
    /// </summary>
    public sealed class StudioText : INotifyPropertyChanged
    {
        public static StudioText Instance { get; } = new();

        private string _culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr" ? "fr-FR" : "en-US";

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>« fr-FR » ou « en-US ».</summary>
        public string Culture
        {
            get => _culture;
            set
            {
                if (_culture == value) return;
                _culture = value;
                // Les liaisons par indexeur d'Avalonia écoutent « Item » (ni « Item[] », ni un nom vide)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(French)));
            }
        }

        public bool French => _culture == "fr-FR";

        public string this[string key] => Texts.TryGetValue(key, out var t) ? (French ? t.Fr : t.En) : key;

        public string T(string fr, string en) => French ? fr : en;

        private static readonly Dictionary<string, (string Fr, string En)> Texts = new()
        {
            ["App.Title"] = ("SMAD-X Studio — création d'exercices", "SMAD-X Studio — lab authoring"),

            ["Menu.File"] = ("Fichier", "File"),
            ["Menu.NewFromRecipe"] = ("Nouvel exercice à partir d'une recommandation...", "New lab from a recommendation..."),
            ["Menu.NewFromDomain"] = ("Nouvel exercice à partir d'un domaine (.smad-x.json)...", "New lab from a domain (.smad-x.json)..."),
            ["Menu.Convert"] = ("Convertir un scénario en exercice...", "Convert a scenario into a lab..."),
            ["Menu.Open"] = ("Ouvrir un exercice...", "Open a lab..."),
            ["Menu.Save"] = ("Enregistrer", "Save"),
            ["Menu.SaveAs"] = ("Enregistrer sous...", "Save as..."),
            ["Menu.ExportSheet"] = ("Exporter la fiche formateur...", "Export trainer sheet..."),
            ["Menu.Quit"] = ("Quitter", "Exit"),
            ["Menu.Language"] = ("Langue", "Language"),

            ["Tab.Lab"] = ("Exercice", "Lab"),
            ["Tab.Statement"] = ("Énoncé", "Statement"),
            ["Tab.Objectives"] = ("Objectifs", "Objectives"),
            ["Tab.Validation"] = ("Validation", "Validation"),
            ["Tab.Sheet"] = ("Fiche formateur", "Trainer sheet"),
            ["Tab.Submissions"] = ("Copies", "Submissions"),

            ["Empty.Title"] = ("Aucun exercice ouvert", "No lab open"),
            ["Empty.Text"] = ("Créez un exercice à partir d'une recommandation de l'ANSSI, d'un domaine préparé dans SMAD-X ou d'un scénario, ou ouvrez un exercice existant.",
                              "Create a lab from an ANSSI recommendation, from a domain prepared in SMAD-X or from a scenario, or open an existing lab."),

            ["Lab.Id"] = ("Identifiant (nom du fichier)", "Identifier (file name)"),
            ["Lab.Title"] = ("Titre", "Title"),
            ["Lab.Language"] = ("Langue de l'exercice", "Lab language"),
            ["Lab.Language.Both"] = ("Bilingue (français et anglais)", "Bilingual (French and English)"),
            ["Lab.Category"] = ("Série dans le menu", "Menu series"),
            ["Lab.Category.General"] = ("Exercices généraux", "General labs"),
            ["Lab.Category.AttackPaths"] = ("Chemins d'attaque", "Attack paths"),
            ["Lab.Level"] = ("Niveau", "Level"),
            ["Lab.Duration"] = ("Durée indicative (minutes)", "Indicative duration (minutes)"),
            ["Lab.Domain"] = ("Domaine de départ", "Starting domain"),
            ["Lab.EditCulture"] = ("Langue des textes en cours d'édition", "Language of the texts being edited"),
            ["Lab.Points"] = ("Total des points", "Total points"),
            ["Lab.Exam"] = ("Mode examen", "Exam mode"),
            ["Lab.Exam.Hint"] = ("Dans SMAD-X : ni indice, ni vérification, ni « Recommencer », et aucun autre domaine ne peut être ouvert. L'apprenant enregistre sa copie, que vous corrigez dans l'onglet Copies.",
                                 "In SMAD-X: no hints, no checking, no Start over, and no other domain can be opened. The learner saves a submission, which you grade in the Submissions tab."),

            ["Statement.Template"] = ("Insérer le modèle", "Insert the template"),
            ["Statement.Preview"] = ("Aperçu, tel que l'apprenant le verra", "Preview, as the learner will see it"),
            ["Statement.Rule"] = ("L'énoncé donne le contexte, la mission et les contraintes, jamais la réponse : le chemin et la correction vont dans les notes du formateur.",
                                  "The statement gives the context, the mission and the constraints, never the answer: the path and the fix go into the trainer notes."),

            ["Objectives.Add"] = ("Ajouter", "Add"),
            ["Objectives.Remove"] = ("Supprimer", "Remove"),
            ["Objectives.Up"] = ("Monter", "Up"),
            ["Objectives.Down"] = ("Descendre", "Down"),
            ["Objectives.Suggest"] = ("Proposer des objectifs", "Suggest objectives"),
            ["Objectives.Suggestions"] = ("Propositions tirées de l'audit et des chemins d'attaque du domaine de départ", "Suggestions from the starting domain's audit and attack paths"),
            ["Objectives.AddSelected"] = ("Ajouter la sélection", "Add selection"),
            ["Objectives.CloseSuggestions"] = ("Fermer", "Close"),
            ["Objectives.NoSuggestion"] = ("Aucune autre proposition : l'exercice contient déjà tout ce que l'audit et les chemins d'attaque du domaine de départ suggèrent.",
                                           "No other suggestion: the lab already contains everything the starting domain's audit and attack paths suggest."),
            ["Objective.Title"] = ("Titre de l'objectif", "Objective title"),
            ["Objective.Hint"] = ("Indice (une piste, pas la correction)", "Hint (a lead, not the fix)"),
            ["Objective.Points"] = ("Points", "Points"),
            ["Objective.Check"] = ("Vérification", "Check"),
            ["Objective.Object"] = ("Objet", "Object"),
            ["Objective.ObjectType"] = ("Type d'objet", "Object type"),
            ["Objective.Group"] = ("Groupe", "Group"),
            ["Objective.Effective"] = ("Compter l'imbrication des groupes", "Count group nesting"),
            ["Objective.Rule"] = ("Règle d'audit", "Audit rule"),
            ["Objective.Severity"] = ("Gravité", "Severity"),
            ["Objective.Score"] = ("Score minimal", "Minimum score"),
            ["Objective.Flag"] = ("Option de compte", "Account option"),
            ["Objective.Tier"] = ("Tier", "Tier"),
            ["Objective.Trustee"] = ("Mandataire", "Trustee"),
            ["Objective.Target"] = ("Cible", "Target"),
            ["Objective.Right"] = ("Catégorie de droit", "Right category"),
            ["Objective.Guard"] = ("Garde : déjà atteinte au départ, elle refuse une solution brutale.", "Guard: already met at start, it refuses a brute-force solution."),
            ["Objective.AnyUser"] = ("Pour « n'importe quel utilisateur », saisissez any-domain-user ; laissez vide pour « aucune source hors Tier 0 ».",
                                     "For \"any domain user\", type any-domain-user; leave empty for \"no source outside Tier 0\"."),

            ["Validation.Run"] = ("Valider l'exercice", "Validate the lab"),
            ["Validation.Solution"] = ("Choisir le domaine corrigé (solution)...", "Choose the fixed domain (solution)..."),
            ["Validation.NoSolution"] = ("Aucune solution choisie : corrigez le domaine dans SMAD-X, enregistrez-le, puis choisissez-le ici pour vérifier qu'il obtient tous les points.",
                                         "No solution chosen: fix the domain in SMAD-X, save it, then choose it here to check that it gets all points."),
            ["Validation.Intro"] = ("Studio vérifie ce que vérifient les tests des exercices fournis : objectifs non atteints au départ, gardes efficaces, objets existants, textes présents, aucune solution visible par l'apprenant.",
                                    "Studio checks what the shipped labs' tests check: objectives not met at start, effective guards, existing objects, texts present, no solution visible to the learner."),

            ["Sheet.Notes"] = ("Notes du formateur : la solution, les points de vigilance. Elles ne sont jamais mises dans l'exercice.", "Trainer notes: the solution, things to watch for. They are never put into the lab."),
            ["Sheet.Preview"] = ("Aperçu de la fiche formateur", "Trainer sheet preview"),

            ["Recipe.Title"] = ("Nouvel exercice à partir d'une recommandation", "New lab from a recommendation"),
            ["Recipe.Intro"] = ("Choisissez le manque de configuration : Studio construit un domaine sain et peuplé, y injecte ce seul manque, puis propose l'énoncé et les objectifs.",
                                "Choose the misconfiguration: Studio builds a healthy, populated domain, injects only that misconfiguration, then suggests the statement and objectives."),
            ["Recipe.Domain"] = ("Nom du domaine", "Domain name"),
            ["Recipe.Language"] = ("Langue de l'exercice", "Lab language"),
            ["Recipe.Create"] = ("Créer l'exercice", "Create the lab"),
            ["Recipe.Cancel"] = ("Annuler", "Cancel"),
            ["Recipe.Check"] = ("Cochez plusieurs recommandations pour les combiner dans le même exercice ; sans case cochée, la ligne surlignée est utilisée.",
                                "Tick several recommendations to combine them in the same lab; with nothing ticked, the highlighted row is used."),
            ["Recipe.Combined"] = ("{0} recommandations combinées dans le même domaine : {1}", "{0} recommendations combined in the same domain: {1}"),
            ["Recipe.ClearChecks"] = ("Tout décocher", "Untick all"),
            ["Recipe.Filter"] = ("Filtrer (règle, point ANSSI, mot du titre)...", "Filter (rule, ANSSI point, title word)..."),

            ["File.Lab"] = ("Exercice SMAD-X", "SMAD-X lab"),
            ["File.Domain"] = ("Domaine SMAD-X", "SMAD-X domain"),
            ["File.Markdown"] = ("Markdown", "Markdown"),
            ["File.Submission"] = ("Copie SMAD-X", "SMAD-X submission"),
            ["File.Csv"] = ("CSV", "CSV"),

            ["Submissions.Intro"] = ("Ajoutez les copies (.smadx-submission.json) rendues par les apprenants : Studio les corrige avec l'exercice tel qu'il est ouvert ici, à la date où chaque apprenant a commencé et enregistré son travail. Le score qu'il a pu voir n'est jamais repris.",
                                     "Add the submissions (.smadx-submission.json) handed in by learners: Studio grades them with the lab as it is open here, at the date each learner started and saved their work. The score they may have seen is never reused."),
            ["Submissions.Add"] = ("Ajouter des copies...", "Add submissions..."),
            ["Submissions.AddFolder"] = ("Ajouter un dossier...", "Add a folder..."),
            ["Submissions.Regrade"] = ("Recorriger", "Regrade"),
            ["Submissions.Clear"] = ("Vider la liste", "Clear the list"),
            ["Submissions.ExportCsv"] = ("Exporter les notes (CSV)...", "Export grades (CSV)..."),
            ["Submissions.ExportReport"] = ("Exporter le bilan (Markdown)...", "Export class results (Markdown)..."),
            ["Submissions.Student"] = ("Apprenant", "Learner"),
            ["Submissions.Grade"] = ("Note", "Grade"),
            ["Submissions.Hints"] = ("Indices", "Hints"),
            ["Submissions.Objectives"] = ("Objectifs", "Objectives"),
            ["Submissions.SavedAt"] = ("Enregistrée le", "Saved at"),
            ["Submissions.Remarks"] = ("Remarques", "Remarks"),
            ["Submissions.Rates"] = ("Réussite par objectif", "Success per objective"),
            ["Submissions.Summary"] = ("{0} copie(s) corrigée(s) sur {1} — moyenne {2} / {3} ({4}), médiane {5}", "{0} of {1} submission(s) graded — average {2} / {3} ({4}), median {5}"),
            ["Submissions.NoneGraded"] = ("Aucune des {0} copie(s) n'a pu être corrigée : voir les remarques.", "None of the {0} submission(s) could be graded: see the remarks."),
            ["Submissions.None"] = ("Aucune copie pour l'instant.", "No submissions yet."),
            ["Submissions.Graded"] = ("{0} copie(s) corrigée(s).", "{0} submission(s) graded."),
            ["Submissions.OpenLabFirst"] = ("C'est une copie d'apprenant : ouvrez d'abord l'exercice, puis ajoutez la copie dans l'onglet Copies.",
                                            "This is a learner's submission: open the lab first, then add it in the Submissions tab."),

            ["Status.Ready"] = ("Prêt.", "Ready."),
            ["Status.Created"] = ("Exercice créé : relisez l'énoncé et les objectifs, puis validez.", "Lab created: review the statement and objectives, then validate."),
            ["Status.Saved"] = ("Enregistré : {0}", "Saved: {0}"),
            ["Status.Opened"] = ("Ouvert : {0}", "Opened: {0}"),
            ["Status.Validated"] = ("Validation : {0} erreur(s), {1} avertissement(s).", "Validation: {0} error(s), {1} warning(s)."),
            ["Status.Error"] = ("Erreur : {0}", "Error: {0}"),
            ["Status.Added"] = ("{0} objectif(s) ajouté(s).", "{0} objective(s) added."),
        };
    }
}
