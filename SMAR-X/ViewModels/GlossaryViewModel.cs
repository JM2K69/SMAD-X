using System;
using System.Collections.Generic;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SMARX.Services;

namespace SMARX.ViewModels
{
    /// <summary>
    /// Aide-mémoire : un onglet Windows (icacls, PowerShell, NTFSSecurity), un onglet Linux (ls -l, getfacl, umask,
    /// commandes) ; les deux s'exportent dans un PDF.
    /// </summary>
    public sealed partial class GlossaryViewModel : ObservableObject
    {
        public GlossaryViewModel(int tab = 0)
        {
            _tab = tab;
            Build();
            SmarxText.Instance.PropertyChanged += OnLanguageChanged;
        }

        [ObservableProperty] private IReadOnlyList<GlossarySection> _windows = [];
        [ObservableProperty] private IReadOnlyList<GlossarySection> _linux = [];

        /// <summary>0 : Windows, 1 : Linux.</summary>
        [ObservableProperty] private int _tab;

        /// <summary>Ce qu'a donné le dernier export, ou ce que fait le bouton.</summary>
        [ObservableProperty] private string _message = "";

        /// <summary>La version de SMAR-X, écrite dans le PDF.</summary>
        public static string Version => typeof(GlossaryViewModel).Assembly.GetName().Version?.ToString(3) ?? "?";

        public string PdfFileName => Glossary.PdfFileName(SmarxText.Instance.Culture);

        private void Build()
        {
            Windows = Glossary.Windows(SmarxText.Instance.Culture);
            Linux = Glossary.Linux(SmarxText.Instance.Culture);
            Message = SmarxText.Instance.T("Chaque commande se copie avec le bouton à sa droite ; le PDF reprend les deux onglets.",
                                           "Each command is copied with the button on its right; the PDF holds both tabs.");
        }

        /// <summary>Écrit l'aide-mémoire, dans la langue de l'interface, en PDF ; vrai s'il est écrit.</summary>
        public bool ExportPdf(string path)
        {
            var text = SmarxText.Instance;
            try
            {
                Glossary.SavePdf(path, text.Culture, Version, DateTime.Now);
                Message = text.T($"PDF enregistré : {path}", $"PDF saved: {path}");
                return true;
            }
            catch (Exception ex)
            {
                Message = text.T($"Le PDF n'a pas pu être écrit : {ex.Message}", $"The PDF could not be written: {ex.Message}");
                return false;
            }
        }

        private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SmarxText.CurrentCulture)) Build();
        }

        public void Detach() => SmarxText.Instance.PropertyChanged -= OnLanguageChanged;
    }
}
