using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SMARX.Views
{
    /// <summary>Espace Windows : arborescence NTFS, testeur, matrice, autorisations, accès effectif et comptes locaux.</summary>
    public partial class WindowsWorkspaceView : UserControl
    {
        public WindowsWorkspaceView()
        {
            InitializeComponent();
        }

        /// <summary>L'aide-mémoire, sur l'onglet de cet espace.</summary>
        private void OnGlossary(object? sender, RoutedEventArgs e) => GlossaryWindow.Show(TopLevel.GetTopLevel(this) as Window, 0);
    }
}
