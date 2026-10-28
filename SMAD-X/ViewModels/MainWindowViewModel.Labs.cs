using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Labs;

namespace SMADX.ViewModels
{
    /// <summary>Exercice proposé dans le menu « Exercices ».</summary>
    public sealed record LabMenuItem(LabDefinition Lab, string Title);

    // Exercices : ouverture (fournis, fichier ou copie), mode exercice et mode examen
    public partial class MainWindowViewModel
    {
        private readonly LabService _labService = new();
        private LabDefinition[]? _allBuiltInLabs;

        private static FileFilter LabFilter => new(Loc["FileType.Lab"], "*" + LabService.FileExtension);

        /// <summary>Exercices généraux fournis avec l'application.</summary>
        public ObservableCollection<LabMenuItem> BuiltInLabs => LabMenu(category: null);

        /// <summary>Exercices de la série « Chemins d'attaque ».</summary>
        public ObservableCollection<LabMenuItem> AttackPathLabs => LabMenu(AttackPathCategory);

        public const string AttackPathCategory = "attack-paths";

        /// <summary>
        /// Exercices d'une série, dans la langue de l'interface : un exercice écrit dans une seule langue
        /// (son domaine compris) n'est proposé que dans celle-ci.
        /// </summary>
        private ObservableCollection<LabMenuItem> LabMenu(string? category)
        {
            var culture = Loc.CurrentCulture.Name;
            var items = new ObservableCollection<LabMenuItem>();
            foreach (var lab in LoadBuiltInLabs().Where(l => l.Category == category && (l.Language is null || string.Equals(l.Language, culture, StringComparison.OrdinalIgnoreCase))))
                items.Add(new LabMenuItem(lab, $"{Loc[$"Lab.Level.{lab.Level}"]} — {lab.Title.Resolve(culture)}"));
            return items;
        }

        /// <summary>Lus une fois : les exercices sont embarqués dans l'exe.</summary>
        private LabDefinition[] LoadBuiltInLabs()
        {
            if (_allBuiltInLabs != null) return _allBuiltInLabs;
            try
            {
                _allBuiltInLabs = _labService.BuiltInLabs().ToArray();
            }
            catch (LabFormatException ex)
            {
                ErrorReporter.Log("Exercices", ex);
                _allBuiltInLabs = Array.Empty<LabDefinition>();
            }
            return _allBuiltInLabs;
        }

        /// <summary>Titres et liste des exercices dans la nouvelle langue.</summary>
        private void RefreshLabMenus()
        {
            OnPropertyChanged(nameof(BuiltInLabs));
            OnPropertyChanged(nameof(AttackPathLabs));
            OnPropertyChanged(nameof(WindowTitle));
        }

        // ─── Mode exercice ───────────────────────────────────────────────────────

        /// <summary>Exercice en cours (fenêtre « Exercice »), null s'il n'y en a pas.</summary>
        public LabViewModel? CurrentLab { get; private set; }

        /// <summary>
        /// Mode exercice : SMAD-X a été ouvert sur un exercice ou une copie, ou l'exercice est un examen.
        /// Les commandes qui remplacent le domaine (nouveau, ouvrir, autre exercice) sont désactivées :
        /// la copie de l'apprenant reste le domaine qu'il a travaillé.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFreeMode), nameof(WindowTitle), nameof(CanLeaveLabMode))]
        [NotifyCanExecuteChangedFor(nameof(CreateNewDomainCommand), nameof(InitializeSampleDataCommand), nameof(LoadFromFileCommand),
                                    nameof(OpenBuiltInLabCommand), nameof(OpenLabFileCommand),
                                    nameof(LeaveLabModeCommand))]
        private bool _isLabMode;

        public bool IsFreeMode => !IsLabMode;

        public bool HasCurrentLab => CurrentLab != null;

        /// <summary>Un examen ne se quitte pas : l'apprenant le termine en enregistrant sa copie.</summary>
        public bool CanLeaveLabMode => IsLabMode && CurrentLab is { IsExam: false };

        public string WindowTitle => IsLabMode && CurrentLab != null
            ? string.Format(Loc[CurrentLab.IsExam ? "Lab.Mode.ExamTitle" : "Lab.Mode.Title"], CurrentLab.Title)
            : "SMAD-X";

        /// <summary>
        /// Paramètre object : le menu évalue CanExecute avec le DataContext de l'entrée, qui n'est pas
        /// toujours un LabMenuItem (un RelayCommand&lt;LabMenuItem&gt; lèverait alors une exception).
        /// </summary>
        [RelayCommand(CanExecute = nameof(IsFreeMode))]
        private async Task OpenBuiltInLab(object? parameter)
        {
            if (parameter is LabMenuItem item)
                await StartLabAsync(item.Lab, labMode: false);
        }

        [RelayCommand(CanExecute = nameof(IsFreeMode))]
        private Task OpenLabFile() => RunFileCommandAsync(async () =>
        {
            var path = await _dialogs.PickOpenFileAsync(Loc["Lab.Open.Title"], LabFilter,
                new FileFilter(Loc["FileType.Submission"], "*" + LabSubmissionService.FileExtension), AllFilesFilter);
            if (path == null) return;
            await OpenLabOrSubmissionAsync(path, labMode: false);
        });

        /// <summary>
        /// Fichier donné au lancement (double-clic ou <c>SMAD-X.exe fichier</c>) : un exercice ou une copie
        /// ouvre le mode exercice, un domaine s'ouvre comme avec « Ouvrir ».
        /// </summary>
        public async Task OpenStartupFileAsync(string path)
        {
            try
            {
                if (path.EndsWith(LabService.FileExtension, StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(LabSubmissionService.FileExtension, StringComparison.OrdinalIgnoreCase))
                    await OpenLabOrSubmissionAsync(path, labMode: true);
                else
                    await LoadDomainFileAsync(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                StatusMessage = string.Format(Loc["Lab.Startup.Error"], path, ex.Message);
            }
        }

        private async Task OpenLabOrSubmissionAsync(string path, bool labMode)
        {
            try
            {
                if (path.EndsWith(LabSubmissionService.FileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    var submission = new LabSubmissionService().LoadFile(path);
                    await StartLabAsync(submission.Lab, labMode, submission, path);
                    StatusMessage = string.Format(Loc["Lab.Resumed"], CurrentLab!.StudentName, CurrentLab.Title);
                }
                else
                {
                    var lab = _labService.LoadFile(path);
                    // L'exemplaire du formateur (indices, corrigé des questions) ne se passe pas : on distribue le sujet
                    if (lab.IsTrainerCopy)
                    {
                        StatusMessage = Loc["Lab.TrainerCopy.Status"];
                        await _dialogs.ShowMessageAsync(Loc["Lab.TrainerCopy.Title"], Loc["Lab.TrainerCopy.Message"]);
                        return;
                    }
                    await StartLabAsync(lab, labMode);
                }
            }
            catch (LabFormatException ex)
            {
                StatusMessage = ex.Message;
            }
        }

        /// <summary>
        /// Ouvre l'exercice, après avoir proposé de reprendre sa copie de secours s'il y en a une : SMAD-X
        /// s'est fermé sans que l'apprenant enregistre. Refusée, elle est supprimée.
        /// </summary>
        private async Task StartLabAsync(LabDefinition lab, bool labMode, LabSubmission? resumed = null, string? submissionPath = null)
        {
            if (FindRecovery(lab, resumed) is { } recovery)
            {
                var name = string.IsNullOrWhiteSpace(recovery.Submission.Student) ? "—" : recovery.Submission.Student;
                if (await _dialogs.ConfirmAsync(Loc["Lab.Recovery.Title"],
                        string.Format(Loc["Lab.Recovery.Message"], recovery.Submission.SavedAt.ToString("g", Loc.CurrentCulture), name),
                        Loc["Lab.Recovery.Resume"], Loc["Lab.Recovery.Discard"]))
                {
                    StartLab(recovery.Submission.Lab, labMode, recovery.Submission, submissionPath);
                    _savedState = string.Empty; // le travail repris n'est pas encore dans la copie de l'apprenant
                    return;
                }
                try { System.IO.File.Delete(recovery.Path); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ErrorReporter.Log("Copie de secours", ex); }
            }
            StartLab(lab, labMode, resumed, submissionPath);
        }

        /// <summary>Charge le domaine de départ (ou celui de la copie reprise) et ouvre la fenêtre de l'exercice.</summary>
        private void StartLab(LabDefinition lab, bool labMode, LabSubmission? resumed = null, string? submissionPath = null)
        {
            var session = resumed != null ? LabSession.Resume(resumed) : LabSession.Begin(lab, DateTime.Now);
            ShowLabDomain(session, resumed?.Work ?? session.NewWorkingCopy());
            CurrentLab = new LabViewModel(session, CurrentDocument, () => ShowLabDomain(session, session.NewWorkingCopy()), _dialogs, resumed, submissionPath);
            MarkSaved(); // les réponses reprises font partie de l'état enregistré
            // Copie enregistrée : plus rien à perdre, la copie de secours ne sert plus
            var current = CurrentLab;
            current.Saved += () =>
            {
                MarkSaved();
                DeleteRecovery(current);
            };
            _recoveredState = null;
            OnPropertyChanged(nameof(CurrentLab));
            OnPropertyChanged(nameof(HasCurrentLab));
            ShowCurrentLabCommand.NotifyCanExecuteChanged();
            SaveSubmissionCommand.NotifyCanExecuteChanged();
            // Un examen est toujours en mode exercice, quelle que soit la façon de l'ouvrir
            IsLabMode = labMode || lab.Exam;
            RefreshLabTools();
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(CanLeaveLabMode));
            LeaveLabModeCommand.NotifyCanExecuteChanged();
            _dialogs.ShowLab(CurrentLab);
        }

        private void ShowLabDomain(LabSession session, ADRootDocument working)
        {
            _dataService.ApplyTierConfiguration(working);
            ShowDomain(working, expandDefaultNodes: true);
            StatusMessage = string.Format(Loc["Lab.Started"], session.Lab.Title.Resolve(Loc.CurrentCulture.Name));
        }

        /// <summary>Document complet en cours (domaine, sites, tiers), tel qu'il est enregistré.</summary>
        private ADRootDocument? CurrentDocument()
        {
            if (RootObject == null) return null;
            var doc = _currentDocument ?? new ADRootDocument();
            doc.Version = ADRootDocument.CurrentVersion;
            doc.Domain = RootObject;
            doc.SitesTopology = SitesTopology;
            return doc;
        }

        [RelayCommand(CanExecute = nameof(HasCurrentLab))]
        private void ShowCurrentLab()
        {
            if (CurrentLab != null) _dialogs.ShowLab(CurrentLab);
        }

        [RelayCommand(CanExecute = nameof(HasCurrentLab))]
        private Task SaveSubmission() => CurrentLab?.SaveSubmissionCommand.ExecuteAsync(null) ?? Task.CompletedTask;

        [RelayCommand(CanExecute = nameof(CanLeaveLabMode))]
        private void LeaveLabMode()
        {
            IsLabMode = false;
            RefreshLabTools();
            StatusMessage = Loc["Lab.Mode.Left"];
        }
    }
}
