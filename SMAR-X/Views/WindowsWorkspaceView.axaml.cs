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

        /// <summary>Partage avancé : les partages du dossier, leurs autorisations et l'énumération basée sur l'accès.</summary>
        private async void OnAdvancedSharing(object? sender, RoutedEventArgs e)
        {
            if (Vm is not { SelectedNode: { IsDirectory: true } node } vm || Owner is not { } owner) return;
            await new ShareWindow { DataContext = new ShareSettingsViewModel(vm.Host, node) }.ShowDialog(owner);
            vm.AfterSecurityWindow();
        }

        /// <summary>whoami du compte testé, dans la session du testeur : sur le serveur ou par le partage choisi, élevée ou non.</summary>
        private void OnWhoamiTester(object? sender, RoutedEventArgs e)
        {
            if (Vm is not { SelectedUser: { } user } vm) return;
            ShowWhoami(new WhoamiViewModel(vm, user.Name, vm.SelectedRoute?.Share != null, vm.Elevated));
        }

        /// <summary>whoami du compte choisi dans la carte Comptes, dans une session sur le serveur.</summary>
        private void OnWhoamiAccount(object? sender, RoutedEventArgs e)
        {
            if (Vm is not { SelectedAccount: { } account } vm) return;
            ShowWhoami(new WhoamiViewModel(vm, account.Name, false, false));
        }

        /// <summary>La fenêtre reste ouverte à côté : on change les groupes et l'on voit le jeton changer.</summary>
        private void ShowWhoami(WhoamiViewModel whoami)
        {
            var window = new WhoamiWindow { DataContext = whoami };
            if (Owner is { } owner) window.Show(owner);
            else window.Show();
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
