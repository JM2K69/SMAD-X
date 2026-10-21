using System.Diagnostics;
using System.Text;
using SMADX.Models;

// Les services s'appuient sur des singletons (tiers, localisation) : pas d'exécution parallèle
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace SMADX.Tests;

internal static class TestHelpers
{
    /// <summary>Crée un domaine minimal : contoso.com / OU=Corp / jdoe + Helpdesk.</summary>
    public static ADObject BuildSmallDomain()
    {
        var domain = new ADObject("contoso.com", ADObjectType.Domain);
        var ou = Add(domain, new ADObject("Corp", ADObjectType.OrganizationalUnit) { Tier = "Tier 2" });
        Add(ou, new ADObject("jdoe", ADObjectType.User));
        Add(ou, new ADObject("Helpdesk", ADObjectType.Group));
        UpdateDns(domain);
        return domain;
    }

    public static ADObject Add(ADObject parent, ADObject child)
    {
        child.Parent = parent;
        parent.Children.Add(child);
        child.UpdateDistinguishedName();
        return child;
    }

    public static ADObject Find(ADObject root, string name) =>
        Flatten(root).First(o => o.Name == name);

    public static IEnumerable<ADObject> Flatten(ADObject root) =>
        new[] { root }.Concat(root.Children.SelectMany(Flatten));

    public static void UpdateDns(ADObject obj)
    {
        obj.UpdateDistinguishedName();
        foreach (var child in obj.Children)
            UpdateDns(child);
    }

    public static string TempFile(string extension)
    {
        var dir = Path.Combine(Path.GetTempPath(), "smadx-tests");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"{Guid.NewGuid():N}{extension}");
    }
}

/// <summary>Exécute un moteur PowerShell (pwsh ou powershell) pour les tests d'export et d'import.</summary>
internal static class PowerShellRunner
{
    /// <summary>
    /// Lance un moteur PowerShell ; échoue explicitement s'il n'est pas installé,
    /// pour qu'un test ne passe jamais sans avoir réellement vérifié le script.
    /// </summary>
    public static async Task<string> RunAsync(string shell, string mode, string argument)
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
