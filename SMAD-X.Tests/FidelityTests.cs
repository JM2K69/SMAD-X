using System.Text.RegularExpressions;
using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Audit;

namespace SMADX.Tests;

/// <summary>
/// Le domaine neuf de SMAD-X comparé à l'export d'un vrai domaine tout juste promu, fait avec le script d'import, pour
/// chaque version de Windows Server : mêmes objets, mêmes options de compte et SPN, mêmes propriétaires, mêmes
/// appartenances, même adminCount, mêmes permissions, même politique de mot de passe, et le même audit. Les exports sont
/// dans Fidelity/windows-server-AAAA.smad-x.json, faits une fois SDProp passé sur les comptes protégés.
/// </summary>
public class FidelityTests
{
    private static string File(WindowsServerVersion version) =>
        Path.Combine(AppContext.BaseDirectory, "Fidelity", $"windows-server-{version.ToString()[^4..]}.smad-x.json");

    public static TheoryData<WindowsServerVersion> Versions()
    {
        var data = new TheoryData<WindowsServerVersion>();
        foreach (var profile in WindowsServerProfile.All.Where(p => p.Verified)) data.Add(profile.Version);
        return data;
    }

    [Fact]
    public void EveryVersion_IsVerified_AndHasItsExport()
    {
        Assert.All(WindowsServerProfile.All, p =>
        {
            Assert.True(p.Verified, p.Name);
            Assert.True(System.IO.File.Exists(File(p.Version)), File(p.Version));
        });
    }

    private static async Task<(ADObject Real, ADObject Simulated)> Load(WindowsServerVersion version)
    {
        var result = await new ADDataService().LoadDocumentAsync(File(version));
        Assert.Equal(DocumentLoadStatus.Loaded, result.Status);
        var real = result.Document!.Domain;
        return (real, new ADDataService().CreateFreshDomainStructure(real.Name, version: version));
    }

    private static IEnumerable<ADObject> Flatten(ADObject o) => new[] { o }.Concat(o.Children.SelectMany(Flatten));

    /// <summary>Le premier contrôleur du domaine : son nom est celui du serveur promu.</summary>
    private static ADObject Dc(ADObject domain) => Flatten(domain).First(o => o.Type == ADObjectType.Computer && o.Parent?.Name == "Domain Controllers");

    /// <summary>
    /// Objets comparables, par le chemin de leurs noms (une GPO porte son nom affiché, le DC est « DC ») ; les conteneurs
    /// Machine et User d'une GPO ne sont pas modélisés.
    /// </summary>
    private static Dictionary<string, ADObject> Objects(ADObject domain)
    {
        var dc = Dc(domain);
        string Key(ADObject o) => o.Parent == null ? "" : $"{Key(o.Parent)}/{(o == dc ? "DC" : o.Name)}";
        return Flatten(domain).Where(o => o.Parent?.Type != ADObjectType.Policy).ToDictionary(o => $"{o.Type}:{Key(o)}", StringComparer.OrdinalIgnoreCase);
    }

    private static string Sorted(IEnumerable<string>? values) => string.Join(" | ", (values ?? []).Order(StringComparer.OrdinalIgnoreCase));

    [Theory]
    [MemberData(nameof(Versions))]
    public async Task FreshDomain_HasTheObjectsOfARealOne(WindowsServerVersion version)
    {
        var (real, simulated) = await Load(version);
        var r = Objects(real);
        var s = Objects(simulated);

        Assert.Empty(r.Keys.Except(s.Keys).Order());
        Assert.Empty(s.Keys.Except(r.Keys).Order());
        Assert.Equal(version, simulated.ServerVersion);
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public async Task FreshDomain_HasTheAttributesOfARealOne(WindowsServerVersion version)
    {
        var (real, simulated) = await Load(version);
        var r = Objects(real);
        var s = Objects(simulated);
        var realDc = Dc(real);
        var simulatedDc = Dc(simulated);
        // Les SPN d'un DC portent son nom et l'identifiant de son agent de l'annuaire, propres à chaque serveur
        string Spns(ADObject o, ADObject dc) =>
            Sorted(o.ServicePrincipalNames.Select(spn => Regex.Replace(Regex.Replace(spn, Regex.Escape(dc.Name), "DC", RegexOptions.IgnoreCase), "[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}", "GUID")));

        var differences = new List<string>();
        foreach (var key in r.Keys.Intersect(s.Keys).Order())
        {
            var (a, b) = (r[key], s[key]);
            void Compare(string what, object? x, object? y)
            {
                if (!string.Equals(x?.ToString() ?? "", y?.ToString() ?? "", StringComparison.Ordinal))
                    differences.Add($"{key} {what} : réel [{x}], SMAD-X [{y}]");
            }
            Compare("options", a.AccountFlags, b.AccountFlags);
            Compare("SPN", Spns(a, realDc), Spns(b, simulatedDc));
            Compare("propriétaire", a.Owner, b.Owner);
            Compare("portée", a.GroupScope, b.GroupScope);
            Compare("catégorie", a.GroupCategory, b.GroupCategory);
            Compare("membre de", Sorted(a.MemberOf), Sorted(b.MemberOf));
            Compare("GPO liées", Sorted(a.LinkedGPOs), Sorted(b.LinkedGPOs));
            Compare("administrateurs locaux", Sorted(a.LocalAdmins), Sorted(b.LocalAdmins));
            Compare("LAPS", a.LapsPasswordExpiration != null, b.LapsPasswordExpiration != null);
            Compare("délégation contrainte", Sorted(a.AllowedToDelegateTo), Sorted(b.AllowedToDelegateTo));
            Compare("adminCount", a.AdminCount, b.AdminCount);
        }
        Assert.Empty(differences);
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public async Task FreshDomain_HasThePasswordPolicyOfARealOne(WindowsServerVersion version)
    {
        var (real, simulated) = await Load(version);

        Assert.Equal(
            (real.PSOMinPasswordLength, real.PSOPasswordHistoryCount, real.PSOComplexityEnabled, real.PSOMaxPasswordAgeDays, real.PSOMinPasswordAgeDays,
             real.PSOLockoutThreshold, real.PSOLockoutDurationMinutes, real.PSOLockoutObservationWindowMinutes, real.MachineAccountQuota),
            (simulated.PSOMinPasswordLength, simulated.PSOPasswordHistoryCount, simulated.PSOComplexityEnabled, simulated.PSOMaxPasswordAgeDays, simulated.PSOMinPasswordAgeDays,
             simulated.PSOLockoutThreshold, simulated.PSOLockoutDurationMinutes, simulated.PSOLockoutObservationWindowMinutes, simulated.MachineAccountQuota));
    }

    /// <summary>
    /// Droits que le script de la 0.11.1, qui a fait les exports de référence, ne nommait pas encore : il écrivait les
    /// droits bruts de l'ACE.
    /// </summary>
    private static readonly Dictionary<string, string> NamedSince012 = new()
    {
        ["ExtendedRight"] = "DS-Query-Self-Quota",
        ["CreateChild, ListChildren, ReadProperty, ListObject"] = "CreateChild:msTPM-InformationObject"
    };

    /// <summary>
    /// Les ACE explicites de chaque objet, par mandataire et droit : celles de la racine et des conteneurs, et celles
    /// qu'AdminSDHolder donne aux comptes et groupes protégés. Les droits par défaut de chaque classe, que le script
    /// écarte, ne sont posés nulle part.
    /// </summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public async Task FreshDomain_HasThePermissionsOfARealOne(WindowsServerVersion version)
    {
        var (real, simulated) = await Load(version);
        var r = Objects(real);
        var s = Objects(simulated);
        // SMAD-X ne modélise pas les drapeaux d'héritage : deux ACE qui ne diffèrent que par eux n'en font qu'une
        static string Aces(ADObject o) => Sorted(o.Delegations.Where(d => !d.IsInherited)
            .Select(d => $"{d.TrusteeName}:{NamedSince012.GetValueOrDefault(d.Right, d.Right)} ({d.Effective})").Distinct());

        var differences = r.Keys.Intersect(s.Keys)
            .Where(k => Aces(r[k]) != Aces(s[k]))
            .Select(k => $"{k}\n   réel   : {Aces(r[k])}\n   SMAD-X : {Aces(s[k])}")
            .ToList();
        Assert.True(differences.Count == 0, string.Join("\n", differences));
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public async Task FreshDomain_GetsTheSameAuditAsARealOne(WindowsServerVersion version)
    {
        var (real, simulated) = await Load(version);
        var now = DateTime.Now;
        IEnumerable<string> Failed(ADObject domain) => new ADSecurityAuditService().Run(domain, now).FailedRules.Select(f => f.Id).Order();

        Assert.Equal(Failed(real), Failed(simulated));
        Assert.Equal(["ACC-03", "DLG-04", "PRIV-03", "PWD-01", "PWD-08", "PWD-09"], Failed(simulated));
    }
}
