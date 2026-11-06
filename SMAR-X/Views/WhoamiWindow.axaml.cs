using System;
using Avalonia.Controls;
using SMARX.ViewModels;

namespace SMARX.Views
{
    /// <summary>La fenêtre whoami : elle reste ouverte à côté, et suit les changements de groupes.</summary>
    public partial class WhoamiWindow : Window
    {
        public WhoamiWindow()
        {
            InitializeComponent();
        }

        protected override void OnClosed(EventArgs e)
        {
            (DataContext as WhoamiViewModel)?.Dispose();
            base.OnClosed(e);
        }
    }
}
