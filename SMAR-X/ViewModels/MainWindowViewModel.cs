using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMARX.Labs;
using SMARX.Models;
using SMARX.Samples;
using SMARX.Services.Labs;

namespace SMARX.ViewModels
{
    /// <summary>
    /// Fenêtre principale : un espace Linux et un espace Windows, choisis dans l'en-tête, la langue, l'ouverture et
    /// l'enregistrement des fichiers .smar-x.json.
    /// </summary>
    public sealed partial class MainWindowViewModel : ObservableObject
    {
        private static SmarxText Text => SmarxText.Instance;

        public MainWindowViewModel()
        {
            Linux = new LinuxWorkspaceViewModel(LinuxSamples.FileServer());
            Windows = new WindowsWorkspaceViewModel(WindowsSamples.FileServer());
            _current = Linux;
            Text.PropertyChanged += OnLanguageChanged;
        }

        public LinuxWorkspaceViewModel Linux { get; }
        public WindowsWorkspaceViewModel Windows { get; }

        /// <summary>L'espace affiché : <see cref="Linux"/> ou <see cref="Windows"/>.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsLinux), nameof(IsWindows), nameof(Legend))]
        private ObservableObject _current;

        public bool IsLinux
        {
            get => Current == Linux;
            set { if (value) Current = Linux; }
        }

        public bool IsWindows
        {
            get => Current == Windows;
            set { if (value) Current = Windows; }
        }

        public string Legend => IsLinux ? Text["Legend"] : Text["Legend.Windows"];

        [RelayCommand]
        private void ShowLinux() => Current = Linux;

        [RelayCommand]
        private void ShowWindows() => Current = Windows;

        [RelayCommand]
        private void LoadSample()
        {
            Linux.Load(LinuxSamples.FileServer());
            Windows.Load(WindowsSamples.FileServer());
        }

        [RelayCommand]
        private void ToggleLanguage() => Text.SetLanguage(Text.Culture == "fr-FR" ? "en-US" : "fr-FR");

        // ─── Exercices ───────────────────────────────────────────────────────────

        public IReadOnlyList<SmarxLab> BuiltInLabs { get; } = SmarxBuiltInLabs.All();

        /// <summary>L'exercice en cours ; null en mode libre.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasLab), nameof(IsFreeMode))]
        private SmarxLabViewModel? _lab;

        public bool HasLab => Lab != null;
        /// <summary>Hors exercice : charger l'exemple ou ouvrir un fichier remplacerait les serveurs de l'apprenant.</summary>
        public bool IsFreeMode => Lab == null;

        /// <summary>Ouvre un exercice : ses serveurs de départ remplacent ceux des espaces concernés.</summary>
        public void OpenLab(SmarxLab lab)
        {
            Lab = new SmarxLabViewModel(lab, () => LabWork(lab), () => LoadStart(lab));
            LoadStart(lab);
        }

        private void LoadStart(SmarxLab lab)
        {
            var work = SmarxLabGrader.Clone(lab.Start);
            if (work.Linux.Count > 0) Linux.Load(work.Linux[0]);
            if (work.Windows.Count > 0) Windows.Load(work.Windows[0]);
            Current = work.Linux.Count == 0 && work.Windows.Count > 0 ? Windows : Linux;
            // L'arbre s'ouvre sur l'objet du premier objectif
            if (lab.Objectives.Select(o => o.Check.Path).FirstOrDefault(p => p != null) is { } path)
            {
                if (Current == Linux) Linux.Select(path);
                else Windows.Select(path);
            }
        }

        /// <summary>Les serveurs de l'apprenant : ceux des espaces que l'exercice utilise.</summary>
        private SmarxDocument LabWork(SmarxLab lab) => new()
        {
            Linux = lab.Start.Linux.Count > 0 ? [Linux.Host] : [],
            Windows = lab.Start.Windows.Count > 0 ? [Windows.Host] : []
        };

        /// <summary>Quitte l'exercice : retour aux serveurs d'exemple.</summary>
        [RelayCommand]
        private void LeaveLab()
        {
            Lab = null;
            LoadSample();
        }

        /// <summary>Ouvre un fichier : son premier serveur Linux et son premier serveur Windows ; l'exemple pour ce qui manque.</summary>
        public void Load(SmarxDocument document)
        {
            Linux.Load(document.Linux.FirstOrDefault() ?? LinuxSamples.FileServer());
            Windows.Load(document.Windows.FirstOrDefault() ?? WindowsSamples.FileServer());
            Current = document.Linux.Count == 0 && document.Windows.Count > 0 ? Windows : Linux;
        }

        public SmarxDocument ToDocument() => new() { Linux = [Linux.Host], Windows = [Windows.Host] };

        public string SuggestedFileName => (IsLinux ? Linux.Host.Name : Windows.Host.Name) + SmarxDocument.FileExtension;

        private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(SmarxText.CurrentCulture)) return;
            Linux.Relabel();
            Windows.Relabel();
            Lab?.Relabel();
            OnPropertyChanged(nameof(Legend));
        }
    }
}
