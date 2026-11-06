using SMARX.Services;

namespace SMARX.Tests;

/// <summary>
/// Les commandes de l'aide-mémoire, rejouées sur de vrais systèmes par tools/SmarxGlossaryCheck : un Windows 11 du labo
/// (Windows PowerShell 5.1, module NTFSSecurity 4.2.6) et un Debian 13 (setfacl 2.3.2, Samba 4.22). Chaque commande a
/// fait ce que l'aide-mémoire dit ; une commande du module NTFSSecurity a laissé les mêmes ACE que son équivalent
/// icacls, appliqué à une copie identique. Une commande modifiée dans l'aide-mémoire n'a plus de relevé : ce test échoue
/// tant qu'elle n'a pas été rejouée.
/// </summary>
public class GlossaryFidelityTests
{
    private static string[] Lines(string file) => File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fidelity", file));

    public static TheoryData<string> Files => new() { "glossary-windows.txt", "glossary-linux.txt" };

    [Theory]
    [MemberData(nameof(Files))]
    public void EveryCommand_DidWhatTheCheatSheetSays(string file)
    {
        var lines = Lines(file);
        var sections = file.Contains("windows") ? Glossary.Windows("fr-FR") : Glossary.Linux("fr-FR");
        var verdicts = lines.Where(l => !l.StartsWith('#')).ToList();

        Assert.DoesNotContain(verdicts, l => !l.StartsWith("OK ") && !l.StartsWith("SYNTAX "));
        foreach (var command in sections.SelectMany(s => s.CommandList))
            Assert.True(verdicts.Contains("OK " + command.Key) || verdicts.Contains("SYNTAX " + command.Key),
                $"{file} : pas de relevé pour « {command.Key} » (relancer tools/SmarxGlossaryCheck)");
        Assert.Equal(sections.SelectMany(s => s.CommandList).Count(), verdicts.Count);
    }

    /// <summary>Les commandes ne changent pas avec la langue : les deux aide-mémoire ont les mêmes clés.</summary>
    [Fact]
    public void Commands_AreTheSameInBothLanguages()
    {
        foreach (var (fr, en) in new[] { (Glossary.Windows("fr-FR"), Glossary.Windows("en-US")), (Glossary.Linux("fr-FR"), Glossary.Linux("en-US")) })
            Assert.Equal(fr.SelectMany(s => s.CommandList).Select(c => c.Key), en.SelectMany(s => s.CommandList).Select(c => c.Key));
    }

    /// <summary>Ce que les relevés ont appris, et que l'aide-mémoire dit.</summary>
    [Fact]
    public void WhatTheMeasurementsTaught_IsInTheCheatSheet()
    {
        var windows = Lines("glossary-windows.txt");
        // Enable-NTFSAccessInheritance : les mêmes ACE qu'icacls /inheritance:e, dans un autre ordre
        Assert.Contains("# same ACEs, in another order", windows);
        var commands = Glossary.Windows("fr-FR").SelectMany(s => s.CommandList).ToList();
        Assert.Contains("autre ordre", commands.Single(c => c.Command.StartsWith("Enable-NTFSAccessInheritance")).Meaning);
        // Un refus d'écriture : icacls l'affiche (DENY)(WD,AD,WEA,WA), NTFSSecurity le pose sans S
        Assert.Contains("(DENY)(WD,AD,WEA,WA)", commands.Single(c => c.Command.Contains("/deny") && c.Icacls == null).Meaning);
        Assert.Contains("S", commands.Single(c => c.Command.Contains("-AccessType Deny")).Meaning);
        // Add-ADGroupMember modifierait l'annuaire : seule sa syntaxe est vérifiée
        Assert.Contains(windows, l => l.StartsWith("SYNTAX Add-ADGroupMember"));
    }
}
