using CommunityToolkit.Mvvm.Input;

namespace SMADX.ViewModels
{
    // Avant / après : point de référence et fenêtre de comparaison
    public partial class MainWindowViewModel
    {
        /// <summary>JSON du document au point de référence de la comparaison « avant / après ».</summary>
        private string? _baseline;

        /// <summary>Le point de référence est l'état du domaine à son ouverture.</summary>
        private void ResetBaseline() => _baseline = RootObject != null ? SerializeDocument() : null;

        /// <summary>Repose le point de référence sur l'état actuel, par exemple avant de commencer une correction.</summary>
        [RelayCommand]
        private void MarkBaseline()
        {
            ResetBaseline();
            StatusMessage = Loc["Comparison.BaselineSet"];
        }

        [RelayCommand]
        private void ShowComparison()
        {
            if (RootObject == null) return;
            RecordHistory();
            _dialogs.ShowComparison(new ComparisonViewModel(() => _baseline, () => RootObject));
        }
    }
}
