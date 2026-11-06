using SMARX.Models;

namespace SMARX.Tests;

/// <summary>
/// L'affichage des droits façon icacls, comparé au vrai icacls d'un Windows 11 (tools/SmarxWindowsCheck/icacls-display.ps1,
/// figé dans Fidelity/icacls-display.txt) : chacune des 16 383 combinaisons des 14 droits spéciaux, autorisée et refusée,
/// et les drapeaux d'héritage. icacls regroupe d'abord les autorisations de base (F, M, RX, R, W, D), puis range le reste
/// dans son ordre à lui, écrit « Rc » et affiche un refus de Contrôle total « (N) ».
/// </summary>
public class IcaclsDisplayFidelityTests
{
    private static readonly string[] Lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fidelity", "icacls-display.txt"));

    private static IEnumerable<(NtfsRights Mask, string Shown)> Masks(string kind) =>
        Lines.Where(l => l.StartsWith(kind + " ")).Select(l => l.Split(' ', 3)).Select(p => ((NtfsRights)Convert.ToInt32(p[1], 16), p[2]));

    [Theory]
    [InlineData("A", AceType.Allow, 16383)]
    [InlineData("D", AceType.Deny, 257)]
    public void EveryMask_IsDisplayedLikeIcacls(string kind, AceType type, int count)
    {
        var masks = Masks(kind).ToList();
        Assert.Equal(count, masks.Count);
        var wrong = masks.Where(m => Ntfs.DisplayAce(false, AceInheritance.None, type, m.Mask) != m.Shown)
            .Select(m => $"0x{(int)m.Mask:X} : icacls {m.Shown}, SMAR-X {Ntfs.DisplayAce(false, AceInheritance.None, type, m.Mask)}").Take(10).ToList();
        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    /// <summary>(OI)(CI)(NP)(IO), dans cet ordre ; NP n'apparaît qu'avec OI ou CI ; le refus vient après les drapeaux.</summary>
    [Fact]
    public void InheritanceFlags_AreDisplayedLikeIcacls()
    {
        var lines = Lines.Where(l => l.StartsWith("FLAGS ")).Select(l => l.Split(' ', 5)).ToList();
        Assert.Equal(196, lines.Count);
        foreach (var p in lines)
        {
            var inheritance = AceInheritance.None;
            var set = p[2] == "-" ? "" : p[2];
            for (var i = 0; i < set.Length; i += 2)
                inheritance |= set.Substring(i, 2) switch
                {
                    "OI" => AceInheritance.ObjectInherit,
                    "CI" => AceInheritance.ContainerInherit,
                    "IO" => AceInheritance.InheritOnly,
                    "NP" => AceInheritance.NoPropagate,
                    var other => throw new InvalidDataException(other)
                };
            var type = p[1] == "D" ? AceType.Deny : AceType.Allow;
            Assert.Equal(p[4], Ntfs.DisplayAce(false, inheritance, type, (NtfsRights)Convert.ToInt32(p[3], 16)));
        }
    }

    /// <summary>Ce qu'affiche icacls se relit : « (RX,WD,AD) », « (D,Rc,WDAC,WO) » redonnent le masque.</summary>
    [Fact]
    public void WhatIcaclsShows_ParsesBackToTheMask()
    {
        foreach (var (mask, shown) in Masks("A"))
            Assert.Equal(mask, Ntfs.Parse(shown));
    }
}
