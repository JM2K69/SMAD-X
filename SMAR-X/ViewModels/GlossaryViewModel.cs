using System.Collections.Generic;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SMARX.Services;

namespace SMARX.ViewModels
{
    /// <summary>Aide-mémoire des abréviations : un onglet Windows (icacls), un onglet Linux (ls -l, getfacl, umask).</summary>
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

        private void Build()
        {
            Windows = Glossary.Windows(SmarxText.Instance.Culture);
            Linux = Glossary.Linux(SmarxText.Instance.Culture);
        }

        private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SmarxText.CurrentCulture)) Build();
        }

        public void Detach() => SmarxText.Instance.PropertyChanged -= OnLanguageChanged;
    }
}
