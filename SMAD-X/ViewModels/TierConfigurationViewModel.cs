using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;
using SMADX.Services;

namespace SMADX.ViewModels
{
    public partial class TierConfigurationViewModel : ViewModelBase
    {
        private readonly TierConfigurationService _tierService;

        [ObservableProperty]
        private ObservableCollection<TierConfiguration> _tiers;

        [ObservableProperty]
        private TierConfiguration? _selectedTier;

        [ObservableProperty]
        private string _newTierName = string.Empty;

        [ObservableProperty]
        private string _newTierColor = "#808080";

        [ObservableProperty]
        private string _newTierDescription = string.Empty;

        [ObservableProperty]
        private int _newTierLevel = 0;

        public TierConfigurationViewModel()
        {
            _tierService = TierConfigurationService.Instance;
            _tiers = _tierService.Tiers;
        }

        [RelayCommand]
        private void AddTier()
        {
            if (string.IsNullOrWhiteSpace(NewTierName))
                return;

            var newTier = new TierConfiguration(NewTierName, NewTierColor, NewTierDescription, NewTierLevel);
            _tierService.AddTier(newTier);

            // Réinitialiser les champs
            NewTierName = string.Empty;
            NewTierColor = "#808080";
            NewTierDescription = string.Empty;
            NewTierLevel = Tiers.Count;
        }

        [RelayCommand]
        private void RemoveTier()
        {
            if (SelectedTier == null)
                return;

            _tierService.RemoveTier(SelectedTier);
            SelectedTier = null;
        }

        [RelayCommand]
        private void ResetToDefaults()
        {
            var loc = LocalizationService.Instance;
            Tiers.Clear();
            Tiers.Add(new TierConfiguration("Tier 0", "#FF0000", loc["TierConfig.Default.Tier0"], 0));
            Tiers.Add(new TierConfiguration("Tier 1", "#FFA500", loc["TierConfig.Default.Tier1"], 1));
            Tiers.Add(new TierConfiguration("Tier 2", "#00FF00", loc["TierConfig.Default.Tier2"], 2));
        }
    }
}
