using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SMARX.Views
{
    /// <summary>Espace Linux : arborescence, testeur, matrice, droits et comptes.</summary>
    public partial class LinuxWorkspaceView : UserControl
    {
        public LinuxWorkspaceView()
        {
            InitializeComponent();
        }

        /// <summary>L'aide-mémoire, sur l'onglet de cet espace.</summary>
        private void OnGlossary(object? sender, RoutedEventArgs e) => GlossaryWindow.Show(TopLevel.GetTopLevel(this) as Window, 1);
    }
}
