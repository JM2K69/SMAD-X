using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SMARX.ViewModels;

namespace SMARX.Views
{
    /// <summary>
    /// « Comptes et groupes » : elle reste ouverte à côté de l'espace Windows ; Ajouter… ouvre « Sélectionner des
    /// utilisateurs ou des groupes », comme la console Active Directory.
    /// </summary>
    public partial class DirectoryWindow : Window
    {
        public DirectoryWindow()
        {
            InitializeComponent();
        }

        private DirectoryViewModel? Vm => DataContext as DirectoryViewModel;

        /// <summary>« Membre de » → Ajouter… : l'objet choisi rejoint les groupes cochés.</summary>
        private async void OnAddMemberOf(object? sender, RoutedEventArgs e)
        {
            if (Vm is not { HasObject: true } vm) return;
            var picker = vm.MemberOfPicker();
            if (await new DirectoryPickerWindow { DataContext = picker }.ShowDialog<bool>(this)) vm.AddMemberOf(picker.Chosen);
        }

        /// <summary>« Membres » → Ajouter… : les comptes et les groupes cochés entrent dans le groupe choisi.</summary>
        private async void OnAddMembers(object? sender, RoutedEventArgs e)
        {
            if (Vm is not { CanEditMembers: true } vm) return;
            var picker = vm.MembersPicker();
            if (await new DirectoryPickerWindow { DataContext = picker }.ShowDialog<bool>(this)) vm.AddMembers(picker.Chosen);
        }

        private void OnClose(object? sender, RoutedEventArgs e) => Close();

        protected override void OnClosed(EventArgs e)
        {
            Vm?.Dispose();
            base.OnClosed(e);
        }
    }
}
