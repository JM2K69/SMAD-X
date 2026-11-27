using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMAGX.Forge;

namespace SMAGX.ViewModels;

/// <summary>Une clé de la page « Clés SSH » : titre, type et empreinte, date d'ajout.</summary>
public sealed record KeyItem(string Title, string Fingerprint, string Details);

/// <summary>
/// La page « Clés SSH » d'un compte de la forge, comme sur GitLab ou GitHub : l'élève y colle sa clé publique (le
/// contenu du fichier .pub) ; la forge le reconnaîtra ensuite à cette clé.
/// </summary>
public partial class KeysViewModel(IForge forge, string account) : ObservableObject
{
    public IForge Forge { get; } = forge;
    public string Account { get; } = account;

    public ObservableCollection<KeyItem> Keys { get; } = [];

    [ObservableProperty] private string _keyText = "";
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _error = "";

    public bool NoKey => Keys.Count == 0;

    private static SmagxText Text => SmagxText.Instance;

    public string Heading => Text.T($"Clés SSH de {Account} sur {Forge.Host}", $"SSH keys of {Account} on {Forge.Host}");

    public async Task RefreshAsync()
    {
        try
        {
            var keys = await Task.Run(() => Forge.Keys(Account));
            Keys.Clear();
            foreach (var key in keys)
                Keys.Add(new KeyItem(key.Title, key.Fingerprint, $"{key.Label} · {Text.T("ajoutée le", "added on")} {key.Added.ToLocalTime():dd/MM/yyyy HH:mm}"));
        }
        catch (ForgeUnavailableException)
        {
            Error = Text["Forge.Unreachable"];
        }
        OnPropertyChanged(nameof(NoKey));
    }

    /// <summary>Ajoute la clé collée ; refuse une clé privée, un texte qui n'est pas une clé, une clé déjà enregistrée.</summary>
    [RelayCommand]
    private async Task Add()
    {
        Error = "";
        var text = KeyText.Trim();
        if (text.Contains("PRIVATE KEY"))
        {
            Error = Text["Keys.Private"];
            return;
        }
        string? refused;
        try
        {
            var title = Title;
            refused = await Task.Run(() => Forge.AddKey(Account, title, text.Split('\n')[0]));
        }
        catch (ForgeUnavailableException)
        {
            Error = Text["Forge.Unreachable"];
            return;
        }
        if (refused is not null)
        {
            Error = Text[refused == "taken" ? "Keys.Taken" : "Keys.Invalid"];
            return;
        }
        KeyText = "";
        Title = "";
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task Remove(KeyItem key)
    {
        try
        {
            await Task.Run(() => Forge.RemoveKey(Account, key.Fingerprint));
        }
        catch (ForgeUnavailableException)
        {
            Error = Text["Forge.Unreachable"];
        }
        await RefreshAsync();
    }
}
