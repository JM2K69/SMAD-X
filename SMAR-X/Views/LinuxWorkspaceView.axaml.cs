using Avalonia.Controls;
using Avalonia.Interactivity;
using SMARX.ViewModels;

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

        /// <summary>Partage Samba du dossier choisi : une section de smb.conf.</summary>
        private async void OnSambaShare(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not LinuxWorkspaceViewModel { SelectedNode: { IsDirectory: true } node } vm || TopLevel.GetTopLevel(this) is not Window owner) return;
            await new SambaShareWindow { DataContext = new SambaShareSettingsViewModel(vm.Host, node) }.ShowDialog(owner);
            vm.AfterShareWindow();
        }
    }
}
