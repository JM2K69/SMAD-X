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
            ["Menu.OpenFolder"] = ("Ouvrir un exercice (son dossier)...", "Open a lab (its folder)..."),
            ["Menu.Open"] = ("Ouvrir un fichier d'exercice (.smadx-lab.json)...", "Open a lab file (.smadx-lab.json)..."),
            ["Menu.Save"] = ("Enregistrer", "Save"),
            ["Menu.SaveAs"] = ("Enregistrer dans un dossier...", "Save to a folder..."),
            ["Menu.ExportSheet"] = ("Exporter la fiche formateur...", "Export trainer sheet..."),
            ["Menu.ExportSubject"] = ("Exporter le sujet pour les apprenants...", "Export the subject for learners..."),
            ["Menu.Quit"] = ("Quitter", "Exit"),
            ["Menu.Language"] = ("Langue", "Language"),

            ["Tab.Lab"] = ("Exercice", "Lab"),
            ["Tab.Statement"] = ("Énoncé", "Statement"),
            ["Tab.Objectives"] = ("Objectifs", "Objectives"),
            ["Tab.Validation"] = ("Validation", "Validation"),
            ["Tab.Sheet"] = ("Fiche formateur", "Trainer sheet"),
            ["Tab.Questions"] = ("Questions", "Questions"),
            ["Tab.Submissions"] = ("Copies", "Submissions"),

            ["Empty.Title"] = ("Aucun exercice ouvert", "No lab open"),
            ["Empty.Text"] = ("Créez un exercice à partir d'une recommandation de l'ANSSI, d'un domaine préparé dans SMAD-X ou d'un scénario, ou ouvrez un exercice existant : son dossier contient tout (exemplaire, sujet, solution, copies). Vous pouvez aussi glisser ce dossier sur la fenêtre.",
                              "Create a lab from an ANSSI recommendation, from a domain prepared in SMAD-X or from a scenario, or open an existing lab: its folder holds everything (your copy, subject, solution, submissions). You can also drop that folder on the window."),
            ["Empty.Recent"] = ("Exercices récents", "Recent labs"),

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
            ["Lab.Subject.Hint"] = ("Votre fichier est l'exemplaire du formateur : il garde les indices et le corrigé des questions, et SMAD-X refuse de l'ouvrir en examen. Rangé dans son dossier, l'exercice a son sujet dans 2-a-distribuer, réécrit à chaque enregistrement : c'est lui qu'on distribue. Ce bouton l'écrit ailleurs.",
                                    "Your file is the trainer's copy: it keeps the hints and the answer key, and SMAD-X refuses to open it as an exam. Laid out in its folder, the lab has its subject in 2-to-hand-out, rewritten at every save: that is the file to hand out. This button writes it elsewhere."),
            ["Lab.Exam.Hint"] = ("Dans SMAD-X : ni indice, ni score, ni « Recommencer », et aucun autre domaine ne peut être ouvert ; le temps passé est mesuré. L'apprenant enregistre sa copie, que vous corrigez dans l'onglet Copies.",
                                 "In SMAD-X: no hints, no score, no Start over, and no other domain can be opened; the time spent is measured. The learner saves a submission, which you grade in the Submissions tab."),
            ["Unsaved.Title"] = ("Modifications non enregistrées", "Unsaved changes"),
            ["Unsaved.Message"] = ("L'exercice ou ses notes ont été modifiés depuis le dernier enregistrement. Voulez-vous les enregistrer avant de fermer ?",
                                   "The lab or its notes have changed since they were last saved. Do you want to save them before closing?"),
            ["Unsaved.Save"] = ("Enregistrer", "Save"),
            ["Unsaved.Discard"] = ("Ne pas enregistrer", "Don't save"),
            ["Unsaved.Cancel"] = ("Annuler", "Cancel"),
            ["Lab.ExamCheck"] = ("Autoriser « Vérifier »", "Allow \"Check\""),
            ["Lab.ExamCheck.Hint"] = ("L'apprenant voit quels objectifs sont atteints, sans score ni explication.", "The learner sees which objectives are met, with no score and no explanation."),
            ["Lab.ExamTools"] = ("Outils fermés pendant l'examen", "Tools closed during the exam"),
            ["Lab.ExamTools.Audit"] = ("Audit de sécurité", "Security audit"),
            ["Lab.ExamTools.FixGuidance"] = ("Section « Comment corriger » des fiches", "\"How to fix\" section of the sheets"),
            ["Lab.ExamTools.AttackPaths"] = ("Chemins d'attaque", "Attack paths"),
            ["Lab.ExamTools.Comparison"] = ("Comparaison avant / après", "Before / after comparison"),
            ["Lab.ExamTools.Hint"] = ("Tous les outils sont ouverts par défaut : ils aident l'apprenant à analyser, mais ils montrent aussi ce que la correction vérifie.",
                                      "All tools are open by default: they help the learner analyse, but they also show what grading checks."),

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
            ["Objective.IsPartial"] = ("Crédit partiel", "Partial credit"),
            ["Objective.Partial"] = ("La part du chemin parcouru rapporte la part des points : constats levés sur ceux du départ, chemins coupés, score gagné vers le score visé.",
                                     "The share of the way covered earns the share of the points: findings cleared out of those at start, paths cut, score gained towards the target."),
            ["Questions.Intro"] = ("Questions de compréhension, posées avec les objectifs et notées avec eux. Le corrigé (bons choix, associations, réponses acceptées) reste dans votre exemplaire : le sujet exporté ne le contient pas.",
                                   "Comprehension questions, asked with the objectives and graded with them. The answer key (right choices, pairs, accepted answers) stays in your copy: the exported subject does not contain it."),
            ["Questions.Add"] = ("Ajouter une question", "Add a question"),
            ["Question.Title"] = ("Question", "Question"),
            ["Question.Hint"] = ("Indice (hors examen)", "Hint (outside exams)"),
            ["Question.Kind"] = ("Forme", "Form"),
            ["Question.Points"] = ("Points", "Points"),
            ["Question.Partial"] = ("Crédit partiel : une réponse incomplète rapporte une part des points", "Partial credit: an incomplete answer earns part of the points"),
            ["Question.Choices"] = ("Choix (cochez les bonnes réponses)", "Choices (tick the right answers)"),
            ["Question.Distractors"] = ("Intrus : propositions qui ne correspondent à aucun élément", "Distractors: proposals that match no item"),
            ["Question.AddChoice"] = ("Ajouter un choix", "Add a choice"),
            ["Question.AddDistractor"] = ("Ajouter un intrus", "Add a distractor"),
            ["Question.Pairs"] = ("Paires : l'élément et la proposition qui lui correspond (elles sont mélangées dans le sujet)", "Pairs: the item and its matching proposal (they are shuffled in the subject)"),
            ["Question.AddPair"] = ("Ajouter une paire", "Add a pair"),
            ["Question.Accepted"] = ("Réponses acceptées, une par ligne", "Accepted answers, one per line"),
            ["Question.Accepted.Hint"] = ("Comparées sans casse, sans accents ni ponctuation finale. Une autre réponse, ou aucune réponse acceptée, se note à la main dans l'onglet Copies.",
                                          "Compared without case, accents or final punctuation. Any other answer, or no accepted answer, is graded by hand in the Submissions tab."),
            ["Objective.IsGuard"] = ("Garde", "Guard"),
            ["Objective.Guard"] = ("Une garde est déjà atteinte au départ et ne rapporte aucun point. Si l'apprenant la viole (en supprimant le compte au lieu de le corriger, par exemple), les objectifs qui concernaient cet objet au départ sont annulés.",
                                   "A guard is already met at start and earns no points. If the learner breaks it (deleting the account instead of fixing it, for example), the objectives that concerned this object at start are cancelled."),
            ["Objective.AnyUser"] = ("Pour « n'importe quel utilisateur », saisissez any-domain-user ; laissez vide pour « aucune source hors Tier 0 ».",
                                     "For \"any domain user\", type any-domain-user; leave empty for \"no source outside Tier 0\"."),

            ["Validation.Run"] = ("Valider l'exercice", "Validate the lab"),
            ["Validation.FixedDomain"] = ("Domaine corrigé : {0}", "Fixed domain: {0}"),
            ["Validation.FixedDomain.Generated"] = ("généré à partir des recommandations choisies, il applique la correction attendue de chacune. Il est enregistré avec l'exercice, dans 1-formateur, et s'ouvre dans SMAD-X.",
                                                    "generated from the chosen recommendations, it applies the expected fix of each one. It is saved with the lab, in 1-trainer, and opens in SMAD-X."),
            ["Validation.FixedDomain.Proposed"] = ("proposé par Studio : la correction de chaque constat de l'audit du départ, sans rien supprimer. Vérifiez-le ; à l'enregistrement, il est gardé avec l'exercice, dans 1-formateur.",
                                                   "suggested by Studio: the fix of each finding of the starting audit, deleting nothing. Check it; when you save, it is kept with the lab, in 1-trainer."),
            ["Validation.FixedDomain.Chosen"] = ("{0}, ramené aux dates du départ : à l'enregistrement, il est gardé avec l'exercice, dans 1-formateur.",
                                                 "{0}, brought back to the dates of the start: when you save, it is kept with the lab, in 1-trainer."),
            ["Validation.Propose"] = ("Proposer une solution", "Suggest a solution"),
            ["Validation.Solution"] = ("Choisir le domaine corrigé (solution)...", "Choose the fixed domain (solution)..."),
            ["Validation.NoSolution"] = ("Aucune solution : proposez-en une, ou corrigez le domaine dans SMAD-X, enregistrez-le, puis choisissez-le ici. Studio vérifie qu'elle obtient tous les points des objectifs et la garde avec l'exercice.",
                                         "No solution: suggest one, or fix the domain in SMAD-X, save it, then choose it here. Studio checks that it gets all the objectives' points and keeps it with the lab."),
            ["Validation.Intro"] = ("Studio vérifie ce que vérifient les tests des exercices fournis : objectifs non atteints au départ, gardes efficaces, objets existants, textes présents, aucune solution visible par l'apprenant.",
                                    "Studio checks what the shipped labs' tests check: objectives not met at start, effective guards, existing objects, texts present, no solution visible to the learner."),

            ["Sheet.Notes"] = ("Notes du formateur : la solution, les points de vigilance. Elles ne sont jamais mises dans l'exercice.", "Trainer notes: the solution, things to watch for. They are never put into the lab."),
            ["Sheet.Preview"] = ("Aperçu de la fiche formateur", "Trainer sheet preview"),

            ["Recipe.Title"] = ("Nouvel exercice à partir d'une recommandation", "New lab from a recommendation"),
            ["Recipe.Intro"] = ("Choisissez le manque de configuration : Studio construit un domaine sain et peuplé, y injecte ce seul manque, puis propose l'énoncé et les objectifs.",
                                "Choose the misconfiguration: Studio builds a healthy, populated domain, injects only that misconfiguration, then suggests the statement and objectives."),
            ["Recipe.Domain"] = ("Nom du domaine", "Domain name"),
            ["Recipe.Language"] = ("Langue de l'exercice", "Lab language"),
            ["Recipe.Server"] = ("Domaine promu par", "Domain promoted by"),
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

            ["Submissions.ExportMoodle"] = ("Exporter pour Moodle (CSV)...", "Export for Moodle (CSV)..."),
            ["Submissions.ExportReports"] = ("Comptes rendus individuels (PDF)...", "Individual reports (PDF)..."),
            ["Submissions.ReportsSaved"] = ("{0} compte(s) rendu(s) écrit(s) dans {1}.", "{0} report(s) written to {1}."),
            ["Submissions.Scale"] = ("Note sur", "Grade out of"),
            ["Submissions.ToGrade"] = ("à noter", "to grade"),
            ["Submissions.MarkedByHand"] = ("noté à la main", "graded by hand"),
            ["Submissions.ToReview"] = ("{0} réponse(s) à noter (* dans la colonne Note).", "{0} answer(s) to grade (* in the Grade column)."),
            ["Submissions.MarksSaved"] = ("Notes et appréciation de {0} enregistrées à côté de sa copie.", "{0}'s grades and comment saved beside the submission."),
            ["Submissions.Selected"] = ("Copie sélectionnée", "Selected submission"),
            ["Submissions.NoSelection"] = ("Sélectionnez une copie corrigée pour noter ses réponses et écrire une appréciation.", "Select a graded submission to grade its answers and write a comment."),
            ["Submissions.Answer"] = ("Réponse", "Answer"),
            ["Submissions.Automatic"] = ("Correction automatique", "Automatic grading"),
            ["Submissions.Manual"] = ("Points donnés (vide : la correction automatique compte)", "Points given (empty: automatic grading counts)"),
            ["Submissions.Comment"] = ("Appréciation (reprise dans le compte rendu)", "Comment (shown in the report)"),
            ["Submissions.SaveMarks"] = ("Enregistrer la note", "Save the grade"),
            ["Submissions.Intro"] = ("Glissez sur la fenêtre les copies (.smadx-submission.json) rendues par les apprenants, ou un dossier de copies, ou ajoutez-les ci-dessous : Studio les range dans 3-copies-rendues et les corrige avec l'exercice tel qu'il est ouvert ici, à la date où chaque apprenant a commencé et enregistré son travail. Les copies déposées directement dans 3-copies-rendues sont corrigées dès votre retour dans Studio. Le score que l'apprenant a pu voir n'est jamais repris.",
                                     "Drop on the window the submissions (.smadx-submission.json) handed in by learners, or a folder of submissions, or add them below: Studio files them in 3-submissions and grades them with the lab as it is open here, at the date each learner started and saved their work. Submissions dropped straight into 3-submissions are graded as soon as you come back to Studio. The score the learner may have seen is never reused."),
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
            ["Status.SolutionProposed"] = ("Solution proposée : {0} correction(s) ; elle atteint tous les objectifs.", "Solution suggested: {0} fix(es); it meets every objective."),
            ["Status.SolutionPartial"] = ("Solution proposée : {0} correction(s) ; {1} objectif(s) restent à atteindre à la main (voir la validation). Enregistrez, ouvrez le domaine corrigé de 1-formateur dans SMAD-X, terminez-le, enregistrez-le, puis choisissez-le ici.",
                                         "Solution suggested: {0} fix(es); {1} objective(s) remain to be met by hand (see the validation). Save, open the fixed domain of 1-trainer in SMAD-X, finish it, save it, then choose it here."),
            ["Status.SavedProject"] = ("Enregistré, rangé par rôle : {0}", "Saved, laid out by role: {0}"),
            ["Status.CreatedProject"] = ("Exercice créé et rangé par rôle dans {0} : relisez l'énoncé et les objectifs, puis validez.",
                                         "Lab created and laid out by role in {0}: review the statement and objectives, then validate."),
            ["Status.CreatedNotSaved"] = ("Exercice créé, pas encore enregistré : « Enregistrer » le rangera dans son dossier.",
                                          "Lab created, not saved yet: \"Save\" will lay it out in its folder."),
            ["Status.SubjectInProject"] = ("Exercice enregistré ; sujet à distribuer, sans indices ni corrigé : {0}", "Lab saved; subject to hand out, without hints or answer key: {0}"),
            ["Status.SheetInProject"] = ("Exercice enregistré ; fiche formateur : {0}", "Lab saved; trainer sheet: {0}"),
            ["Submissions.Filed"] = ("{0} copie(s) rangée(s) dans {1}.", "{0} submission(s) filed in {1}."),
            ["Create.PickFolder"] = ("Où ranger le nouvel exercice ? Studio y crée son dossier, rangé par rôle", "Where to keep the new lab? Studio creates its folder there, laid out by role"),
            ["Menu.ShowFolder"] = ("Ouvrir le dossier de l'exercice", "Open the lab folder"),
            ["Status.NotALabFolder"] = ("« {0} » n'est pas un dossier d'exercice : choisissez le dossier créé par Studio, celui qui contient 1-formateur.",
                                        "\"{0}\" is not a lab folder: choose the folder Studio created, the one holding 1-trainer."),
            ["Status.OpenedSubject"] = ("C'est le sujet distribué, sans indices ni corrigé : pour corriger les copies, ouvrez votre exemplaire, dans le dossier 1-formateur.",
                                        "This is the subject handed out, without hints or answer key: to grade submissions, open your copy, in the 1-trainer folder."),
            ["Submissions.ResultsSaved"] = ("Écrit dans le dossier des résultats : {0}", "Written to the results folder: {0}"),
            ["Status.SubjectSaved"] = ("Sujet à distribuer écrit, sans indices ni corrigé : {0}", "Subject to hand out written, without hints or answer key: {0}"),
            ["Status.SubjectOverMaster"] = ("Choisissez un autre fichier que votre exemplaire : le sujet le remplacerait, et vous perdriez le corrigé.",
                                            "Choose another file than your copy: the subject would replace it, and you would lose the answer key."),
        };
    }
}
