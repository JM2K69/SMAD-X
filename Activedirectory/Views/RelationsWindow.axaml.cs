using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SMADX.Views
{
    public partial class RelationsWindow : Window
    {
        public RelationsWindow()
        {
            InitializeComponent();
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
