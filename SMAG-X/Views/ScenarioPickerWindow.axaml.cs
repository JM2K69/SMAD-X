using Avalonia.Controls;
using Avalonia.Interactivity;
using SMAGX.Labs;
using SMAGX.ViewModels;

namespace SMAGX.Views;

/// <summary>Le parcours des scénarios : rend le scénario choisi et où il se joue, ou null.</summary>
public partial class ScenarioPickerWindow : Window
{
    public ScenarioPickerWindow() => InitializeComponent();

    private ScenarioPickerViewModel? Vm => DataContext as ScenarioPickerViewModel;

    /// <summary>Choisit un scénario du parcours : seul (<paramref name="pair"/> faux) ou à deux, par sa place dans la liste.</summary>
    public void Select(bool pair, int index) => (pair ? PairList : SoloList).SelectedIndex = index;

    // Une seule sélection pour les deux listes (seul, à deux)
    private void OnSelect(object? sender, SelectionChangedEventArgs e)
    {
        if (Vm is null || sender is not ListBox { SelectedItem: ScenarioCard card } list) return;
        Vm.Selected = card;
        var other = list == SoloList ? PairList : SoloList;
        other.SelectedItem = null;
    }

    private void Choose(ScenarioMode mode)
    {
        if (Vm?.Selected is { } card) Close(((SmagxLab Lab, ScenarioMode Mode)?)(card.Lab, mode));
    }

    private void OnHere(object? sender, RoutedEventArgs e) => Choose(ScenarioMode.Here);
    private void OnServe(object? sender, RoutedEventArgs e) => Choose(ScenarioMode.Serve);
    private void OnJoin(object? sender, RoutedEventArgs e) => Choose(ScenarioMode.Join);
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
