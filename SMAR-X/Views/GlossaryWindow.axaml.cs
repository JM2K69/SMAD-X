using Avalonia.Controls;
using SMARX.ViewModels;

namespace SMARX.Views
{
    /// <summary>Aide-mémoire des abréviations, ouvert à côté de la fenêtre principale (une seule fois à la fois).</summary>
    public partial class GlossaryWindow : Window
    {
        private static GlossaryWindow? _open;

        public GlossaryWindow()
        {
            InitializeComponent();
        }

        /// <summary>Ouvre l'aide-mémoire sur un onglet (0 : Windows, 1 : Linux), ou l'amène devant s'il est déjà ouvert.</summary>
        public static void Show(Window? owner, int tab)
        {
            if (_open is { } window)
            {
                ((GlossaryViewModel)window.DataContext!).Tab = tab;
                window.Activate();
                return;
            }
            var vm = new GlossaryViewModel(tab);
            _open = new GlossaryWindow { DataContext = vm };
            _open.Closed += (_, _) =>
            {
                vm.Detach();
                _open = null;
            };
            if (owner != null) _open.Show(owner); else _open.Show();
        }
    }
}
