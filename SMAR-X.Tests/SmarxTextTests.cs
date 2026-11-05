using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace SMARX.Tests;

/// <summary>
/// Textes de l'interface de SMAR-X : chaque texte des vues est une ressource dynamique ({DynamicResource Clé}),
/// réécrite à chaque changement de langue, et chaque clé existe en français et en anglais. Une liaison par indexeur
/// ({Binding [Clé], Source=…}) ne se rafraîchirait pas : Avalonia 12 ignore la notification « Item » quand l'indexeur
/// est hérité d'une classe de base (celui du Localizer), et la fenêtre resterait à moitié traduite.
/// </summary>
public partial class SmarxTextTests
{
    [Fact]
    public void FrenchAndEnglish_HaveTheSameKeys()
    {
        var text = SmarxText.Instance;

        Assert.Equal(text.KeysOf("fr-FR").Order(), text.KeysOf("en-US").Order());
    }

    [Fact]
    public void EveryViewText_IsADynamicResource_TranslatedInBothLanguages()
    {
        var root = RepoRoot();
        var themeKeys = ThemeKeyRegex().Matches(File.ReadAllText(Path.Combine(root, "SMAR-X", "App.axaml"))).Select(m => m.Groups[1].Value).ToHashSet();
        var text = SmarxText.Instance;

        foreach (var view in Directory.GetFiles(Path.Combine(root, "SMAR-X", "Views"), "*.axaml"))
        {
            var xaml = File.ReadAllText(view);
            Assert.DoesNotMatch(@"\{Binding \[[^\]]+\], Source=", xaml);
            var missing = DynamicKeyRegex().Matches(xaml).Select(m => m.Groups[1].Value)
                .Where(k => !themeKeys.Contains(k))
                .Where(k => !text.KeysOf("fr-FR").Contains(k) || !text.KeysOf("en-US").Contains(k))
                .ToList();
            Assert.True(missing.Count == 0, $"{Path.GetFileName(view)} : clés sans traduction {string.Join(", ", missing)}");
        }
    }

    /// <summary>Racine du dépôt (dossier de SMAD-X.slnx), depuis les binaires des tests ou, s'ils sont construits ailleurs, depuis ce fichier.</summary>
    private static string RepoRoot()
    {
        return Find(AppContext.BaseDirectory) ?? Find(Path.GetDirectoryName(ThisFile())!)
            ?? throw new DirectoryNotFoundException("SMAD-X.slnx introuvable au-dessus de " + AppContext.BaseDirectory);

        static string? Find(string start)
        {
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "SMAD-X.slnx")))
                    return dir.FullName;
            return null;
        }
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;

    [GeneratedRegex(@"\{DynamicResource ([A-Za-z.]+)\}")]
    private static partial Regex DynamicKeyRegex();

    [GeneratedRegex(@"x:Key=""([A-Za-z]+)""")]
    private static partial Regex ThemeKeyRegex();
}
