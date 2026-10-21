using System.Diagnostics;
using System.Text;
using SMADX.Models;
using SMADX.Services;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

public class ADPowerShellExportServiceTests
{
    // Noms piégés : chacun serait interprété par PowerShell s'il n'était pas échappé
    private const string TrickyOu = "Test $env:USERNAME `\"q\" “smart”";
    private const string TrickyUser = "j$doe";
    private const string TrickyGpo = "GPO $(Get-Date)";

    /// <summary>
    /// Moteurs sur lesquels le script exporté doit fonctionner : PowerShell 7 partout,
    /// Windows PowerShell 5.1 (celui des contrôleurs de domaine) sous Windows.
    /// </summary>
    public static TheoryData<string> Shells()
    {
        var shells = new TheoryData<string> { "pwsh" };
        if (OperatingSystem.IsWindows())
            shells.Add("powershell");
        return shells;
    }

    private static async Task<(string Path, string Script)> ExportAsync(ADObject root)
    {
        var path = TempFile(".ps1");
        Assert.True(await new ADPowerShellExportService().ExportToPowerShellAsync(root, path));
        return (path, await File.ReadAllTextAsync(path));
    }

    private static ADObject BuildTrickyDomain()
    {
        var root = new ADDataService().CreateSampleStructure();
        var ou = Add(root, new ADObject(TrickyOu, ADObjectType.OrganizationalUnit) { Description = "Coût $100 `n \"x\"" });
        var user = Add(ou, new ADObject(TrickyUser, ADObjectType.User));
        user.MemberOf.Add("Domain Admins");
        ou.LinkedGPOs.Add(TrickyGpo);
        return root;
    }

    [Fact]
    public async Task Export_WritesUtf8WithBom()
    {
        var (path, _) = await ExportAsync(BuildSmallDomain());

        var bytes = await File.ReadAllBytesAsync(path);
        Assert.Equal(Encoding.UTF8.Preamble.ToArray(), bytes[..3]);
    }

    [Fact]
    public async Task Export_ExistenceChecksDoNotRelyOnSilentlyContinue()
    {
        // Get-ADxxx -Identity lève une erreur terminante quand l'objet n'existe pas
        var (_, script) = await ExportAsync(BuildSmallDomain());

        Assert.DoesNotContain("-Identity $dn -ErrorAction SilentlyContinue", script);
        Assert.DoesNotContain("-Identity $SamAccountName -ErrorAction SilentlyContinue", script);
    }

    [Fact]
    public async Task Export_EscapesPowerShellSpecialCharacters()
    {
        var (_, script) = await ExportAsync(BuildTrickyDomain());

        Assert.Contains("-SamAccountName \"j`$doe\"", script);
        Assert.Contains("New-GPLink -Name \"GPO `$(Get-Date)\"", script);
        Assert.Contains("-Name \"Test `$env:USERNAME ```\"q`\" `“smart`”\"", script);
        Assert.DoesNotContain("$(Get-Date)\"", script.Replace("`$(Get-Date)", ""));
    }

    [Theory]
    [MemberData(nameof(Shells))]
    public async Task Export_ProducesScriptWithoutParseErrors(string shell)
    {
        var (path, _) = await ExportAsync(BuildTrickyDomain());

        var output = await RunAsync(shell, "-Command",
            "$t=$null;$e=$null;" +
            $"[void][System.Management.Automation.Language.Parser]::ParseFile('{path}',[ref]$t,[ref]$e);" +
            "$e | ForEach-Object { \"$($_.Extent.StartLineNumber): $($_.Message)\" }");

        Assert.True(string.IsNullOrWhiteSpace(output), $"Erreurs d'analyse {shell} :\n{output}");
    }

    [Theory]
    [MemberData(nameof(Shells))]
    public async Task Export_EscapedValuesExpandBackToOriginalNames(string shell)
    {
        var (_, script) = await ExportAsync(BuildTrickyDomain());
        var line = script.Split('\n').First(l => l.StartsWith("New-ADOrganizationalUnitIfNotExists -Name \"Test"));
        var quoted = line[(line.IndexOf("-Name ") + 6)..line.IndexOf(" -Path ")];

        var file = TempFile(".ps1");
        await File.WriteAllTextAsync(file,
            $"[Console]::OutputEncoding = [Text.Encoding]::UTF8\nWrite-Output {quoted}", new UTF8Encoding(true));
        var output = await RunAsync(shell, "-File", file);

        Assert.Equal(TrickyOu, output.Trim());
    }

    /// <summary>
    /// Lance un moteur PowerShell ; échoue explicitement s'il n'est pas installé,
    /// pour qu'un test ne passe jamais sans avoir réellement vérifié le script.
    /// </summary>
    private static async Task<string> RunAsync(string shell, string mode, string argument)
    {
        var psi = new ProcessStartInfo(shell, ["-NoProfile", "-NonInteractive", mode, argument])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        Process process;
        try
        {
            process = Process.Start(psi)!;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Assert.Fail($"« {shell} » est introuvable : il doit être installé et dans le PATH pour valider l'export PowerShell.");
            throw; // inatteignable, Assert.Fail lève toujours
        }

        using (process)
        {
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            Assert.True(process.ExitCode == 0, $"{shell} a échoué (code {process.ExitCode}) :\n{await stderr}");
            return await stdout;
        }
    }
}
