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
        var rows = Glossary.Windows(culture).SelectMany(s => s.Rows).ToList();

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
}
