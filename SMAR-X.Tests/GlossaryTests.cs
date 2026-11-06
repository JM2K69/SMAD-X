using SMARX.Models;
using SMARX.Services;

namespace SMARX.Tests;

/// <summary>L'aide-mémoire explique chaque abréviation que montre SMAR-X, en français et en anglais.</summary>
public class GlossaryTests
{
    [Theory]
    [InlineData("fr-FR")]
    [InlineData("en-US")]
    public void EveryIcaclsAbbreviation_IsExplained(string culture)
    {
        // Les abréviations expliquées ; la section NTFSSecurity reprend les mêmes codes avec ses noms à elle
        var rows = Glossary.Windows(culture).Where(s => !s.Title.Contains("NTFSSecurity")).SelectMany(s => s.Rows).ToList();

        foreach (var code in Ntfs.Special.Select(s => s.Code).Concat(Ntfs.Simple.Select(s => s.Code)))
            Assert.Contains(rows, r => r.Code == code && r.Name.Length > 0 && r.Meaning.Length > 0);
        foreach (var flag in new[] { "(I)", "(OI)", "(CI)", "(IO)", "(NP)", "(DENY)" })
            Assert.Contains(rows, r => r.Code == flag);
        Assert.Equal("RD, RA, REA, RC, S", rows.Single(r => r.Code == "R").Meaning);
        Assert.Equal("0x1301BF", rows.Single(r => r.Code == "M").Value);
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("en-US")]
    public void EveryLinuxNotation_IsExplained(string culture)
    {
        var rows = Glossary.Linux(culture).SelectMany(s => s.Rows).ToList();

        foreach (var code in new[] { "r", "w", "x", "d", "s / S", "t / T", "+", "user::", "group::", "mask::", "other::", "#effective:", "default:", "022" })
            Assert.Contains(rows, r => r.Code == code && r.Meaning.Length > 0);
    }

    /// <summary>
    /// Des commandes d'exemple dans chaque onglet, chacune expliquée ; pour NTFSSecurity, l'équivalent icacls de chaque
    /// commande qui en a un, et le nom de chaque droit d'icacls dans -AccessRights.
    /// </summary>
    [Theory]
    [InlineData("fr-FR")]
    [InlineData("en-US")]
    public void Examples_AreExplained_AndNtfsSecurityNamesEveryIcaclsRight(string culture)
    {
        var windows = Glossary.Windows(culture);
        var linux = Glossary.Linux(culture);
        Assert.All(windows.Concat(linux).SelectMany(s => s.CommandList), c => Assert.True(c.Command.Length > 0 && c.Meaning.Length > 0));
        Assert.Contains(windows.SelectMany(s => s.CommandList), c => c.Command.StartsWith("icacls "));
        Assert.Contains(windows.SelectMany(s => s.CommandList), c => c.Command.StartsWith("Get-Acl "));
        Assert.Contains(linux.SelectMany(s => s.CommandList), c => c.Command.StartsWith("setfacl -d -m "));

        var module = windows.Where(s => s.Title.Contains("NTFSSecurity")).ToList();
        Assert.Equal(3, module.Count);
        var commands = module.SelectMany(s => s.CommandList).ToList();
        Assert.Contains(commands, c => c.Command.StartsWith("Add-NTFSAccess ") && c.Icacls!.Contains("/grant"));
        Assert.All(commands.Where(c => c.HasIcacls), c => Assert.StartsWith("icacls ", c.Icacls));
        var rows = module.SelectMany(s => s.Rows).ToList();
        foreach (var code in Ntfs.Special.Select(s => s.Code).Concat(Ntfs.Simple.Select(s => s.Code)))
            Assert.Contains(rows, r => r.Code == code && r.Name.Length > 0 && r.Meaning.Length > 0);
        Assert.Equal("ThisFolderSubfoldersAndFiles", rows.Single(r => r.Code == "(OI)(CI)").Name);
        Assert.Equal("Traverse, ExecuteFile", rows.Single(r => r.Code == "X").Name);
    }

    /// <summary>
    /// Le PDF : le bouclier de SMAR-X en tête, d'où vient l'aide-mémoire (SMAR-X et sa version, la suite SMA-X et son
    /// dépôt GitHub), les deux onglets, et chaque commande dans un bloc de code.
    /// </summary>
    [Fact]
    public void Pdf_ShowsTheShield_AndWhereItComesFrom()
    {
        var markdown = Glossary.Markdown("fr-FR", "0.13.1", new DateTime(2026, 11, 6));

        Assert.StartsWith("![SMAR-X](data:image/png;base64,iVBOR", markdown);
        Assert.Contains("Extrait de **SMAR-X 0.13.1**, de la suite **SMA-X**", markdown);
        Assert.Contains("le 6 novembre 2026", markdown);
        Assert.Contains("https://github.com/JM2K69/SMAD-X", markdown);
        Assert.Contains("## Windows", markdown);
        Assert.Contains("## Linux", markdown);
        Assert.Contains("# NTFSSecurity\nAdd-NTFSAccess", markdown);
        Assert.Contains("```\nsetfacl -m u:bob:rw", markdown);

        var file = Path.Combine(Path.GetTempPath(), "smarx-glossary-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            Glossary.SavePdf(file, "en-US", "0.13.1", new DateTime(2026, 11, 6));
            var pdf = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(file));
            Assert.StartsWith("%PDF", pdf);
            Assert.Contains("/Subtype /Image", pdf);
        }
        finally
        {
            File.Delete(file);
        }
    }
}
