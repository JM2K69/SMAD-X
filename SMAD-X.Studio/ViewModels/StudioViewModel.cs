using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;
using SMADX.Services;
using SMADX.Services.AttackPaths;
using SMADX.Services.Audit;
using SMADX.Services.Labs;

namespace SMADX.Studio.ViewModels
{
    /// <summary>Recettes choisies dans la fenêtre « Nouvel exercice à partir d'une recommandation » : une seule, ou plusieurs à combiner.</summary>
    public sealed record RecipeRequest(IReadOnlyList<LabRecipe> Recipes, string Culture, string DomainName);

    /// <summary>Fenêtres de Studio vues par le ViewModel : sélecteurs de fichiers et choix de la recette.</summary>
    public interface IStudioDialogs
    {
        Task<string?> OpenFileAsync(string title, string filterName, string pattern);
        /// <summary>Plusieurs fichiers (les copies d'une classe) ; liste vide si l'utilisateur annule.</summary>
        Task<IReadOnlyList<string>> OpenFilesAsync(string title, string filterName, string pattern);
        Task<string?> PickFolderAsync(string title);
        Task<string?> SaveFileAsync(string title, string suggestedName, string filterName, string pattern);
        Task<RecipeRequest?> ChooseRecipeAsync();
        /// <summary>Avant de fermer, l'exercice a des modifications non enregistrées.</summary>
        Task<UnsavedChoice> AskUnsavedAsync();
    }

    /// <summary>
    /// SMAD-X Studio : un exercice ouvert, ses textes dans la langue éditée, ses objectifs, sa validation et
    /// la fiche du formateur. Les notes du formateur (la solution) sont enregistrées à côté de l'exercice,
    /// jamais dedans.
    /// </summary>
    public sealed partial class StudioViewModel : ObservableObject
    {
        private readonly IStudioDialogs _dialogs;
        private readonly LabService _labs = new();
        private readonly ADDataService _data = new();

        /// <summary>Domaine corrigé choisi par le formateur (enregistré depuis SMAD-X), utilisé tel quel.</summary>
        private ADObject? _solution;

        /// <summary>
        /// Domaine corrigé de l'exercice, aux dates du départ : généré à partir des recommandations, ou relu à côté
        /// de l'exercice (.solution.smad-x.json). Il est décalé à aujourd'hui, comme le départ, pour la validation.
        /// </summary>
        private ADRootDocument? _fixedDomain;

        public StudioViewModel(IStudioDialogs dialogs, Func<DateTime>? clock = null)
        {
            _dialogs = dialogs;
            Clock = clock ?? (() => DateTime.Now);
            _editCulture = Text.Culture;
            _status = Text["Status.Ready"];
            _classSummary = Text["Submissions.None"];
        }

        public static StudioText Text => StudioText.Instance;
        public Func<DateTime> Clock { get; }

        // ─── Exercice ouvert ─────────────────────────────────────────────────────

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasLab), nameof(LabId), nameof(LabTitle), nameof(Statement), nameof(Level), nameof(Duration), nameof(IsExam), nameof(ExamCheck),
            nameof(ExamCloseAudit), nameof(ExamCloseFixGuidance), nameof(ExamCloseAttackPaths), nameof(ExamCloseComparison),
                                  nameof(LanguageChoice), nameof(CategoryChoice), nameof(DomainSummary), nameof(TotalPoints))]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(SaveAsCommand), nameof(ExportSheetCommand), nameof(ExportSubjectCommand), nameof(ValidateCommand),
                                    nameof(SuggestCommand), nameof(AddObjectiveCommand), nameof(AddQuestionCommand), nameof(InsertTemplateCommand), nameof(ChooseSolutionCommand), nameof(ProposeSolutionCommand),
                                    nameof(AddSubmissionsCommand), nameof(AddSubmissionFolderCommand), nameof(RegradeSubmissionsCommand))]
        private LabDefinition? _lab;

        [ObservableProperty] private string? _filePath;
        [ObservableProperty] private string _status;
        [ObservableProperty] private string _notes = "";
        [ObservableProperty] private string _sheetPreview = "";
        [ObservableProperty] private string _solutionLabel = "";

        public bool HasLab => Lab != null;

        public ObservableCollection<ObjectiveItem> Objectives { get; } = new();
        public ObservableCollection<QuestionItem> Questions { get; } = new();
        public ObservableCollection<SuggestionItem> Suggestions { get; } = new();
        public ObservableCollection<IssueItem> Issues { get; } = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RemoveObjectiveCommand), nameof(MoveUpCommand), nameof(MoveDownCommand))]
        private ObjectiveItem? _selectedObjective;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RemoveQuestionCommand), nameof(MoveQuestionUpCommand), nameof(MoveQuestionDownCommand))]
        private QuestionItem? _selectedQuestion;

        [ObservableProperty] private bool _showSuggestions;

        // ─── Langue des textes édités ────────────────────────────────────────────

        public IReadOnlyList<Choice<string>> EditCultures { get; } = [new("fr-FR", "Français"), new("en-US", "English")];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LabTitle), nameof(Statement), nameof(EditCultureChoice))]
        private string _editCulture;

        public Choice<string> EditCultureChoice
        {
            get => EditCultures.First(c => c.Value == EditCulture);
            set { if (value != null) EditCulture = value.Value; }
        }

        partial void OnEditCultureChanged(string value)
        {
            foreach (var o in Objectives) o.Refresh();
            foreach (var q in Questions) q.Refresh();
            RefreshSheet();
        }

        // ─── Champs généraux ─────────────────────────────────────────────────────

        public string LabId
        {
            get => Lab?.Id ?? "";
            set { if (Lab == null) return; Lab.Id = value.Trim(); OnPropertyChanged(); }
        }

        public string LabTitle
        {
            get => Lab?.Title.GetValueOrDefault(EditCulture) ?? "";
            set { if (Lab == null) return; Lab.Title[EditCulture] = value; OnPropertyChanged(); }
        }

        public string Statement
        {
            get => Lab?.Statement.GetValueOrDefault(EditCulture) ?? "";
            set { if (Lab == null) return; Lab.Statement[EditCulture] = value; OnPropertyChanged(); }
        }

        public IReadOnlyList<Choice<LabLevel>> Levels => Enum.GetValues<LabLevel>()
            .Select(l => new Choice<LabLevel>(l, LocalizationService.Instance.Translate($"Lab.Level.{l}", Text.Culture))).ToList();

        public Choice<LabLevel>? Level
        {
            get => Lab == null ? null : Levels.First(l => l.Value == Lab.Level);
            set { if (Lab == null || value == null) return; Lab.Level = value.Value; OnPropertyChanged(); }
        }

        public int Duration
        {
            get => Lab?.DurationMinutes ?? 0;
            set { if (Lab == null) return; Lab.DurationMinutes = Math.Max(1, value); OnPropertyChanged(); }
        }

        /// <summary>Mode examen : dans SMAD-X, ni indice, ni score, ni « Recommencer ».</summary>
        public bool IsExam
        {
            get => Lab?.Exam ?? false;
            set { if (Lab == null) return; Lab.Exam = value; OnPropertyChanged(); RefreshSheet(); }
        }

        /// <summary>En examen, « Vérifier » montre les objectifs atteints, sans score ni explication.</summary>
        public bool ExamCheck
        {
            get => Lab?.ExamCheck ?? false;
            set { if (Lab == null) return; Lab.ExamCheck = value; OnPropertyChanged(); RefreshSheet(); }
        }

        public bool ExamCloseAudit { get => IsClosedInExam(LabTools.Audit); set => CloseInExam(LabTools.Audit, value); }
        public bool ExamCloseFixGuidance { get => IsClosedInExam(LabTools.FixGuidance); set => CloseInExam(LabTools.FixGuidance, value); }
        public bool ExamCloseAttackPaths { get => IsClosedInExam(LabTools.AttackPaths); set => CloseInExam(LabTools.AttackPaths, value); }
        public bool ExamCloseComparison { get => IsClosedInExam(LabTools.Comparison); set => CloseInExam(LabTools.Comparison, value); }

        private bool IsClosedInExam(LabTools tool) => Lab?.ExamClosedTools.HasFlag(tool) ?? false;

        private void CloseInExam(LabTools tool, bool closed, [System.Runtime.CompilerServices.CallerMemberName] string? property = null)
        {
            if (Lab == null) return;
            Lab.ExamClosedTools = closed ? Lab.ExamClosedTools | tool : Lab.ExamClosedTools & ~tool;
            OnPropertyChanged(property);
            RefreshSheet();
        }

        public IReadOnlyList<Choice<string?>> Languages =>
            [new(null, Text["Lab.Language.Both"]), new("fr-FR", "Français"), new("en-US", "English")];

        public Choice<string?>? LanguageChoice
        {
            get => Lab == null ? null : Languages.First(l => l.Value == Lab.Language);
            set
            {
                if (Lab == null || value == null) return;
                Lab.Language = value.Value;
                if (value.Value != null) EditCulture = value.Value;
                OnPropertyChanged();
            }
        }

        public IReadOnlyList<Choice<string?>> Categories =>
            [new(null, Text["Lab.Category.General"]), new("attack-paths", Text["Lab.Category.AttackPaths"])];

        public Choice<string?>? CategoryChoice
        {
            get => Lab == null ? null : Categories.FirstOrDefault(c => c.Value == Lab.Category) ?? Categories[0];
            set { if (Lab == null || value == null) return; Lab.Category = value.Value; OnPropertyChanged(); }
        }

        public int TotalPoints => Objectives.Where(o => !o.IsGuard).Sum(o => o.Points) + Questions.Sum(q => q.Points);

        public string DomainSummary => Lab == null ? "" : string.Format(Text.T(
            "{0} — {1} objets, {2} chemin(s) d'attaque hors Tier 0, score d'audit {3} / 100",
            "{0} — {1} objects, {2} attack path(s) outside Tier 0, audit score {3} / 100"),
            Lab.Start.Domain.Name, LabAuthoring.AllObjects(Lab.Start.Domain).Count(),
            new AttackPathService(Lab.Start.Domain, Clock()).PathsOutsideTier0().Count,
            new ADSecurityAuditService().Run(Lab.Start.Domain, Clock()).Score);

        // ─── Listes des champs de vérification ───────────────────────────────────

        public IReadOnlyList<LabCheckType> CheckTypes { get; } = Enum.GetValues<LabCheckType>();
        public IReadOnlyList<AuditSeverity> Severities { get; } = Enum.GetValues<AuditSeverity>();
        public IReadOnlyList<ADObjectType> ObjectTypes { get; } = Enum.GetValues<ADObjectType>();
        public IReadOnlyList<RightCategory> RightCategories { get; } = Enum.GetValues<RightCategory>();
        public IReadOnlyList<ADAccountFlags> Flags { get; } = Enum.GetValues<ADAccountFlags>().Where(f => f != ADAccountFlags.None).ToList();
        public IReadOnlyList<string> RuleIds { get; } = AuditRules.All.Select(r => r.Id).ToList();

        /// <summary>
        /// Formes des questions, pour la liste de l'éditeur : une seule liste pour toutes les questions (une liste propre
        /// à chacune, changée avec la question sélectionnée, ferait repousser l'ancienne forme dans la nouvelle question).
        /// </summary>
        public IReadOnlyList<Choice<LabQuestionKind>> QuestionKinds => _questionKinds ??= QuestionItem.KindChoices();
        private IReadOnlyList<Choice<LabQuestionKind>>? _questionKinds;

        /// <summary>Noms des objets du domaine de départ, pour les champs Objet, Groupe, Mandataire et Cible.</summary>
        public ObservableCollection<string> ObjectNames { get; } = new();

        // ─── Créer, ouvrir, enregistrer ──────────────────────────────────────────

        [RelayCommand]
        private async Task NewFromRecipeAsync()
        {
            if (await _dialogs.ChooseRecipeAsync() is not { } request) return;
            await RunAsync(() =>
            {
                var created = LabRecipes.CreateLab(request.Recipes, request.Culture, request.DomainName, Clock().Date);
                Load(created.Lab, null, created.TeacherNotes);
                SetFixedDomain(created.Solution, Text["Validation.FixedDomain.Generated"]);
                EditCulture = request.Culture;
                Status = Text["Status.Created"];
            });
        }

        [RelayCommand]
        private async Task NewFromDomainAsync()
        {
            if (await _dialogs.OpenFileAsync(Text["Menu.NewFromDomain"], Text["File.Domain"], "*.smad-x.json") is not { } path) return;
            await NewFromDomainFileAsync(path);
        }

        /// <summary>Nouvel exercice dont le domaine de départ est ce fichier ; le titre vient de son nom.</summary>
        public Task NewFromDomainFileAsync(string path) =>
            RunAsync(async () =>
            {
                var document = await LoadDocumentAsync(path);
                var culture = EditCulture;
                var title = Path.GetFileName(path).Split('.')[0];
                var objectives = LabAuthoring.Suggest(document.Domain, Clock()).Where(s => s.Recommended).Select(s => s.Objective).ToList();
                LabAuthoring.Renumber(objectives);
                var lab = new LabDefinition
                {
                    Id = title, Language = culture, ReferenceDate = Clock().Date,
                    Title = new LocalizedText { [culture] = title },
                    Statement = new LocalizedText { [culture] = LabAuthoring.StatementTemplate(culture, title) },
                    Start = document, Objectives = objectives
                };
                Load(lab, null, "");
                Status = Text["Status.Created"];
            });

        /// <summary>
        /// Fichier donné au lancement : un exercice s'ouvre, un domaine (envoyé par SMAD-X) devient le
        /// domaine de départ d'un nouvel exercice.
        /// </summary>
        public Task OpenPathAsync(string path)
        {
            if (path.EndsWith(LabService.FileExtension, StringComparison.OrdinalIgnoreCase))
                return OpenLabFileAsync(path);
            if (path.EndsWith(LabSubmissionService.FileExtension, StringComparison.OrdinalIgnoreCase))
            {
                Status = Text["Submissions.OpenLabFirst"];
                return Task.CompletedTask;
            }
            return NewFromDomainFileAsync(path);
        }

        [RelayCommand]
        private async Task ConvertScenarioAsync()
        {
            if (await _dialogs.OpenFileAsync(Text["Menu.Convert"], Text["File.Domain"], "*.smad-x.json") is not { } path) return;
            await RunAsync(async () =>
            {
                var document = await LoadDocumentAsync(path);
                var (lab, notes) = LabAuthoring.ConvertScenario(document, EditCulture, Path.GetFileName(path).Split('.')[0], Clock().Date);
                Load(lab, null, notes);
                Status = Text["Status.Created"];
            });
        }

        [RelayCommand]
        private async Task OpenAsync()
        {
            if (await _dialogs.OpenFileAsync(Text["Menu.Open"], Text["File.Lab"], "*" + LabService.FileExtension) is not { } path) return;
            await OpenLabFileAsync(path);
        }

        /// <summary>
        /// Ouvre un exercice, avec ses notes et son domaine corrigé. Rangé dans son dossier d'exercice, il retrouve
        /// aussi les copies déposées dans 3-copies-rendues, aussitôt corrigées.
        /// </summary>
        public Task OpenLabFileAsync(string path) =>
            RunAsync(async () =>
            {
                var project = LabProject.Of(path);
                var notesPath = project?.NotesPath ?? NotesPath(path);
                Load(_labs.LoadFile(path), path, File.Exists(notesPath) ? File.ReadAllText(notesPath) : "");
                var solutionPath = project?.SolutionPath ?? SolutionPath(path);
                if (File.Exists(solutionPath))
                {
                    SetFixedDomain(await LoadDocumentAsync(solutionPath), Path.GetFileName(solutionPath));
                    MarkSaved(); // relu avec l'exercice : rien de neuf à enregistrer
                }
                if (Lab!.Language is { } lang) EditCulture = lang;
                Status = string.Format(Text["Status.Opened"], path);
                if (project?.SubmissionFiles() is { Count: > 0 } submissions)
                    AddSubmissions(submissions);
                // Le sujet n'a ni indices d'examen ni corrigé : on corrige avec l'exemplaire
                if (LabProject.IsHandOut(path))
                    Status = Text["Status.OpenedSubject"];
            });

        /// <summary>Dossier de l'exercice (rangé par rôle), ou null pour un exercice enregistré seul.</summary>
        public LabProject? Project => FilePath != null ? LabProject.Of(FilePath) : null;

        [RelayCommand(CanExecute = nameof(HasLab))]
        private async Task SaveAsync()
        {
            if (FilePath == null) { await SaveAsAsync(); return; }
            await RunAsync(() => Save(FilePath));
        }

        /// <summary>Range l'exercice dans un dossier choisi : un sous-dossier à son nom, sauf si le dossier est vide.</summary>
        [RelayCommand(CanExecute = nameof(HasLab))]
        private async Task SaveAsAsync()
        {
            if (await _dialogs.PickFolderAsync(Text["Menu.SaveAs"]) is not { } folder) return;
            await RunAsync(() => SaveToFolder(folder));
        }

        /// <summary>Enregistre l'exercice dans un dossier d'exercice rangé par rôle ; renvoie le chemin de l'exemplaire.</summary>
        public string SaveToFolder(string folder)
        {
            var project = LabProject.Create(LabProject.RootFor(folder, LabId), Text.Culture);
            var path = project.LabPath(LabId);
            Save(path);
            return path;
        }

        /// <summary>
        /// Enregistre l'exercice. Rangé dans son dossier d'exercice, Studio y écrit tout : l'exemplaire, les notes du
        /// formateur, le domaine corrigé, la fiche formateur, le sujet à distribuer et le LISEZMOI. Enregistré seul,
        /// il écrit à côté les notes (.notes.md) et le domaine corrigé (.solution.smad-x.json).
        /// </summary>
        public void Save(string path)
        {
            if (Lab == null) return;
            CurrentLab();
            if (LabProject.Of(path) is { } project)
            {
                project.Save(Lab, path, Notes, _fixedDomain, Clock());
                FilePath = path;
                Status = string.Format(Text["Status.SavedProject"], project.Root);
                MarkSaved();
                return;
            }
            _labs.SaveFile(Lab, path);
            var notesPath = NotesPath(path);
            if (!string.IsNullOrWhiteSpace(Notes))
                File.WriteAllText(notesPath, Notes.ReplaceLineEndings("\n"));
            else if (File.Exists(notesPath))
                File.Delete(notesPath);
            if (_fixedDomain != null)
                File.WriteAllText(SolutionPath(path), System.Text.Json.JsonSerializer.Serialize(_fixedDomain, ADDataService.SerializerOptions));
            FilePath = path;
            Status = string.Format(Text["Status.Saved"], path);
            MarkSaved();
        }

        /// <summary>Exercice et notes au dernier enregistrement (ou à l'ouverture).</summary>
        private string? _savedSnapshot;

        /// <summary>Change avec le domaine corrigé : en proposer ou en choisir un est une modification à enregistrer.</summary>
        private int _solutionVersion;

        private string Snapshot() => Lab == null ? string.Empty
            : System.Text.Json.JsonSerializer.Serialize(CurrentLab(), ADDataService.SerializerOptions) + "\n" + Notes + "\n" + _solutionVersion;

        private void MarkSaved() => _savedSnapshot = Snapshot();

        public bool HasUnsavedChanges => Lab != null && Snapshot() != _savedSnapshot;

        /// <summary>Avant de fermer Studio : vrai si la fenêtre peut se fermer (rien à perdre, enregistré, ou abandonné).</summary>
        public async Task<bool> ConfirmCloseAsync()
        {
            if (!HasUnsavedChanges) return true;
            switch (await _dialogs.AskUnsavedAsync())
            {
                case UnsavedChoice.Discard:
                    return true;
                case UnsavedChoice.Save:
                    await SaveCommand.ExecuteAsync(null);
                    return !HasUnsavedChanges;
                default:
                    return false;
            }
        }

        /// <summary>« X.smadx-lab.json » → « X.smadx-lab.notes.md ».</summary>
        public static string NotesPath(string labPath) => WithoutJson(labPath) + ".notes.md";

        /// <summary>« X.smadx-lab.json » → « X.smadx-lab.solution.smad-x.json » : un domaine, qui s'ouvre dans SMAD-X.</summary>
        public static string SolutionPath(string labPath) => WithoutJson(labPath) + ".solution.smad-x.json";

        private static string WithoutJson(string path) =>
            path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? path[..^".json".Length] : path;

        /// <summary>
        /// Sujet à distribuer : l'exercice sans le corrigé des questions et, pour un examen, sans les indices. L'exemplaire
        /// ouvert dans Studio, lui, garde tout : c'est avec lui que les copies se corrigent.
        /// </summary>
        [RelayCommand(CanExecute = nameof(HasLab))]
        private async Task ExportSubjectAsync()
        {
            if (await _dialogs.SaveFileAsync(Text["Menu.ExportSubject"], LabService.SubjectFileName(LabId, EditCulture), Text["File.Lab"], "*" + LabService.FileExtension) is not { } path) return;
            await RunAsync(() => ExportSubject(path));
        }

        public void ExportSubject(string path)
        {
            if (Lab == null) return;
            if (FilePath != null && string.Equals(Path.GetFullPath(path), Path.GetFullPath(FilePath), StringComparison.OrdinalIgnoreCase))
            {
                Status = Text["Status.SubjectOverMaster"];
                return;
            }
            _labs.SaveSubject(CurrentLab(), path);
            Status = string.Format(Text["Status.SubjectSaved"], path);
        }

        [RelayCommand(CanExecute = nameof(HasLab))]
        private async Task ExportSheetAsync()
        {
            var suggested = LabId + Text.T(".formateur.md", ".trainer.md");
            if (await _dialogs.SaveFileAsync(Text["Menu.ExportSheet"], suggested, Text["File.Markdown"], "*.md") is not { } path) return;
            await RunAsync(() =>
            {
                File.WriteAllText(path, LabValidator.TeacherSheet(CurrentLab(), EditCulture, Notes, Clock(), SolutionDomain()));
                Status = string.Format(Text["Status.Saved"], path);
            });
        }

        private async Task<ADRootDocument> LoadDocumentAsync(string path)
        {
            var result = await _data.LoadDocumentAsync(path);
            return result.Document ?? throw new InvalidOperationException(result.Error ?? result.Status.ToString());
        }

        /// <summary>Affiche un exercice : objectifs, noms d'objets du domaine, aperçu de la fiche.</summary>
        public void Load(LabDefinition lab, string? path, string notes)
        {
            // Copie d'abord : l'aperçu de la fiche, rafraîchi par Notes, réécrit lab.Objectives depuis la liste affichée
            var objectives = lab.Objectives.ToList();
            var questions = lab.Questions.ToList();
            Objectives.Clear();
            Questions.Clear();
            Lab = lab;
            FilePath = path;
            Notes = notes;
            _solution = null;
            _fixedDomain = null;
            SolutionLabel = Text["Validation.NoSolution"];
            foreach (var o in objectives)
                Objectives.Add(NewItem(o));
            lab.Objectives = objectives;
            foreach (var q in questions)
                Questions.Add(NewQuestion(q));
            lab.Questions = questions;
            ObjectNames.Clear();
            ObjectNames.Add(AttackPathService.AnyUserId);
            foreach (var name in LabAuthoring.AllObjects(lab.Start.Domain).Select(o => o.Name).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
                ObjectNames.Add(name);
            Suggestions.Clear();
            ShowSuggestions = false;
            Issues.Clear();
            ResetSubmissions();
            SelectedObjective = Objectives.FirstOrDefault();
            SelectedQuestion = Questions.FirstOrDefault();
            OnPropertyChanged(nameof(TotalPoints));
            RefreshSheet();
            // Un exercice tout juste créé (recette, conversion) n'est pas encore enregistré
            if (path != null) MarkSaved();
            else _savedSnapshot = string.Empty;
        }

        private ObjectiveItem NewItem(LabObjective o)
        {
            var item = new ObjectiveItem(o, () => EditCulture);
            item.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ObjectiveItem.Points)) OnPropertyChanged(nameof(TotalPoints)); };
            return item;
        }

        private QuestionItem NewQuestion(LabQuestion q)
        {
            var item = new QuestionItem(q, () => EditCulture);
            item.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(QuestionItem.Points)) OnPropertyChanged(nameof(TotalPoints)); };
            return item;
        }

        private LabDefinition CurrentLab()
        {
            Lab!.Objectives = Objectives.Select(o => o.Model).ToList();
            Lab.Questions = Questions.Select(q => q.Model).ToList();
            return Lab;
        }

        // ─── Questions ───────────────────────────────────────────────────────────

        [RelayCommand(CanExecute = nameof(HasLab))]
        private void AddQuestion()
        {
            var question = new LabQuestion
            {
                Kind = LabQuestionKind.SingleChoice, Points = 1,
                Title = new LocalizedText { [EditCulture] = Text.T("Nouvelle question", "New question") }, Hint = new LocalizedText { [EditCulture] = "" }
            };
            var item = NewQuestion(question);
            item.AddChoiceCommand.Execute(null);
            item.AddChoiceCommand.Execute(null);
            Questions.Add(item);
            RenumberQuestions();
            SelectedQuestion = item;
        }

        private bool HasSelectedQuestion => SelectedQuestion != null;

        [RelayCommand(CanExecute = nameof(HasSelectedQuestion))]
        private void RemoveQuestion()
        {
            var index = Questions.IndexOf(SelectedQuestion!);
            Questions.Remove(SelectedQuestion!);
            RenumberQuestions();
            SelectedQuestion = Questions.Count == 0 ? null : Questions[Math.Min(index, Questions.Count - 1)];
        }

        [RelayCommand(CanExecute = nameof(HasSelectedQuestion))]
        private void MoveQuestionUp() => MoveQuestion(-1);

        [RelayCommand(CanExecute = nameof(HasSelectedQuestion))]
        private void MoveQuestionDown() => MoveQuestion(1);

        private void MoveQuestion(int delta)
        {
            var item = SelectedQuestion!;
            var index = Questions.IndexOf(item);
            var target = index + delta;
            if (target < 0 || target >= Questions.Count) return;
            Questions.Move(index, target);
            RenumberQuestions();
            SelectedQuestion = item;
        }

        /// <summary>Questions numérotées q1, q2… dans l'ordre.</summary>
        private void RenumberQuestions()
        {
            for (var i = 0; i < Questions.Count; i++)
                Questions[i].Model.Id = $"q{i + 1}";
            foreach (var q in Questions) q.Refresh();
            OnPropertyChanged(nameof(TotalPoints));
        }

        // ─── Énoncé ──────────────────────────────────────────────────────────────

        [RelayCommand(CanExecute = nameof(HasLab))]
        private void InsertTemplate() => Statement = LabAuthoring.StatementTemplate(EditCulture, LabTitle);

        // ─── Objectifs ───────────────────────────────────────────────────────────

        [RelayCommand(CanExecute = nameof(HasLab))]
        private void AddObjective()
        {
            var objective = new LabObjective
            {
                Points = 1, Title = new LocalizedText { [EditCulture] = Text.T("Nouvel objectif", "New objective") }, Hint = new LocalizedText { [EditCulture] = "" },
                Check = new LabCheck { Type = LabCheckType.NoAttackPath }
            };
            var item = NewItem(objective);
            Objectives.Add(item);
            Renumber();
            SelectedObjective = item;
        }

        private bool HasSelection => SelectedObjective != null;

        [RelayCommand(CanExecute = nameof(HasSelection))]
        private void RemoveObjective()
        {
            var index = Objectives.IndexOf(SelectedObjective!);
            Objectives.Remove(SelectedObjective!);
            Renumber();
            SelectedObjective = Objectives.Count == 0 ? null : Objectives[Math.Min(index, Objectives.Count - 1)];
        }

        [RelayCommand(CanExecute = nameof(HasSelection))]
        private void MoveUp() => Move(-1);

        [RelayCommand(CanExecute = nameof(HasSelection))]
        private void MoveDown() => Move(1);

        private void Move(int delta)
        {
            var item = SelectedObjective!;
            var index = Objectives.IndexOf(item);
            var target = index + delta;
            if (target < 0 || target >= Objectives.Count) return;
            Objectives.Move(index, target);
            Renumber();
            SelectedObjective = item;
        }

        private void Renumber()
        {
            var models = Objectives.Select(o => o.Model).ToList();
            LabAuthoring.Renumber(models);
            foreach (var o in Objectives) o.Refresh();
            OnPropertyChanged(nameof(TotalPoints));
        }

        [RelayCommand(CanExecute = nameof(HasLab))]
        private void Suggest()
        {
            Suggestions.Clear();
            var existing = Objectives.Select(o => System.Text.Json.JsonSerializer.Serialize(o.Model.Check)).ToHashSet();
            foreach (var s in LabAuthoring.Suggest(_labs.PrepareStart(Lab!, Clock()).Domain, Clock())
                         .Where(s => !existing.Contains(System.Text.Json.JsonSerializer.Serialize(s.Objective.Check))))
                Suggestions.Add(new SuggestionItem(s, EditCulture));
            ShowSuggestions = true;
        }

        [RelayCommand]
        private void AddSelectedSuggestions()
        {
            var selected = Suggestions.Where(s => s.IsSelected).ToList();
            foreach (var s in selected)
            {
                var o = s.Suggestion.Objective;
                // L'exercice ne garde que ses langues
                if (Lab!.Language is { } lang)
                {
                    o.Title = new LocalizedText { [lang] = o.Title.Resolve(lang) };
                    o.Hint = new LocalizedText { [lang] = o.Hint.Resolve(lang) };
                }
                Objectives.Add(NewItem(o));
                Suggestions.Remove(s);
            }
            Renumber();
            ShowSuggestions = Suggestions.Count > 0;
            Status = string.Format(Text["Status.Added"], selected.Count);
        }

        [RelayCommand]
        private void CloseSuggestions() => ShowSuggestions = false;

        // ─── Validation et fiche ─────────────────────────────────────────────────

        [RelayCommand(CanExecute = nameof(HasLab))]
        private void Validate()
        {
            var report = LabValidator.Validate(CurrentLab(), EditCulture, Clock(), SolutionDomain());
            Issues.Clear();
            foreach (var issue in report.Issues.OrderByDescending(i => i.Level))
                Issues.Add(new IssueItem(issue));
            Status = string.Format(Text["Status.Validated"], report.Issues.Count(i => i.Level == LabIssueLevel.Error), report.Warnings);
            RefreshSheet();
        }

        [RelayCommand(CanExecute = nameof(HasLab))]
        private async Task ChooseSolutionAsync()
        {
            if (await _dialogs.OpenFileAsync(Text["Validation.Solution"], Text["File.Domain"], "*.smad-x.json") is not { } path) return;
            await RunAsync(async () =>
            {
                // Fait dans SMAD-X, il a les dates du jour où l'exercice y a été ouvert : on le ramène à celles du départ
                ChooseSolution(await LoadDocumentAsync(path), Path.GetFileName(path));
                Validate();
            });
        }

        /// <summary>Domaine corrigé à la main : ramené aux dates du départ, il devient celui de l'exercice et se garde avec lui.</summary>
        public void ChooseSolution(ADRootDocument solution, string label)
        {
            if (Lab == null) return;
            SetFixedDomain(_labs.NormalizeSolution(Lab, solution), string.Format(Text["Validation.FixedDomain.Chosen"], label));
        }

        /// <summary>
        /// Propose une solution : la correction de chaque constat de l'audit du départ (voir <see cref="LabSolver"/>), puis la
        /// validation, qui dit ce qui reste à faire à la main.
        /// </summary>
        [RelayCommand(CanExecute = nameof(HasLab))]
        private void ProposeSolution()
        {
            var lab = CurrentLab();
            var proposed = LabSolver.Propose(lab);
            SetFixedDomain(proposed.Solution, Text["Validation.FixedDomain.Proposed"]);
            Validate();
            var now = Clock();
            var missed = new LabGrader().Grade(lab, _labs.PrepareStart(lab, now).Domain, _labs.PrepareSolution(lab, proposed.Solution, now).Domain, now)
                .Objectives.Count(o => !o.Passed);
            Status = missed == 0
                ? string.Format(Text["Status.SolutionProposed"], proposed.Fixes)
                : string.Format(Text["Status.SolutionPartial"], proposed.Fixes, missed);
        }

        /// <summary>Domaine corrigé par le formateur, utilisé tel quel par la validation (il remplace celui de l'exercice).</summary>
        public void SetSolution(ADObject? solution, string label)
        {
            _solution = solution;
            _fixedDomain = null;
            _solutionVersion++;
            SolutionLabel = label;
            RefreshSheet();
        }

        /// <summary>Domaine corrigé de l'exercice, aux dates du départ : enregistré avec lui, et décalé comme lui pour la validation.</summary>
        public void SetFixedDomain(ADRootDocument fixedDomain, string label)
        {
            _fixedDomain = fixedDomain;
            _solution = null;
            _solutionVersion++;
            SolutionLabel = string.Format(Text["Validation.FixedDomain"], label);
            RefreshSheet();
        }

        public bool HasFixedDomain => _fixedDomain != null;

        /// <summary>Domaine corrigé utilisé par la validation et la fiche : celui de l'exercice, ou celui choisi.</summary>
        private ADObject? SolutionDomain() =>
            _fixedDomain != null && Lab != null ? _labs.PrepareSolution(Lab, _fixedDomain, Clock()).Domain : _solution;

        partial void OnNotesChanged(string value) => RefreshSheet();

        [RelayCommand]
        private void RefreshSheet()
        {
            if (Lab == null) { SheetPreview = ""; return; }
            try { SheetPreview = LabValidator.TeacherSheet(CurrentLab(), EditCulture, Notes, Clock(), SolutionDomain()); }
            catch (Exception ex) { SheetPreview = ex.Message; }
        }

        // ─── Langue de l'interface ───────────────────────────────────────────────

        [RelayCommand]
        private void SetLanguage(string culture)
        {
            var ready = Status == Text["Status.Ready"];
            Text.Culture = culture;
            if (ready) Status = Text["Status.Ready"];
            _questionKinds = null;
            foreach (var name in new[] { nameof(Levels), nameof(Level), nameof(Languages), nameof(LanguageChoice), nameof(Categories), nameof(CategoryChoice), nameof(DomainSummary), nameof(QuestionKinds) })
                OnPropertyChanged(name);
            foreach (var q in Questions) q.Refresh();
            if (Lab?.Language == null) EditCulture = culture;
        }

        private async Task RunAsync(Func<Task> action)
        {
            try { await action(); }
            catch (Exception ex) { Status = string.Format(Text["Status.Error"], ex.Message); ErrorReporter.Log("Studio", ex); }
        }

        private Task RunAsync(Action action) => RunAsync(() => { action(); return Task.CompletedTask; });
    }
}
