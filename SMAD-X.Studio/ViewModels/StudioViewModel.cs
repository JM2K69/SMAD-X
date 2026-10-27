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
        private ADObject? _solution;

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
        [NotifyPropertyChangedFor(nameof(HasLab), nameof(LabId), nameof(LabTitle), nameof(Statement), nameof(Level), nameof(Duration), nameof(IsExam),
                                  nameof(LanguageChoice), nameof(CategoryChoice), nameof(DomainSummary), nameof(TotalPoints))]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(SaveAsCommand), nameof(ExportSheetCommand), nameof(ValidateCommand),
                                    nameof(SuggestCommand), nameof(AddObjectiveCommand), nameof(InsertTemplateCommand), nameof(ChooseSolutionCommand),
                                    nameof(AddSubmissionsCommand), nameof(AddSubmissionFolderCommand), nameof(RegradeSubmissionsCommand))]
        private LabDefinition? _lab;

        [ObservableProperty] private string? _filePath;
        [ObservableProperty] private string _status;
        [ObservableProperty] private string _notes = "";
        [ObservableProperty] private string _sheetPreview = "";
        [ObservableProperty] private string _solutionLabel = "";

        public bool HasLab => Lab != null;

        public ObservableCollection<ObjectiveItem> Objectives { get; } = new();
        public ObservableCollection<SuggestionItem> Suggestions { get; } = new();
        public ObservableCollection<IssueItem> Issues { get; } = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RemoveObjectiveCommand), nameof(MoveUpCommand), nameof(MoveDownCommand))]
        private ObjectiveItem? _selectedObjective;

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

        /// <summary>Mode examen : dans SMAD-X, ni indice, ni vérification, ni « Recommencer ».</summary>
        public bool IsExam
        {
            get => Lab?.Exam ?? false;
            set { if (Lab == null) return; Lab.Exam = value; OnPropertyChanged(); RefreshSheet(); }
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

        public int TotalPoints => Objectives.Sum(o => o.Points);

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

        /// <summary>Noms des objets du domaine de départ, pour les champs Objet, Groupe, Mandataire et Cible.</summary>
        public ObservableCollection<string> ObjectNames { get; } = new();

        // ─── Créer, ouvrir, enregistrer ──────────────────────────────────────────

        [RelayCommand]
        private async Task NewFromRecipeAsync()
        {
            if (await _dialogs.ChooseRecipeAsync() is not { } request) return;
            await RunAsync(() =>
            {
                var (lab, notes) = LabRecipes.CreateLab(request.Recipes, request.Culture, request.DomainName, Clock().Date);
                Load(lab, null, notes);
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

        public Task OpenLabFileAsync(string path) =>
            RunAsync(() =>
            {
                var notesPath = NotesPath(path);
                Load(_labs.LoadFile(path), path, File.Exists(notesPath) ? File.ReadAllText(notesPath) : "");
                if (Lab!.Language is { } lang) EditCulture = lang;
                Status = string.Format(Text["Status.Opened"], path);
            });

        [RelayCommand(CanExecute = nameof(HasLab))]
        private async Task SaveAsync()
        {
            if (FilePath == null) { await SaveAsAsync(); return; }
            await RunAsync(() => Save(FilePath));
        }

        [RelayCommand(CanExecute = nameof(HasLab))]
        private async Task SaveAsAsync()
        {
            if (await _dialogs.SaveFileAsync(Text["Menu.SaveAs"], LabId + LabService.FileExtension, Text["File.Lab"], "*" + LabService.FileExtension) is not { } path) return;
            await RunAsync(() => Save(path));
        }

        /// <summary>Écrit l'exercice et, à côté, les notes du formateur (fichier .notes.md, jamais distribué).</summary>
        public void Save(string path)
        {
            if (Lab == null) return;
            Lab.Objectives = Objectives.Select(o => o.Model).ToList();
            _labs.SaveFile(Lab, path);
            var notesPath = NotesPath(path);
            if (!string.IsNullOrWhiteSpace(Notes))
                File.WriteAllText(notesPath, Notes.ReplaceLineEndings("\n"));
            else if (File.Exists(notesPath))
                File.Delete(notesPath);
            FilePath = path;
            Status = string.Format(Text["Status.Saved"], path);
        }

        /// <summary>« X.smadx-lab.json » → « X.smadx-lab.notes.md ».</summary>
        public static string NotesPath(string labPath) =>
            (labPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? labPath[..^".json".Length] : labPath) + ".notes.md";

        [RelayCommand(CanExecute = nameof(HasLab))]
        private async Task ExportSheetAsync()
        {
            var suggested = LabId + Text.T(".formateur.md", ".trainer.md");
            if (await _dialogs.SaveFileAsync(Text["Menu.ExportSheet"], suggested, Text["File.Markdown"], "*.md") is not { } path) return;
            await RunAsync(() =>
            {
                File.WriteAllText(path, LabValidator.TeacherSheet(CurrentLab(), EditCulture, Notes, Clock()));
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
            Objectives.Clear();
            Lab = lab;
            FilePath = path;
            Notes = notes;
            _solution = null;
            SolutionLabel = Text["Validation.NoSolution"];
            foreach (var o in objectives)
                Objectives.Add(NewItem(o));
            lab.Objectives = objectives;
            ObjectNames.Clear();
            ObjectNames.Add(AttackPathService.AnyUserId);
            foreach (var name in LabAuthoring.AllObjects(lab.Start.Domain).Select(o => o.Name).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
                ObjectNames.Add(name);
            Suggestions.Clear();
            ShowSuggestions = false;
            Issues.Clear();
            ResetSubmissions();
            SelectedObjective = Objectives.FirstOrDefault();
            OnPropertyChanged(nameof(TotalPoints));
            RefreshSheet();
        }

        private ObjectiveItem NewItem(LabObjective o)
        {
            var item = new ObjectiveItem(o, () => EditCulture);
            item.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ObjectiveItem.Points)) OnPropertyChanged(nameof(TotalPoints)); };
            return item;
        }

        private LabDefinition CurrentLab()
        {
            Lab!.Objectives = Objectives.Select(o => o.Model).ToList();
            return Lab;
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
            var report = LabValidator.Validate(CurrentLab(), EditCulture, Clock(), _solution);
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
                _solution = (await LoadDocumentAsync(path)).Domain;
                SolutionLabel = path;
                Validate();
            });
        }

        /// <summary>Domaine corrigé par le formateur, utilisé par la validation.</summary>
        public void SetSolution(ADObject? solution, string label)
        {
            _solution = solution;
            SolutionLabel = label;
        }

        partial void OnNotesChanged(string value) => RefreshSheet();

        [RelayCommand]
        private void RefreshSheet()
        {
            if (Lab == null) { SheetPreview = ""; return; }
            try { SheetPreview = LabValidator.TeacherSheet(CurrentLab(), EditCulture, Notes, Clock()); }
            catch (Exception ex) { SheetPreview = ex.Message; }
        }

        // ─── Langue de l'interface ───────────────────────────────────────────────

        [RelayCommand]
        private void SetLanguage(string culture)
        {
            var ready = Status == Text["Status.Ready"];
            Text.Culture = culture;
            if (ready) Status = Text["Status.Ready"];
            foreach (var name in new[] { nameof(Levels), nameof(Level), nameof(Languages), nameof(LanguageChoice), nameof(Categories), nameof(CategoryChoice), nameof(DomainSummary) })
                OnPropertyChanged(name);
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
