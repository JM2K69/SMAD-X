using Avalonia.Controls;
using Avalonia.Interactivity;
using SMADX.ViewModels;
using SMADX.Models;

namespace SMADX.Views
{
    public partial class TierConfigurationWindow : Window
    {
        public TierConfigurationWindow()
        {
            InitializeComponent();
            DataContext = new TierConfigurationViewModel();
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void OnSelectColorClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is TierConfigurationViewModel vm)
            {
                var dialog = new ColorPickerDialog(vm.NewTierColor ?? "#808080");
                var result = await dialog.ShowDialog<string?>(this);

                if (result != null)
                {
                    vm.NewTierColor = result;
                }
            }
        }

        private void OnMoveUpClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && 
                button.DataContext is TierConfiguration tier &&
                DataContext is TierConfigurationViewModel vm)
            {
                var tiers = vm.Tiers;
                var index = tiers.IndexOf(tier);

                if (index > 0)
                {
                    tiers.RemoveAt(index);
                    tiers.Insert(index - 1, tier);
                    vm.SelectedTier = tier;
                }
            }
        }

        private void OnMoveDownClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && 
                button.DataContext is TierConfiguration tier &&
                DataContext is TierConfigurationViewModel vm)
            {
                var tiers = vm.Tiers;
                var index = tiers.IndexOf(tier);

                if (index >= 0 && index < tiers.Count - 1)
                {
                    tiers.RemoveAt(index);
                    tiers.Insert(index + 1, tier);
                    vm.SelectedTier = tier;
                }
            }
        }
    }
}
