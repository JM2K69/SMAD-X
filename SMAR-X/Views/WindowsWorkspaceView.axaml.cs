using Avalonia.Controls;
using Avalonia.Interactivity;
using SMARX.ViewModels;

namespace SMARX.Views
{
    /// <summary>Espace Windows : arborescence NTFS, testeur, matrice, autorisations, accès effectif et comptes locaux.</summary>
    public partial class WindowsWorkspaceView : UserControl
    {
        public WindowsWorkspaceView()
        {
            InitializeComponent();
        }

        private WindowsWorkspaceViewModel? Vm => DataContext as WindowsWorkspaceViewModel;
        private Window? Owner => TopLevel.GetTopLevel(this) as Window;

        /// <summary>L'aide-mémoire, sur l'onglet de cet espace.</summary>
        private void OnGlossary(object? sender, RoutedEventArgs e) => GlossaryWindow.Show(Owner, 0);

        /// <summary>Bouton Modifier de l'onglet Sécurité : la fenêtre « Autorisations pour … ».</summary>
        private async void OnEditPermissions(object? sender, RoutedEventArgs e)
        {
            if (Vm is not { SelectedNode: { } node } vm || Owner is not { } owner) return;
            await new PermissionsWindow { DataContext = new PermissionsEditViewModel(vm.Host, node) }.ShowDialog(owner);
            vm.AfterSecurityWindow();
        }

        /// <summary>Bouton Avancé : « Paramètres de sécurité avancés », l'accès effectif ouvert sur le compte testé.</summary>
        private async void OnAdvancedSecurity(object? sender, RoutedEventArgs e)
        {
            if (Vm is not { SelectedNode: { } node } vm || Owner is not { } owner) return;
            await new AdvancedSecurityWindow { DataContext = new AdvancedSecurityViewModel(vm.Host, node, vm.SelectedUser?.Name) }.ShowDialog(owner);
            vm.AfterSecurityWindow();
        }
    }
}
