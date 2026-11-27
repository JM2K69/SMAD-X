using System.Text.Json;

namespace SMAGX;

/// <summary>
/// Les réglages de SMAG-X sur ce poste, dans %LocalAppData%\SMAG-X\settings.json : l'identité choisie au premier
/// lancement (nom, adresse, nom d'utilisateur du poste simulé). Git ne la reçoit pas : l'élève la lui donne avec git config.
/// </summary>
public sealed class SmagxSettings
{
    /// <summary>Le fichier des réglages ; les tests le remplacent par un fichier temporaire.</summary>
    public static string DefaultPath { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SMAG-X", "settings.json");

    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Login { get; set; }

    public bool HasIdentity => !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Login);

    public static SmagxSettings Load()
    {
        try
        {
            return File.Exists(DefaultPath)
                ? JsonSerializer.Deserialize<SmagxSettings>(File.ReadAllText(DefaultPath)) ?? new SmagxSettings()
                : new SmagxSettings();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return new SmagxSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DefaultPath)!);
            File.WriteAllText(DefaultPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Le nom d'utilisateur du poste tiré du prénom : minuscules, sans accent ni espace (« Élise Martin » → « elise »).</summary>
    public static string LoginFrom(string name)
    {
        var first = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "eleve";
        var normalized = first.Normalize(System.Text.NormalizationForm.FormD);
        var login = new string(normalized.Where(c => char.IsAsciiLetterOrDigit(c)).ToArray()).ToLowerInvariant();
        return login.Length == 0 ? "eleve" : login;
    }
}
