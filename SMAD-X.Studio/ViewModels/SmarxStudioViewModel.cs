using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;
using SMADX.Services.Labs;
using SMARX.Models;
using SMARX.Services.Labs;

namespace SMADX.Studio.ViewModels
{
    /// <summary>
    /// Un objectif d'un exercice SMAR-X dans Studio : son texte, ses points, et la vérification, dont seuls les champs
    /// utiles au type s'affichent. Les listes (comptes, chemins, opérations, accès nécessaires) s'écrivent une ligne par
    /// élément.
    /// </summary>
    public sealed partial class SmarxObjectiveEditItem : ObservableObject
    {
        private static StudioText Text => StudioText.Instance;
        private readonly SmarxStudioViewModel _owner;

        public SmarxObjectiveEditItem(SmarxObjective model, SmarxStudioViewModel owner)
        {
            Model = model;
            _owner = owner;
        }

        public SmarxObjective Model { get; }
        private SmarxCheck Check => Model.Check;
        private string Culture => _owner.EditCulture;

        public string Title
        {
            get => Model.Title.GetValueOrDefault(Culture) ?? "";
            set { Model.Title[Culture] = value; OnPropertyChanged(); OnPropertyChanged(nameof(Summary)); }
        }

        public string Hint
        {
            get => Model.Hint.GetValueOrDefault(Culture) ?? "";
            set { Model.Hint[Culture] = value; OnPropertyChanged(); }
        }

        public int Points
        {
            get => Model.Points;
            set { Model.Points = Math.Max(0, value); OnPropertyChanged(); OnPropertyChanged(nameof(Summary)); }
        }

        /// <summary>Une garde ne rapporte rien.</summary>
        public bool Guard
        {
            get => Model.Guard;
            set
            {
                Model.Guard = value;
                if (value) Model.Points = 0;
                else if (Model.Points == 0) Model.Points = 1;
                OnPropertyChanged(); OnPropertyChanged(nameof(Points)); OnPropertyChanged(nameof(IsScored)); OnPropertyChanged(nameof(Summary));
            }
        }

        public bool IsScored => !Model.Guard;

        /// <summary>Crédit partiel : chaque case juste rapporte sa part des points (« exactement les accès nécessaires »).</summary>
        public bool Partial
        {
            get => Model.Partial;
            set { Model.Partial = value && Check.Type == SmarxCheckType.ExactAccess; OnPropertyChanged(); }
        }

        // ─── La vérification ─────────────────────────────────────────────────────

        public static IReadOnlyList<Choice<SmarxCheckType>> TypeChoices() =>
            Enum.GetValues<SmarxCheckType>().Select(t => new Choice<SmarxCheckType>(t, Text[$"Smarx.Check.{t}"])).ToList();

        public static IReadOnlyList<Choice<SmarxSystem>> SystemChoices() =>
            [new(SmarxSystem.Linux, "Linux"), new(SmarxSystem.Windows, "Windows")];

        public static IReadOnlyList<Choice<SmarxHygieneRule>> RuleChoices() =>
            Enum.GetValues<SmarxHygieneRule>().Select(r => new Choice<SmarxHygieneRule>(r, Text[$"Smarx.Rule.{r}"])).ToList();

        public Choice<SmarxCheckType> TypeChoice
        {
            get => _owner.CheckTypes.First(c => c.Value == Check.Type);
            set
            {
                if (value == null || value.Value == Check.Type) return;
                Check.Type = value.Value;
                if (value.Value is SmarxCheckType.AccountUnchanged or SmarxCheckType.PathUnchanged) Guard = true;
                if (value.Value == SmarxCheckType.Hygiene) Check.Rule ??= Check.System == SmarxSystem.Linux ? SmarxHygieneRule.NoWorldWritable : SmarxHygieneRule.NoBroadWrite;
                if (value.Value == SmarxCheckType.AccountUnchanged) Check.Account ??= "*";
                if (value.Value != SmarxCheckType.ExactAccess) Model.Partial = false;
                RefreshAll();
            }
        }

        public Choice<SmarxSystem> SystemChoice
        {
            get => _owner.Systems.First(c => c.Value == Check.System);
            set
            {
                if (value == null || value.Value == Check.System) return;
                Check.System = value.Value;
                Check.Operation = null;
                Check.Share = null;
                RefreshAll();
            }
        }

        public IReadOnlyList<string> Accounts => _owner.AccountsOf(Check.System);
        public IReadOnlyList<string> PathsAvailable => _owner.PathsOf(Check.System);
        public IReadOnlyList<string> OperationsAvailable => _owner.OperationsOf(Check.System);
        /// <summary>Le premier choix, vide, veut dire « en local ».</summary>
        public IReadOnlyList<string> SharesAvailable => [Text["Smarx.Local"], .. _owner.SharesOf(Check.System)];

        public string? Account { get => Check.Account; set { Check.Account = Clean(value); OnPropertyChanged(); OnPropertyChanged(nameof(Summary)); } }
        public string? Other { get => Check.Other; set { Check.Other = Clean(value); OnPropertyChanged(); } }
        public string? Operation { get => Check.Operation; set { Check.Operation = Clean(value); OnPropertyChanged(); OnPropertyChanged(nameof(Summary)); } }
        public string? Path { get => Check.Path; set { Check.Path = Clean(value); OnPropertyChanged(); OnPropertyChanged(nameof(Summary)); } }

        public string Share
        {
            get => Check.Share ?? Text["Smarx.Local"];
            set { Check.Share = value == Text["Smarx.Local"] ? null : Clean(value); OnPropertyChanged(); OnPropertyChanged(nameof(Summary)); }
        }

        public string AccountsText { get => Lines(Check.Accounts); set { Check.Accounts = Split(value); OnPropertyChanged(); } }
        public string PathsText { get => Lines(Check.Paths); set { Check.Paths = Split(value); OnPropertyChanged(); } }
        public string OperationsText { get => Lines(Check.Operations); set { Check.Operations = Split(value); OnPropertyChanged(); } }
        public string NeedsText { get => Lines(Check.Needs); set { Check.Needs = Split(value) ?? []; OnPropertyChanged(); } }

        public Choice<SmarxHygieneRule>? RuleChoice
        {
            get => Check.Rule is { } rule ? _owner.Rules.First(r => r.Value == rule) : null;
            set { Check.Rule = value?.Value; OnPropertyChanged(); OnPropertyChanged(nameof(Summary)); }
        }

        // Les champs utiles à chaque type
        public bool ShowAccount => Check.Type is SmarxCheckType.AccessAllowed or SmarxCheckType.AccessDenied or SmarxCheckType.CreatedAllows or SmarxCheckType.AccountUnchanged;
        public bool ShowOther => Check.Type == SmarxCheckType.CreatedAllows;
        public bool ShowOperation => Check.Type is SmarxCheckType.AccessAllowed or SmarxCheckType.AccessDenied or SmarxCheckType.CreatedAllows;
        public bool ShowPath => Check.Type is not (SmarxCheckType.ExactAccess or SmarxCheckType.AccountUnchanged);
        public bool ShowShare => Check.Type is SmarxCheckType.AccessAllowed or SmarxCheckType.AccessDenied or SmarxCheckType.ExactAccess or SmarxCheckType.CreatedAllows
                                 || Check.Type == SmarxCheckType.Hygiene && Check.Rule == SmarxHygieneRule.ShareStandard;
        public bool ShowExact => Check.Type == SmarxCheckType.ExactAccess;
        public bool ShowRule => Check.Type == SmarxCheckType.Hygiene;
        public bool ShowAnyAccount => Check.Type == SmarxCheckType.AccountUnchanged;

        /// <summary>La ligne de la liste : numéro, points ou garde, ce qui est vérifié.</summary>
        public string Summary => $"{Model.Id} · {(Model.Guard ? "🛡" : $"{Model.Points} pt")} · {(Title.Length > 0 ? Title : Describe())}";

        /// <summary>Ce que vérifie l'objectif, en clair.</summary>
        public string Describe()
        {
            var via = Check.Share is { } s ? Text.T($" (partage {s})", $" (share {s})") : "";
            return Check.Type switch
            {
                SmarxCheckType.AccessAllowed => Text.T($"{Check.Account} peut {Check.Operation} {Check.Path}{via}", $"{Check.Account} can {Check.Operation} {Check.Path}{via}"),
                SmarxCheckType.AccessDenied => Text.T($"{Check.Account} ne peut pas {Check.Operation} {Check.Path}{via}", $"{Check.Account} cannot {Check.Operation} {Check.Path}{via}"),
                SmarxCheckType.ExactAccess => Text.T($"exactement les accès nécessaires{via}", $"exactly the accesses needed{via}"),
                SmarxCheckType.CreatedAllows => Text.T($"{Check.Other} peut {Check.Operation} ce que {Check.Account} crée dans {Check.Path}", $"{Check.Other} can {Check.Operation} what {Check.Account} creates in {Check.Path}"),
                SmarxCheckType.Hygiene => Check.Rule is { } r ? Text[$"Smarx.Rule.{r}"] : "",
                SmarxCheckType.AccountUnchanged => Text.T($"le compte {Check.Account} ne change pas", $"account {Check.Account} does not change"),
                _ => Text.T($"les droits de {Check.Path} ne changent pas", $"the permissions of {Check.Path} do not change")
            };
        }

        public void RefreshAll() => OnPropertyChanged(string.Empty);

        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        private static string Lines(List<string>? values) => string.Join("\n", values ?? []);
        private static List<string>? Split(string value)
        {
            var lines = value.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            return lines.Count > 0 ? lines : null;
        }
    }

    /// <summary>Une copie SMAR-X dans l'onglet « Copies » : note, indices, objectifs et questions, remarques.</summary>
    public sealed record SmarxSubmissionRow(SmarxGradedSubmission Graded, int MaxPoints, string Culture)
    {
        public SmarxSubmission? Submission => Graded.Submission;
        public SmarxLabResult? Result => Graded.Result;
        public string Student => Graded.Student;
        public string FileName => System.IO.Path.GetFileName(Graded.Path);
        public string SavedAt => Submission?.SavedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "";
        public double Points => Graded.Points;
        public string Grade => Result == null ? "—" : $"{SmarxClassGrader.Format(Points, CultureInfo.GetCultureInfo(Culture))} / {MaxPoints}";
        public string Percent => Result != null && MaxPoints > 0 ? $"{100.0 * Points / MaxPoints:0} %" : "";
        public int Hints => Graded.HintsUsed;

        public IReadOnlyList<ObjectiveMark> Marks => Result is not { } result ? []
            : result.Objectives.Select(o => new ObjectiveMark(o.Passed ? MarkState.Met : o.Fraction > 0 && !o.Annulled ? MarkState.Partial : MarkState.Missed,
                    $"{o.Objective.Id} · {o.Objective.Title.Resolve(Culture)} : {o.Feedback}"))
                .Concat(result.Questions.Select(q => new ObjectiveMark(q.NeedsReview ? MarkState.ToReview : q.Passed ? MarkState.Met : q.EarnedPoints > 0 ? MarkState.Partial : MarkState.Missed,
                    $"{q.Question.Id} · {q.Question.Title.Resolve(Culture)}")))
                .ToList();

        public string Remarks => string.Join(" ", Graded.Issues.Select(i => i.Message));
        public string RemarksColor => Graded.Issues.Any(i => i.Level == LabIssueLevel.Error) ? "#E74C3C"
            : Graded.Issues.Any(i => i.Level == LabIssueLevel.Warning) ? "#F39C12" : "#27AE60";
    }

    /// <summary>
    /// Un exercice SMAR-X ouvert dans Studio : ses textes dans la langue éditée, ses objectifs vérifiés par les moteurs
    /// de SMAR-X, ses questions, sa validation (le départ échoue, la solution réussit) et la correction d'une classe. La
    /// solution (les serveurs corrigés, enregistrés depuis SMAR-X) se range à côté de l'exercice, jamais dedans.
    /// </summary>
    public sealed partial class SmarxStudioViewModel : ObservableObject
    {
        public const string SolutionExtension = ".solution" + SmarxDocument.FileExtension;
        private static StudioText Text => StudioText.Instance;
        private readonly IStudioDialogs _dialogs;
        private readonly Action<string> _status;
        private readonly List<string> _submissionPaths = new();

        public SmarxStudioViewModel(SmarxLab lab, string? filePath, IStudioDialogs dialogs, Action<string> status, string culture)
        {
            Lab = lab;
            _filePath = filePath;
            _dialogs = dialogs;
            _status = status;
            _editCulture = culture;
            foreach (var objective in lab.Objectives) Objectives.Add(Watch(new SmarxObjectiveEditItem(objective, this)));
            foreach (var question in lab.Questions) Questions.Add(Watch(new QuestionItem(question, () => EditCulture)));
            SelectedObjective = Objectives.FirstOrDefault();
            SelectedQuestion = Questions.FirstOrDefault();
            if (filePath != null && File.Exists(SolutionPath(filePath)))
            {
                try { Solution = SmarxDocument.Parse(File.ReadAllText(SolutionPath(filePath))); SolutionLabel = Path.GetFileName(SolutionPath(filePath)); }
                catch (InvalidDataException) { }
            }
            // Rangé par rôle : les notes du formateur, et les copies déjà rendues, corrigées dès l'ouverture
            if (Project is { } project)
            {
                if (File.Exists(project.NotesPath)) TeacherNotes = File.ReadAllText(project.NotesPath);
                _submissionPaths.AddRange(project.SubmissionFiles());
                if (_submissionPaths.Count > 0) Regrade();
            }
        }

        /// <summary>Le dossier de l'exercice, s'il est rangé par rôle (l'exemplaire dans 1-formateur).</summary>
        public SmarxLabProject? Project => FilePath != null ? SmarxLabProject.Of(FilePath) : null;

        /// <summary>Les notes du formateur (une solution), écrites dans 1-formateur ; jamais dans l'exercice.</summary>
        public string? TeacherNotes { get; set; }

        public SmarxLab Lab { get; }

        [ObservableProperty] private string? _filePath;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SolutionText))]
        private SmarxDocument? _solution;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SolutionText))]
        private string _solutionLabel = "";

        /// <summary>La solution choisie, ou ce qu'il faut faire pour en avoir une.</summary>
        public string SolutionText => Solution == null
            ? Text.T("Solution : aucune pour l'instant.", "Solution: none yet.")
            : Text.T($"Solution : {(SolutionLabel.Length > 0 ? SolutionLabel : "en mémoire")}", $"Solution: {(SolutionLabel.Length > 0 ? SolutionLabel : "in memory")}");

        public static string SolutionPath(string labPath) => SmarxLabProject.Of(labPath)?.SolutionPath ?? SolutionBeside(labPath);

        private static string SolutionBeside(string labPath) =>
            labPath.EndsWith(SmarxLab.FileExtension, StringComparison.OrdinalIgnoreCase)
                ? labPath[..^SmarxLab.FileExtension.Length] + SolutionExtension
                : labPath + SolutionExtension;

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
            foreach (var o in Objectives) o.RefreshAll();
            foreach (var q in Questions) q.Refresh();
        }

        // ─── Champs généraux ─────────────────────────────────────────────────────

        public string LabId { get => Lab.Id; set { Lab.Id = value.Trim(); OnPropertyChanged(); } }

        public string LabTitle
        {
            get => Lab.Title.GetValueOrDefault(EditCulture) ?? "";
            set { Lab.Title[EditCulture] = value; OnPropertyChanged(); }
        }

        public string Statement
        {
            get => Lab.Statement.GetValueOrDefault(EditCulture) ?? "";
            set { Lab.Statement[EditCulture] = value; OnPropertyChanged(); }
        }

        public IReadOnlyList<Choice<LabLevel>> Levels =>
        [
            new(LabLevel.Beginner, Text.T("Débutant", "Beginner")),
            new(LabLevel.Intermediate, Text.T("Intermédiaire", "Intermediate")),
            new(LabLevel.Advanced, Text.T("Avancé", "Advanced"))
        ];

        public Choice<LabLevel> Level
        {
            get => Levels.First(l => l.Value == Lab.Level);
            set { if (value != null) { Lab.Level = value.Value; OnPropertyChanged(); } }
        }

        public int Duration { get => Lab.DurationMinutes; set { Lab.DurationMinutes = Math.Max(1, value); OnPropertyChanged(); } }

        public bool IsExam { get => Lab.Exam; set { Lab.Exam = value; if (!value) Lab.ExamCheck = false; OnPropertyChanged(); OnPropertyChanged(nameof(ExamCheck)); } }
        public bool ExamCheck { get => Lab.ExamCheck; set { Lab.ExamCheck = value && Lab.Exam; OnPropertyChanged(); } }

        public int TotalPoints => Lab.Objectives.Where(o => !o.Guard).Sum(o => o.Points) + Lab.Questions.Sum(q => q.Points);

        /// <summary>Les serveurs de départ : comptes, objets, partages.</summary>
        public string StartSummary => string.Join(" ; ",
            Lab.Start.Linux.Select(h => Text.T($"Linux {h.Name} : {h.Users.Count} comptes, {h.Shares.Count} partages Samba", $"Linux {h.Name}: {h.Users.Count} accounts, {h.Shares.Count} Samba shares"))
                .Concat(Lab.Start.Windows.Select(h => Text.T($"Windows {h.Name} : {h.Users.Count} comptes, {h.Shares.Count} partages", $"Windows {h.Name}: {h.Users.Count} accounts, {h.Shares.Count} shares"))));

        // ─── Ce que proposent les listes : tiré des serveurs de départ ───────────

        public IReadOnlyList<Choice<SmarxCheckType>> CheckTypes { get; } = SmarxObjectiveEditItem.TypeChoices();
        public IReadOnlyList<Choice<SmarxSystem>> Systems { get; } = SmarxObjectiveEditItem.SystemChoices();
        public IReadOnlyList<Choice<SmarxHygieneRule>> Rules { get; } = SmarxObjectiveEditItem.RuleChoices();

        public IReadOnlyList<string> AccountsOf(SmarxSystem system) => system == SmarxSystem.Linux
            ? ["*", .. Lab.Start.Linux.SelectMany(h => h.Users).Select(u => u.Name)]
            : ["*", .. Lab.Start.Windows.SelectMany(h => h.Users).Select(u => u.Name)];

        public IReadOnlyList<string> SharesOf(SmarxSystem system) => system == SmarxSystem.Linux
            ? Lab.Start.Linux.SelectMany(h => h.Shares).Select(s => s.Name).ToList()
            : Lab.Start.Windows.SelectMany(h => h.Shares).Select(s => s.Name).ToList();

        public IReadOnlyList<string> OperationsOf(SmarxSystem system) => system == SmarxSystem.Linux
            ? Enum.GetNames<SMARX.Services.Linux.LinuxOperation>()
            : Enum.GetNames<SMARX.Services.Windows.WindowsOperation>();

        public IReadOnlyList<string> PathsOf(SmarxSystem system)
        {
            var paths = new List<string>();
            if (system == SmarxSystem.Linux)
                foreach (var host in Lab.Start.Linux) Walk(host.Root, "/", paths, (n, p) => n.Children.Select(c => (c, LinuxHost.Combine(p, c.Name))));
            else
                foreach (var host in Lab.Start.Windows) Walk(host.Root, host.Root.Name, paths, (n, p) => n.Children.Select(c => (c, WindowsHost.Combine(p, c.Name))));
            return paths;
        }

        private static void Walk<T>(T node, string path, List<string> paths, Func<T, string, IEnumerable<(T, string)>> children)
        {
            paths.Add(path);
            foreach (var (child, childPath) in children(node, path)) Walk(child, childPath, paths, children);
        }

        // ─── Objectifs ───────────────────────────────────────────────────────────

        public ObservableCollection<SmarxObjectiveEditItem> Objectives { get; } = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RemoveObjectiveCommand), nameof(MoveObjectiveUpCommand), nameof(MoveObjectiveDownCommand))]
        private SmarxObjectiveEditItem? _selectedObjective;

        private bool HasSelectedObjective => SelectedObjective != null;

        [RelayCommand]
        private void AddObjective()
        {
            var system = Lab.Start.Linux.Count > 0 ? SmarxSystem.Linux : SmarxSystem.Windows;
            var objective = new SmarxObjective
            {
                Points = 1, Title = new LocalizedText { [EditCulture] = "" }, Hint = new LocalizedText { [EditCulture] = "" },
                Check = new SmarxCheck { Type = SmarxCheckType.AccessAllowed, System = system, Operation = system == SmarxSystem.Linux ? "Read" : "Read" }
            };
            Lab.Objectives.Add(objective);
            var item = Watch(new SmarxObjectiveEditItem(objective, this));
            Objectives.Add(item);
            Renumber();
            SelectedObjective = item;
        }

        [RelayCommand(CanExecute = nameof(HasSelectedObjective))]
        private void RemoveObjective()
        {
            var index = Objectives.IndexOf(SelectedObjective!);
            Lab.Objectives.Remove(SelectedObjective!.Model);
            Objectives.Remove(SelectedObjective);
            Renumber();
            SelectedObjective = Objectives.Count == 0 ? null : Objectives[Math.Min(index, Objectives.Count - 1)];
        }

        [RelayCommand(CanExecute = nameof(HasSelectedObjective))]
        private void MoveObjectiveUp() => MoveObjective(-1);

        [RelayCommand(CanExecute = nameof(HasSelectedObjective))]
        private void MoveObjectiveDown() => MoveObjective(1);

        private void MoveObjective(int delta)
        {
            var item = SelectedObjective!;
            var index = Objectives.IndexOf(item);
            var target = index + delta;
            if (target < 0 || target >= Objectives.Count) return;
            Objectives.Move(index, target);
            Lab.Objectives.RemoveAt(index);
            Lab.Objectives.Insert(target, item.Model);
            Renumber();
            SelectedObjective = item;
        }

        /// <summary>Objectifs numérotés o1, o2… dans l'ordre.</summary>
        private void Renumber()
        {
            for (var i = 0; i < Objectives.Count; i++) Objectives[i].Model.Id = $"o{i + 1}";
            foreach (var o in Objectives) o.RefreshAll();
            OnPropertyChanged(nameof(TotalPoints));
        }

        // ─── Questions ───────────────────────────────────────────────────────────

        public ObservableCollection<QuestionItem> Questions { get; } = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RemoveQuestionCommand), nameof(MoveQuestionUpCommand), nameof(MoveQuestionDownCommand))]
        private QuestionItem? _selectedQuestion;

        private bool HasSelectedQuestion => SelectedQuestion != null;

        public IReadOnlyList<Choice<LabQuestionKind>> QuestionKinds => QuestionItem.KindChoices();

        [RelayCommand]
        private void AddQuestion()
        {
            var question = new LabQuestion
            {
                Kind = LabQuestionKind.SingleChoice, Points = 1,
                Title = new LocalizedText { [EditCulture] = Text.T("Nouvelle question", "New question") }, Hint = new LocalizedText { [EditCulture] = "" }
            };
            Lab.Questions.Add(question);
            var item = Watch(new QuestionItem(question, () => EditCulture));
            item.AddChoiceCommand.Execute(null);
            item.AddChoiceCommand.Execute(null);
            Questions.Add(item);
            RenumberQuestions();
            SelectedQuestion = item;
        }

        [RelayCommand(CanExecute = nameof(HasSelectedQuestion))]
        private void RemoveQuestion()
        {
            var index = Questions.IndexOf(SelectedQuestion!);
            Lab.Questions.Remove(SelectedQuestion!.Model);
            Questions.Remove(SelectedQuestion);
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
            Lab.Questions.RemoveAt(index);
            Lab.Questions.Insert(target, item.Model);
            RenumberQuestions();
            SelectedQuestion = item;
        }

        private void RenumberQuestions()
        {
            for (var i = 0; i < Questions.Count; i++) Questions[i].Model.Id = $"q{i + 1}";
            foreach (var q in Questions) q.Refresh();
            OnPropertyChanged(nameof(TotalPoints));
        }

        private T Watch<T>(T item) where T : INotifyPropertyChanged
        {
            item.PropertyChanged += (_, e) => { if (e.PropertyName is "Points" or "Guard" or "") OnPropertyChanged(nameof(TotalPoints)); };
            return item;
        }

        // ─── Validation ──────────────────────────────────────────────────────────

        public ObservableCollection<IssueItem> Issues { get; } = new();

        /// <summary>
        /// Vérifie l'exercice : identifiant, titres, au moins un objectif ; au départ, aucun objectif noté n'est atteint et
        /// toutes les gardes tiennent ; avec la solution, tous les objectifs sont atteints. Une question sans corrigé se
        /// note à la main.
        /// </summary>
        [RelayCommand]
        public void Validate()
        {
            Issues.Clear();
            foreach (var issue in Check()) Issues.Add(new IssueItem(issue));
            var errors = Issues.Count(i => i.Issue.Level == LabIssueLevel.Error);
            var warnings = Issues.Count(i => i.Issue.Level == LabIssueLevel.Warning);
            _status(string.Format(Text["Status.Validated"], errors, warnings));
        }

        /// <summary>Les vérifications de SmarxLabValidator, dans la langue de l'interface.</summary>
        public IReadOnlyList<LabIssue> Check() => SmarxLabValidator.Check(Lab, Solution, Text.Culture);

        [RelayCommand]
        private async Task ChooseSolutionAsync()
        {
            if (await _dialogs.OpenFileAsync(Text["Smarx.ChooseSolution"], Text["File.SmarxServers"], "*" + SmarxDocument.FileExtension) is not { } path) return;
            try
            {
                Solution = SmarxDocument.Parse(await File.ReadAllTextAsync(path));
                SolutionLabel = Path.GetFileName(path);
                Validate();
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException)
            {
                _status(string.Format(Text["Status.Error"], ex.Message));
            }
        }

        // ─── Fichiers ────────────────────────────────────────────────────────────

        /// <summary>Enregistre l'exercice, et la solution à côté (jamais dans l'exercice).</summary>
        public async Task SaveAsync(bool choosePath)
        {
            var path = FilePath;
            if (choosePath || path == null)
            {
                path = await _dialogs.SaveFileAsync(Text["Menu.SaveAs"], (Lab.Id.Length > 0 ? Lab.Id : "exercice") + SmarxLab.FileExtension, Text["File.SmarxLab"], "*" + SmarxLab.FileExtension);
                if (path == null) return;
            }
            Save(path);
        }

        public void Save(string path)
        {
            if (SmarxLabProject.Of(path) is { } project)
                project.Save(Lab, path, TeacherNotes, Solution);
            else
            {
                File.WriteAllText(path, SmarxLabService.Serialize(Lab));
                if (Solution != null) File.WriteAllText(SolutionPath(path), Solution.Serialize());
            }
            FilePath = path;
            _status(string.Format(Text["Status.Saved"], Path.GetFileName(path)));
        }

        /// <summary>Le sujet à distribuer : sans le corrigé des questions et, pour un examen, sans les indices.</summary>
        public async Task ExportSubjectAsync()
        {
            if (Project is { } project && FilePath != null)
            {
                File.WriteAllText(project.SubjectPath(FilePath), SmarxLabService.SerializeSubject(Lab));
                _status(string.Format(Text["Status.SubjectSaved"], project.SubjectPath(FilePath)));
                return;
            }
            var name = Lab.Id + (EditCulture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? ".subject" : ".sujet") + SmarxLab.FileExtension;
            if (await _dialogs.SaveFileAsync(Text["Menu.ExportSubject"], name, Text["File.SmarxLab"], "*" + SmarxLab.FileExtension) is not { } path) return;
            File.WriteAllText(path, SmarxLabService.SerializeSubject(Lab));
            _status(string.Format(Text["Status.SubjectSaved"], Path.GetFileName(path)));
        }

        // ─── Copies : corriger une classe ────────────────────────────────────────

        public ObservableCollection<SmarxSubmissionRow> Submissions { get; } = new();

        [ObservableProperty] private string _classSummary = "";

        public bool HasSubmissions => Submissions.Count > 0;

        [RelayCommand]
        private async Task AddSubmissionsAsync()
        {
            var files = await _dialogs.OpenFilesAsync(Text["Submissions.Add"], Text["File.SmarxSubmission"], "*" + SmarxSubmission.FileExtension);
            AddSubmissions(files);
        }

        public void AddSubmissions(IEnumerable<string> paths)
        {
            foreach (var path in paths.Where(p => !_submissionPaths.Contains(p, StringComparer.OrdinalIgnoreCase)))
                _submissionPaths.Add(path);
            Regrade();
        }

        /// <summary>Corrige chaque copie avec l'exercice tel qu'il est ouvert ici (le corrigé des questions compris).</summary>
        [RelayCommand]
        public void Regrade()
        {
            var culture = EditCulture;
            _graded = SmarxClassGrader.GradeAll(Lab, _submissionPaths, culture);
            Submissions.Clear();
            foreach (var graded in _graded) Submissions.Add(new SmarxSubmissionRow(graded, Lab.MaxPoints, culture));
            var count = _graded.Count(g => g.Graded);
            ClassSummary = SmarxClassGrader.Average(_graded) is { } average
                ? Text.T($"{count} copie(s) corrigée(s) — moyenne {average:0.#} / {Lab.MaxPoints}", $"{count} copy(ies) graded — average {average:0.#} / {Lab.MaxPoints}")
                : Text["Submissions.None"];
            OnPropertyChanged(nameof(HasSubmissions));
            _status(ClassSummary);
        }

        private IReadOnlyList<SmarxGradedSubmission> _graded = [];

        /// <summary>Barème des notes exportées : sur 20 par défaut.</summary>
        [ObservableProperty] private int _scale = SmarxClassGrader.DefaultScale;

        partial void OnScaleChanged(int value)
        {
            if (value < 1) Scale = 1;
        }

        [RelayCommand]
        private void ClearSubmissions()
        {
            _submissionPaths.Clear();
            _graded = [];
            Submissions.Clear();
            ClassSummary = Text["Submissions.None"];
            OnPropertyChanged(nameof(HasSubmissions));
        }

        [RelayCommand]
        private async Task ExportGradesAsync()
        {
            if ((Project?.GradesPath ?? await _dialogs.SaveFileAsync(Text["Submissions.ExportCsv"], Lab.Id + ".notes.csv", Text["File.Csv"], "*.csv")) is not { } path) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, GradesCsv(), new UTF8Encoding(true));
            _status(string.Format(Text["Status.Saved"], Path.GetFileName(path)));
        }

        [RelayCommand]
        private async Task ExportMoodleAsync()
        {
            if ((Project?.MoodlePath ?? await _dialogs.SaveFileAsync(Text["Submissions.ExportMoodle"], Lab.Id + ".moodle.csv", Text["File.Csv"], "*.csv")) is not { } path) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, SmarxClassGrader.MoodleCsv(Lab, _graded, EditCulture, Scale), new UTF8Encoding(false));
            _status(string.Format(Text["Status.Saved"], Path.GetFileName(path)));
        }

        [RelayCommand]
        private async Task ExportClassReportAsync()
        {
            if ((Project?.ClassReportPath ?? await _dialogs.SaveFileAsync(Text["Submissions.ExportReport"], Lab.Id + ".bilan.md", Text["File.Markdown"], "*.md")) is not { } path) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, SmarxClassGrader.ClassReport(Lab, _graded, EditCulture));
            _status(string.Format(Text["Status.Saved"], Path.GetFileName(path)));
        }

        /// <summary>Les notes de la classe, une copie par ligne (Excel les ouvre directement), sur le barème choisi.</summary>
        public string GradesCsv() => SmarxClassGrader.Csv(Lab, _graded, EditCulture, Scale);
    }
}
