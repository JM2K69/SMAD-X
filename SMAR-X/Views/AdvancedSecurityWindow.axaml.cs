using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SMARX.ViewModels;

namespace SMARX.Views
{
    /// <summary>« Paramètres de sécurité avancés » : OK garde les changements ; Annuler, ou la croix, remet l'objet comme avant.</summary>
    public partial class AdvancedSecurityWindow : Window
    {
        private bool _confirmed;

        public AdvancedSecurityWindow()
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
            if (!_confirmed) (DataContext as AdvancedSecurityViewModel)?.Cancel();
            base.OnClosed(e);
        }
    }
}
