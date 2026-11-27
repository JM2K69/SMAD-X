using Avalonia.Controls;
using SMAGX.Labs;
using SMAGX.ViewModels;

namespace SMAGX.Views;

/// <summary>Les fenêtres que l'application ouvre au-dessus de la fenêtre principale.</summary>
public sealed class WindowDialogs(Window owner) : IDialogs
{
    public Task<SmagxSettings?> AskIdentityAsync(SmagxSettings current, IReadOnlyCollection<string> takenLogins, bool another) =>
        new IdentityWindow(current, takenLogins, another).ShowDialog<SmagxSettings?>(owner);

    public Task<string?> EditAsync(string text) =>
        new EditorWindow(text).ShowDialog<string?>(owner);

    public Task CollaborateAsync(MainWindowViewModel vm) =>
        new CollaborateWindow { DataContext = vm }.ShowDialog(owner);

    public Task ManageKeysAsync(KeysViewModel keys) =>
        new KeysWindow { DataContext = keys }.ShowDialog(owner);

    public Task<(SmagxLab Lab, ScenarioMode Mode)?> ChooseScenarioAsync(bool joined)
    {
        var picker = new ScenarioPickerWindow { DataContext = new ScenarioPickerViewModel(SmagxBuiltInLabs.All(), joined) };
        picker.Opened += (_, _) => picker.Select(pair: false, 0);
        return picker.ShowDialog<(SmagxLab Lab, ScenarioMode Mode)?>(owner);
    }
}
