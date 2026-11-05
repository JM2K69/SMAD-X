using SMARX.Models;
using SMARX.Services.Windows;
using static SMARX.Models.AceInheritance;

namespace SMARX.Tests;

/// <summary>
/// L'onglet Sécurité et la fenêtre Avancé de SMAR-X donnent ce que montre l'Explorateur. Fidelity/windows-security-tab.txt
/// a été relevé sur un Windows 11 (tools/SmarxSecurityTabCheck) : un dossier F1 où chaque compte a ses ACE (une
/// autorisation de base, une portée incomplète, (NP), (IO), des droits en plus, deux ACE complémentaires…), son
/// sous-dossier Sub qui en hérite (avec une ACE explicite en plus), et deux fichiers.
/// </summary>
public class WindowsSecurityTabTests
{
    private static readonly string[] Lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fidelity", "windows-security-tab.txt"));
    private const string Admins = WellKnown.Administrators;
    private const AceInheritance All = ThisFolderSubfoldersAndFiles;

    /// <summary>Le dossier du relevé, avec les mêmes ACE (GA de icacls : F sur ce dossier, et F à transmettre).</summary>
    public static WindowsHost Battery()
    {
        var host = new WindowsHost { Name = "CLI" };
        foreach (var g in new[] { "Guests", "Power Users", "Backup Operators", "Replicator", "Remote Desktop Users", "Network Configuration Operators",
                     "Performance Monitor Users", "Performance Log Users", "Distributed COM Users", "IIS_IUSRS", "Cryptographic Operators",
                     "Event Log Readers", "Hyper-V Administrators", "Access Control Assistance Operators", "Remote Management Users", "Device Owners",
                     "BATCH", "SERVICE" })
            host.Groups.Add(new WindowsGroup { Name = g });
        static NtfsAce A(string who, string rights, AceInheritance scope = All) => NtfsAce.Allow(who, rights, scope);
        static NtfsAce D(string who, string rights, AceInheritance scope = All) => NtfsAce.Deny(who, rights, scope);
        static string Of(NtfsRights rights) => Ntfs.Format(rights);

        host.Root = NtfsNode.Directory("D:", Admins, A(Admins, "F")).With(
            NtfsNode.Directory("F1", Admins,
                    D("Hyper-V Administrators", "RX"), D("Remote Management Users", "F"), D("Power Users", Ntfs.DenyWrite), D("BATCH", "W"),
                    D(WellKnown.Network, Ntfs.DenyWrite),
                    A(Admins, "F"), A(WellKnown.System, "F"), A(WellKnown.Users, "F"), A(WellKnown.AuthenticatedUsers, "M"), A(WellKnown.Everyone, "RX"),
                    A("Guests", "RX", ContainerInherit), A("Backup Operators", "RX", ObjectInherit), A("Remote Desktop Users", "RX", None),
                    A("Network Configuration Operators", "R"), A("Performance Monitor Users", "W"), A("Event Log Readers", "RX", All | InheritOnly),
                    A(WellKnown.Interactive, "RX", All | NoPropagate), A("SERVICE", "(RD,X,WD,AD,RA,REA,RC,S)", None),
                    A(WellKnown.CreatorOwner, "F", All | InheritOnly), A("Power Users", Of(NtfsRights.ReadAndExecute | NtfsRights.Delete)),
                    A("Replicator", "RX", ObjectInherit), A("Replicator", "RX", ContainerInherit),
                    A("Distributed COM Users", Of(NtfsRights.ReadAndExecute | NtfsRights.DeleteSubdirectoriesAndFiles)), A("IIS_IUSRS", "(RD)"),
                    A("Cryptographic Operators", "M", None), A("Cryptographic Operators", "M", ObjectInherit | InheritOnly),
                    A("Cryptographic Operators", "M", ContainerInherit | InheritOnly),
                    A("Access Control Assistance Operators", Of(NtfsRights.ReadAndExecute | NtfsRights.Write)),
                    A("Performance Log Users", "F", None), A("Performance Log Users", "F", All | InheritOnly),
                    A("Device Owners", "M", ContainerInherit), A(WellKnown.OwnerRights, "(RC)"))
                .Protected()
                .With(
                    NtfsNode.Directory("Sub", Admins, A("Guests", "M")).With(NtfsNode.File("g.txt", Admins)),
                    NtfsNode.File("f.txt", Admins, A(WellKnown.Everyone, "R", None), D(WellKnown.AuthenticatedUsers, Ntfs.DenyWrite, None))));
        return host;
    }

    private static IEnumerable<(string Path, string[] Lines)> Objects()
    {
        string? path = null;
        var current = new List<string>();
        foreach (var line in Lines.Where(l => !l.StartsWith('#')))
        {
            if (line.StartsWith("OBJ|"))
            {
                if (path != null) yield return (path, current.ToArray());
                path = line[4..];
                current = [];
            }
            else current.Add(line);
        }
        if (path != null) yield return (path, current.ToArray());
    }

    [Fact]
    public void EveryCheckMark_IsTheOneTheExplorerShows()
    {
        var host = Battery();
        var objects = Objects().ToList();
        Assert.Equal(4, objects.Count);
        var differences = new List<string>();
        foreach (var (path, lines) in objects)
        {
            var folder = host.Find(path)!.IsDirectory;
            Assert.Equal(lines.Single(l => l.StartsWith("ROWS|"))[5..], string.Join(" ; ", NtfsSecurityTab.Rows(folder).Select(r => NtfsSecurityTab.Name(r, "en-US"))));

            var tab = lines.Where(l => l.StartsWith("TAB|")).Select(l => l.Split('|')).ToList();
            Assert.Equal(tab.Select(t => t[1]).Order(), NtfsSecurityTab.Principals(host, path).Order());
            foreach (var t in tab)
            {
                var expected = t[2].Split(';', StringSplitOptions.RemoveEmptyEntries).Order().ToList();
                var actual = Marks(host, path, t[1]).Order().ToList();
                if (!expected.SequenceEqual(actual))
                    differences.Add($"{path} {t[1]}\n  Windows : {string.Join(";", expected)}\n  SMAR-X  : {string.Join(";", actual)}");
            }
        }
        Assert.True(differences.Count == 0, "L'Explorateur coche autrement :\n" + string.Join('\n', differences));
    }

    /// <summary>« Autorisations spéciales » est toujours grise dans l'onglet : seule sa présence se compare.</summary>
    private static IEnumerable<string> Marks(WindowsHost host, string path, string trustee)
    {
        foreach (var row in NtfsSecurityTab.Permissions(host, path, trustee))
            foreach (var (column, mark) in new[] { ("Allow", row.Allow), ("Deny", row.Deny) })
                if (mark != Mark.None)
                    yield return $"{column}:{NtfsSecurityTab.Name(row.Permission, "en-US")}="
                                 + (row.Permission == BasicPermission.Special ? "Inherited" : mark.ToString());
    }

    [Fact]
    public void AdvancedEntries_AreMergedLikeTheAdvancedWindow()
    {
        var host = Battery();
        var differences = new List<string>();
        foreach (var (path, lines) in Objects())
        {
            var folder = host.Find(path)!.IsDirectory;
            var expected = lines.Where(l => l.StartsWith("ADV|")).Select(l => l[4..]).Order().ToList();
            var actual = NtfsSecurityTab.Advanced(host, path).Select(e => string.Join('|', e.Trustee, e.Type,
                NtfsSecurityTab.Access(e, folder, "en-US"), NtfsSecurityTab.InheritedFrom(e, "en-US"), NtfsSecurityTab.AppliesTo(e, folder, "en-US"))).Order().ToList();
            foreach (var missing in expected.Except(actual)) differences.Add($"{path} Windows seulement : {missing}");
            foreach (var extra in actual.Except(expected)) differences.Add($"{path} SMAR-X seulement : {extra}");
        }
        Assert.True(differences.Count == 0, "La fenêtre Avancé montre autre chose :\n" + string.Join('\n', differences));
    }

    [Fact]
    public void Principals_FollowTheirSids_LikeTheExplorer()
    {
        var measured = Objects().First().Lines.Where(l => l.StartsWith("TAB|")).Select(l => l.Split('|')[1]);
        string[] wellKnown = [WellKnown.Everyone, WellKnown.CreatorOwner, WellKnown.OwnerRights, WellKnown.AuthenticatedUsers, WellKnown.System,
                              WellKnown.Network, Admins, WellKnown.Users, WellKnown.Interactive];
        Assert.Equal(measured.Where(wellKnown.Contains), NtfsSecurityTab.Principals(Battery(), @"D:\F1").Where(wellKnown.Contains));
    }

    /// <summary>Les cases cliquées sur Windows (lignes EDIT), rejouées : la même DACL, dans le même ordre.</summary>
    [Fact]
    public void EditWindowClicks_WriteTheDaclThatWindowsWrites()
    {
        var cases = Lines.Where(l => l.StartsWith("EDIT|")).Select(l => l.Split('|')).ToList();
        Assert.Equal(8, cases.Count);
        var differences = new List<string>();
        foreach (var c in cases)
        {
            var host = new WindowsHost { Name = "CLI" };
            host.Root = NtfsNode.Directory("D:", Admins).With(
                NtfsNode.Directory(c[1], Admins, NtfsAce.Allow(WellKnown.Users, "M"), NtfsAce.Allow(WellKnown.System, "F"), NtfsAce.Allow(Admins, "F")).Protected());
            var path = @"D:\" + c[1];
            foreach (var click in c[2].Split(','))
            {
                var (column, name) = (click.Split(':')[0], click.Split(':')[1]);
                var type = Enum.Parse<AceType>(column);
                var permission = NtfsSecurityTab.Rows(true).Single(p => NtfsSecurityTab.Name(p, "en-US") == name);
                var row = NtfsSecurityTab.Permissions(host, path, WellKnown.Users).Single(r => r.Permission == permission);
                var isChecked = (type == AceType.Allow ? row.Allow : row.Deny) != Mark.None;
                Assert.True(NtfsSecurityTab.Set(host, path, WellKnown.Users, permission, type, !isChecked));
            }
            var actual = string.Join(';', NtfsInheritance.Explicit(host.Find(path)!).Select(a =>
                $"{a.Type},{a.Trustee},{(a.Inheritance.HasFlag(ObjectInherit) ? "OI" : "")}{(a.Inheritance.HasFlag(ContainerInherit) ? "CI" : "")},{(uint)a.Mask:X8}"));
            if (actual != c[3])
                differences.Add($"{c[1]} ({c[2]})\n  Windows : {c[3]}\n  SMAR-X  : {actual}");
        }
        Assert.True(differences.Count == 0, "La fenêtre Modifier écrit autre chose :\n" + string.Join('\n', differences));
    }

    [Fact]
    public void EditWindow_WritesTheAceThatWindowsWrites()
    {
        var host = Battery();
        // Cocher Refuser « Écriture » sur Sub pour Guests, qui a Modification explicite : le refus omet Synchroniser,
        // et l'autorisation perd ses droits d'écriture
        Assert.True(NtfsSecurityTab.Set(host, @"D:\F1\Sub", "Guests", BasicPermission.Write, AceType.Deny, on: true));
        var sub = host.Find(@"D:\F1\Sub")!;
        Assert.Contains(sub.Aces, a => a.Type == AceType.Deny && a.Trustee == "Guests" && a.Mask == (NtfsRights.Write & ~NtfsRights.Synchronize));
        var guests = NtfsSecurityTab.Permissions(host, @"D:\F1\Sub", "Guests");
        Assert.Equal(Mark.Explicit, guests.Single(r => r.Permission == BasicPermission.Write).Deny);
        Assert.Equal(Mark.Inherited, guests.Single(r => r.Permission == BasicPermission.ListFolderContents).Allow);

        // Une coche seulement héritée ne se décoche pas ici
        Assert.False(NtfsSecurityTab.Set(host, @"D:\F1\Sub", WellKnown.Everyone, BasicPermission.Read, AceType.Allow, on: false));
        // Un compte qui hérite ne se supprime pas ; un compte seulement explicite, si
        Assert.False(NtfsSecurityTab.Remove(host, @"D:\F1\Sub", "Guests"));
        NtfsSecurityTab.Add(host, @"D:\F1\Sub", "SERVICE");
        Assert.Equal([Mark.None, Mark.None, Mark.Explicit, Mark.Explicit, Mark.Explicit, Mark.None, Mark.None],
            NtfsSecurityTab.Permissions(host, @"D:\F1\Sub", "SERVICE").Select(r => r.Allow));
        Assert.True(NtfsSecurityTab.Remove(host, @"D:\F1\Sub", "SERVICE"));
        Assert.DoesNotContain("SERVICE", NtfsSecurityTab.Principals(host, @"D:\F1\Sub"));
    }
}
