using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMARX.Models;
using SMARX.Samples;

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
            OnPropertyChanged(nameof(Legend));
        }
    }
}
