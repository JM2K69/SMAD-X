using Avalonia.Controls;
using SMADX.Models;
using SMADX.ViewModels;

namespace SMADX.Views
{
    public partial class SitesWindow : Window
    {
        public SitesWindow()
        {
            InitializeComponent();
            DataContext = new SitesViewModel();
        }

        public SitesWindow(ADSitesTopology topology) : this()
        {
            if (DataContext is SitesViewModel vm)
                vm.LoadTopology(topology);
        }
    }
}
