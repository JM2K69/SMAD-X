using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SMARX.ViewModels;

namespace SMARX.Views
{
    /// <summary>« Partage Samba » : OK garde les changements ; Annuler, ou la croix, remet les partages comme avant.</summary>
    public partial class SambaShareWindow : Window
    {
        private bool _confirmed;

        public SambaShareWindow()
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
            if (!_confirmed) (DataContext as SambaShareSettingsViewModel)?.Cancel();
            base.OnClosed(e);
        }
    }
}
