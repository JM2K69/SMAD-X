using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SMARX.ViewModels;

namespace SMARX.Views
{
    /// <summary>« Autorisations pour … » : OK garde les changements ; Annuler, ou la croix, remet l'objet comme avant.</summary>
    public partial class PermissionsWindow : Window
    {
        private bool _confirmed;

        public PermissionsWindow()
        {
            InitializeComponent();
        }

        private void OnOk(object? sender, RoutedEventArgs e)
        {
            _confirmed = true;
            Close();
        }

        private void OnCancel(object? sender, RoutedEventArgs e) => Close();

        protected override void OnClosed(EventArgs e)
        {
            if (!_confirmed) (DataContext as PermissionsEditViewModel)?.Cancel();
            base.OnClosed(e);
        }
    }
}
