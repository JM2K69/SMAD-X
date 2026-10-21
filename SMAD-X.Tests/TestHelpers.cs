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
