using SMARX.Models;
using SMARX.Samples;
using SMARX.Services.Windows;

namespace SMARX.Tests;

/// <summary>
/// Le moteur Windows donne les mêmes résultats qu'un vrai Windows Server. Fidelity/windows-file-server.txt a été produit
/// par tools/SmarxWindowsCheck sur un contrôleur de domaine Windows Server 2025 : le serveur d'exemple recréé avec icacls,
/// ses propriétaires (OWNER) et ses DACL telles qu'elles sont stockées (ACL), puis chaque objet ouvert avec chacun des
/// 14 droits spéciaux (NtOpenFile) sous le jeton de chaque compte, administratrice non élevée comprise (ACCESS).
/// </summary>
public class WindowsFidelityTests
{
    private static readonly string[] Lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fidelity", "windows-file-server.txt"));
    private readonly WindowsHost _host = WindowsSamples.FileServer();

    [Fact]
    public void EveryRightOnEveryObject_IsGrantedOrDenied_AsByWindows()
    {
        var accesses = Lines.Where(l => l.StartsWith("ACCESS|")).Select(l => l.Split('|')).ToList();
        Assert.True(accesses.Count >= 1500, $"{accesses.Count} accès seulement");
        Assert.DoesNotContain(accesses, a => a[5].StartsWith("err"));

        var differences = new List<string>();
        foreach (var a in accesses)
        {
            var (user, elevated, path, code) = (_host.User(a[1])!, bool.Parse(a[2]), a[3], a[4]);
            var right = Ntfs.Special.Single(s => s.Code == code).Right;
            var granted = WindowsAccess.Open(_host, WindowsToken.Local(_host, user, elevated), path, right) != OpenGrant.Denied;
            if (granted != (a[5] == "yes"))
                differences.Add(string.Join('|', a) + $" ; SMAR-X : {(granted ? "yes" : "no")}");
        }
        Assert.True(differences.Count == 0, "Windows dit autrement :\n" + string.Join('\n', differences));
    }

    [Fact]
    public void Owners_AreTheSample()
    {
        foreach (var o in Lines.Where(l => l.StartsWith("OWNER|")).Select(l => l.Split('|')))
            Assert.Equal(_host.Find(o[1])!.Owner, o[2]);
    }

    /// <summary>
    /// Les ACE de chaque objet, explicites et héritées, avec leur héritage et leurs droits, dans l'ordre de la DACL :
    /// celles que calcule le moteur. L'ordre compte : Windows ne recopie pas une ACE héritée en double, et laquelle il
    /// garde dépend de celle qu'il lit en premier.
    /// </summary>
    [Fact]
    public void InheritedAndExplicitAces_AreTheOnesWindowsPropagates()
    {
        var real = Lines.Where(l => l.StartsWith("ACL|")).Select(l => l.Split('|')).GroupBy(a => a[1]).ToList();
        Assert.Equal(18, real.Count);

        var differences = new List<string>();
        foreach (var node in real)
        {
            var expected = NtfsInheritance.Dacl(_host, node.Key).Select(a => Key(a.Type, a.Trustee, a.Inherited, a.Inheritance, a.Mask)).ToList();
            var actual = node.OrderBy(a => int.Parse(a[2])).Select(a =>
            {
                var flags = int.Parse(a[5]);
                return Key(Enum.Parse<AceType>(a[3]), a[4], (flags & 0x10) != 0, Inheritance(flags), Specific(Convert.ToUInt32(a[6], 16)));
            }).ToList();
            if (!expected.SequenceEqual(actual))
                differences.Add($"{node.Key}\n  Windows : {string.Join(" ; ", actual)}\n  SMAR-X  : {string.Join(" ; ", expected)}");

            // Ordre canonique : explicites avant héritées, refus avant autorisations parmi les explicites
            var order = node.Select(a => (Inherited: (int.Parse(a[5]) & 0x10) != 0, Deny: a[3] == "Deny")).ToList();
            Assert.True(order.SkipWhile(o => !o.Inherited).All(o => o.Inherited), $"{node.Key} : explicite après une héritée");
            Assert.True(order.Where(o => !o.Inherited).SkipWhile(o => o.Deny).All(o => !o.Deny), $"{node.Key} : refus explicite après une autorisation");
        }
        Assert.True(differences.Count == 0, "Windows propage autrement :\n" + string.Join('\n', differences));
    }

    private static string Key(AceType type, string trustee, bool inherited, AceInheritance inheritance, NtfsRights mask) =>
        $"{type} {trustee} {(inherited ? "(I)" : "")}{inheritance} {Ntfs.Format(mask)}";

    /// <summary>Drapeaux d'une ACE Windows : OI 1, CI 2, NP 4, IO 8 (et 0x10 : héritée).</summary>
    private static AceInheritance Inheritance(int flags) =>
        ((flags & 1) != 0 ? AceInheritance.ObjectInherit : 0) | ((flags & 2) != 0 ? AceInheritance.ContainerInherit : 0)
        | ((flags & 4) != 0 ? AceInheritance.NoPropagate : 0) | ((flags & 8) != 0 ? AceInheritance.InheritOnly : 0);

    /// <summary>Droits génériques (une ACE « héritage seulement » peut les garder) traduits en droits de fichier, comme Windows à l'héritage.</summary>
    private static NtfsRights Specific(uint mask)
    {
        var rights = (NtfsRights)(mask & 0x0FFFFFFF);
        if ((mask & 0x10000000) != 0) rights |= NtfsRights.FullControl;
        if ((mask & 0x80000000) != 0) rights |= NtfsRights.Read;
        if ((mask & 0x40000000) != 0) rights |= NtfsRights.Write | NtfsRights.ReadPermissions;
        if ((mask & 0x20000000) != 0) rights |= NtfsRights.ExecuteFile | NtfsRights.ReadAttributes | NtfsRights.ReadPermissions | NtfsRights.Synchronize;
        return rights;
    }
}
