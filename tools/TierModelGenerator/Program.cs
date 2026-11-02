// Génère un modèle de tiering de SMAD-X (SMAD-X.Core/TierModels/<id>.json) à partir de la source officielle du
// modèle, pour une version publiée. Mettre un modèle à jour : relancer avec la nouvelle version, relire le diff et
// le rapport des droits non reconnus, puis lancer les tests.
//
//   dotnet run --project tools/TierModelGenerator -- microsoft v2.1.0 SMAD-X.Core/TierModels
//   dotnet run --project tools/TierModelGenerator -- hardenad HardenAD-2.9.9v2 SMAD-X.Core/TierModels
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SMADX.Models;
using SMADX.Services;

if (args.Length < 3)
{
    Console.Error.WriteLine("Usage : TierModelGenerator <microsoft|hardenad> <version> <dossier de sortie>");
    return 1;
}

var (id, version, outDir) = (args[0].ToLowerInvariant(), args[1], args[2]);
using var http = new HttpClient();
http.DefaultRequestHeaders.UserAgent.ParseAdd("SMAD-X-TierModelGenerator");
var unmapped = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

TierModelDefinition model;
switch (id)
{
    case "microsoft": model = await MicrosoftTierModel.Generate(http, version, unmapped); break;
    case "hardenad": model = await HardenAdTierModel.Generate(http, version, unmapped); break;
    default:
        Console.Error.WriteLine($"Modèle inconnu : {id} (microsoft ou hardenad)");
        return 1;
}

// AD ignore la casse des DN, la source aussi parfois (« OU=contacts ») : chaque chemin reprend celui de son OU
var ouPaths = model.OrganizationalUnits.ToDictionary(o => o.Path, o => o.Path, StringComparer.OrdinalIgnoreCase);
string Canonical(string p) => ouPaths.GetValueOrDefault(p, p);
foreach (var d in model.Delegations) d.Target = Canonical(d.Target);
foreach (var link in model.GroupPolicies.SelectMany(g => g.Links)) link.Target = Canonical(link.Target);
foreach (var g in model.Groups) g.Path = Canonical(g.Path);
foreach (var a in model.Accounts) a.Path = Canonical(a.Path);
model.Delegations = Rights.Dedupe(model.Delegations);

Directory.CreateDirectory(outDir);
var path = Path.Combine(outDir, $"{id}.json");
File.WriteAllText(path, JsonSerializer.Serialize(model, ADDataService.SerializerOptions) + "\n");

Console.WriteLine($"{model.Name} {model.Source.Version} ({model.Source.Commit[..7]}) -> {path}");
Console.WriteLine($"  {model.OrganizationalUnits.Count} OU, {model.Groups.Count} groupes, {model.Accounts.Count} comptes, " +
                  $"{model.Delegations.Count} délégations, {model.GroupPolicies.Count} GPO, {model.DefaultMembers.Count} ajouts à des groupes existants");
if (unmapped.Count > 0)
{
    Console.WriteLine("  Droits non reconnus, rangés en « Other » ou en écriture d'attribut : à vérifier");
    foreach (var item in unmapped) Console.WriteLine($"    - {item}");
}
return 0;

/// <summary>Note bilingue sur ce que SMAD-X ne reproduit pas.</summary>
static class Text
{
    public static TierModelNote Note(string fr, string en) => new() { Fr = fr, En = en };
}

/// <summary>Téléchargement des fichiers d'une source, figée sur le commit de la version demandée.</summary>
static class Source
{
    public static async Task<string> Commit(HttpClient http, string repository, string reference)
    {
        using var doc = JsonDocument.Parse(await http.GetStringAsync($"https://api.github.com/repos/{repository}/commits/{reference}"));
        return doc.RootElement.GetProperty("sha").GetString()!;
    }

    /// <summary>Fichier de la source ; l'encodage suit sa marque d'ordre des octets (les rapports de GPO sont en UTF-16).</summary>
    public static async Task<string> File(HttpClient http, string repository, string commit, string path)
    {
        var bytes = await http.GetByteArrayAsync($"https://raw.githubusercontent.com/{repository}/{commit}/{string.Join('/', path.Split('/').Select(Uri.EscapeDataString))}");
        using var reader = new StreamReader(new MemoryStream(bytes), System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync();
    }

    /// <summary>Chemins de tous les fichiers du dépôt au commit donné.</summary>
    public static async Task<List<string>> Tree(HttpClient http, string repository, string commit)
    {
        using var doc = JsonDocument.Parse(await http.GetStringAsync($"https://api.github.com/repos/{repository}/git/trees/{commit}?recursive=1"));
        if (doc.RootElement.GetProperty("truncated").GetBoolean())
            throw new InvalidOperationException($"Arborescence de {repository} tronquée par l'API GitHub");
        return doc.RootElement.GetProperty("tree").EnumerateArray()
            .Where(e => e.GetProperty("type").GetString() == "blob").Select(e => e.GetProperty("path").GetString()!).ToList();
    }

    /// <summary>« OU=B,OU=A,DC=x » ou « OU=B,OU=A,{{DOMAIN_DN}} » -> « A/B ». Null si le DN n'est pas une OU.</summary>
    public static string? OuPath(string dn)
    {
        var parts = dn.Split(',').Select(p => p.Trim())
            .Where(p => p.Length > 0 && !p.StartsWith("DC=", StringComparison.OrdinalIgnoreCase)
                        && !Regex.IsMatch(p, @"^(\{\{DOMAIN_DN\}\}|ROOTDN|RootDN)$", RegexOptions.IgnoreCase))
            .ToList();
        if (parts.Any(p => !p.StartsWith("OU=", StringComparison.OrdinalIgnoreCase))) return null;
        return string.Join('/', parts.Select(p => p[3..]).Reverse());
    }
}

/// <summary>Traduction des droits d'une ACE en catégories de SMAD-X, communes aux deux modèles.</summary>
static class Rights
{
    private static readonly HashSet<string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        "GenericAll", "GenericWrite", "WriteDacl", "WriteOwner", "CreateChild", "DeleteChild", "ExtendedRight",
        "AllExtendedRights", "WriteProperty", "ReadProperty", "Delete", "DeleteTree", "GenericRead", "ListChildren"
    };

    public static IEnumerable<(RightCategory Category, string Right)> Map(
        IReadOnlyCollection<string> rights, string? objectType, string? inheritedObject, ISet<string> unmapped)
    {
        var type = (objectType ?? string.Empty).Trim();
        var inherited = (inheritedObject ?? string.Empty).Trim();
        bool Has(string right) => rights.Contains(right, StringComparer.OrdinalIgnoreCase);
        bool TypeIs(params string[] names) => names.Any(n => type.Equals(n, StringComparison.OrdinalIgnoreCase));
        var bitLocker = TypeIs("BitLockerKeyPackage", "BitLockerRecoveryPassword", "msFVE-RecoveryInformation")
                        || inherited.Equals("msFVE-RecoveryInformation", StringComparison.OrdinalIgnoreCase);
        var laps = TypeIs("ms-Mcs-AdmPwd", "msLAPS-Password", "msLAPS-EncryptedPassword");
        var objectClass = (type.Length > 0 ? type : inherited).ToLowerInvariant();

        foreach (var right in rights.Where(r => !Known.Contains(r)))
            unmapped.Add($"{right} ({type})");

        // Contrôle total : limité à une classe d'objets quand seule elle en hérite, comme l'import le lit dans l'ACE
        if (Has("GenericAll"))
            yield return bitLocker ? (RightCategory.Other, "GenericAll:msFVE-RecoveryInformation")
                : objectClass == "computer" ? (RightCategory.ComputerManagement, "GenericAll:computer")
                // Toutes les classes, les comptes et les groupes, ou les OU et conteneurs qui les contiennent
                : objectClass is "" or "allobjectclasses" or "user" or "group" or "inetorgperson" or "msds-groupmanagedserviceaccount"
                    or "msds-managedserviceaccount" or "organizationalunit" or "container"
                    ? (RightCategory.FullControl, "GenericAll")
                    : (RightCategory.Other, $"GenericAll:{(type.Length > 0 ? type : inherited)}");
        if (Has("GenericWrite")) yield return (RightCategory.GenericWrite, "GenericWrite");
        if (Has("WriteDacl")) yield return (RightCategory.FullControl, "WriteDacl");
        if (Has("WriteOwner")) yield return (RightCategory.FullControl, "WriteOwner");
        if (Has("CreateChild") || Has("DeleteChild"))
            yield return objectClass == "computer"
                ? (RightCategory.ComputerManagement, "CreateChild:computer")
                : (RightCategory.Other, $"CreateChild:{(objectClass.Length > 0 ? objectClass : "all")}");
        if (Has("ExtendedRight") || Has("AllExtendedRights"))
        {
            if (TypeIs("PasswordReset", "User-Force-Change-Password")) yield return (RightCategory.PasswordReset, "ResetPassword");
            // Écritures validées : le compte ne peut y écrire que ses propres noms DNS
            else if (TypeIs("WriteSPN", "DnsHostname", "Validated-SPN", "Validated-DNS-Host-Name")) yield return (RightCategory.Other, $"Self:{type}");
            else if (type.Length == 0 || Has("AllExtendedRights")) yield return (RightCategory.AllExtendedRights, "AllExtendedRights");
            else { unmapped.Add($"ExtendedRight:{type}"); yield return (RightCategory.Other, $"ExtendedRight:{type}"); }
        }
        if (Has("WriteProperty"))
        {
            if (TypeIs("member")) yield return (RightCategory.WriteMembers, "WriteProperty:member");
            else if (TypeIs("LockoutTime")) yield return (RightCategory.AccountUnlock, "WriteProperty:lockoutTime");
            else if (TypeIs("LogonScript", "scriptPath")) yield return (RightCategory.AttributeWrite, "WriteProperty:scriptPath");
            else if (TypeIs("UserAccountOption", "userAccountControl")) yield return (RightCategory.AttributeWrite, "WriteProperty:userAccountControl");
            else if (TypeIs("servicePrincipalName")) yield return (RightCategory.WriteSpn, "WriteProperty:servicePrincipalName");
            else if (TypeIs("ms-Mcs-AdmPwdExpirationTime", "msLAPS-PasswordExpirationTime")) yield return (RightCategory.Other, $"WriteProperty:{type}");
            // Sans attribut : toutes les propriétés des objets de cette classe
            else if (TypeIs("", "User", "Computer", "Group", "Contact")) yield return (RightCategory.GenericWrite, "WriteProperty");
            else { unmapped.Add($"WriteProperty:{type}"); yield return (RightCategory.AttributeWrite, $"WriteProperty:{type}"); }
        }
        if (Has("ReadProperty") && !Has("WriteProperty"))
        {
            if (laps) yield return (RightCategory.ReadLapsPassword, "ReadProperty:msLAPS-Password");
            else if (bitLocker) yield return (RightCategory.Other, "ReadProperty:msFVE-RecoveryPassword");
            // Une lecture ordinaire ne donne aucun contrôle : rien à représenter
        }
        if (Has("Delete") || Has("DeleteTree")) yield return (RightCategory.Other, "Delete");
    }

    public static string Gpo(string name) =>
        name.Contains("Account Restrictions", StringComparison.OrdinalIgnoreCase) || name.Contains("LoginRestrictions", StringComparison.OrdinalIgnoreCase) ? nameof(TierModelGpoRole.LogonRestrictions)
        : name.Contains("LocalAdmins", StringComparison.OrdinalIgnoreCase) ? nameof(TierModelGpoRole.LocalAdministrators)
        : name.Contains("LocalRDU", StringComparison.OrdinalIgnoreCase) ? nameof(TierModelGpoRole.RemoteDesktopUsers)
        : name.Contains("LAPS", StringComparison.OrdinalIgnoreCase) ? nameof(TierModelGpoRole.Laps)
        : name.Contains("Authentication Silo", StringComparison.OrdinalIgnoreCase) ? nameof(TierModelGpoRole.AuthenticationSilo)
        : nameof(TierModelGpoRole.Baseline);

    public static List<TierModelDelegation> Dedupe(IEnumerable<TierModelDelegation> delegations) =>
        delegations.GroupBy(d => (d.Trustee.ToLowerInvariant(), d.Target.ToLowerInvariant(), d.Right.ToLowerInvariant()))
            .Select(g => g.First()).ToList();
}

static class Tiers
{
    public static List<TierModelTier> Standard() =>
    [
        new() { Name = "Tier 0", Level = 0, Color = "#C0392B", Description = "Administration et contrôle de domaine" },
        new() { Name = "Tier 1", Level = 1, Color = "#E67E22", Description = "Serveurs et infrastructure" },
        new() { Name = "Tier 2", Level = 2, Color = "#27AE60", Description = "Postes de travail utilisateurs" }
    ];
}

/// <summary>Microsoft Active Directory Tier Model : configuration déclarative en JSON (dossier config/).</summary>
static class MicrosoftTierModel
{
    private const string Repository = "microsoft/ActiveDirectoryTierModel";
    private static readonly string[] Files =
        ["config/tiermodel-ous.json", "config/tiermodel-groups.json", "config/tiermodel-users.json", "config/tiermodel-acls.json", "config/tiermodel-gpos.json", "LICENSE"];

    public static async Task<TierModelDefinition> Generate(HttpClient http, string version, ISet<string> unmapped)
    {
        var commit = await Source.Commit(http, Repository, version);
        async Task<JsonElement> Json(string file) => JsonDocument.Parse(await Source.File(http, Repository, commit, file)).RootElement;

        var model = new TierModelDefinition
        {
            Id = "microsoft",
            Name = "Microsoft Active Directory Tier Model",
            Source = new TierModelSource
            {
                Repository = $"https://github.com/{Repository}",
                Version = version,
                Commit = commit,
                License = "MIT",
                Copyright = Regex.Match(await Source.File(http, Repository, commit, "LICENSE"), @"Copyright \(c\)[^\r\n]*").Value.Trim(),
                Files = Files.ToList(),
                Notes =
                [
                    Text.Note("Composants optionnels non repris : MSA, gMSA et dMSA (-IncludeMsa…), Windows LAPS (-IncludeWinLaps), silos d'authentification (-IncludeAuthSilos).",
                         "Optional components not included: MSA, gMSA and dMSA (-IncludeMsa…), Windows LAPS (-IncludeWinLaps), authentication silos (-IncludeAuthSilos)."),
                    Text.Note("Le préfixe « *- » des noms de GPO est retiré.", "The \"*- \" prefix of GPO names is removed."),
                    Text.Note("Des paramètres des GPO, SMAD-X reprend les refus d'ouverture de session (session locale, Bureau à distance) et les membres du groupe local Administrateurs (groupes restreints) ; les autres paramètres ne sont pas modélisés.",
                         "Of the GPO settings, SMAD-X includes the logon denials (local session, Remote Desktop) and the members of the local Administrators group (restricted groups); other settings are not modelled."),
                    Text.Note("Les liens de GPO que le modèle crée désactivés restent désactivés.", "GPO links the model creates disabled stay disabled.")
                ]
            },
            Tiers = Tiers.Standard()
        };

        foreach (var ou in (await Json("config/tiermodel-ous.json")).GetProperty("organizationUnits").EnumerateArray())
        {
            var parent = Source.OuPath(ou.GetProperty("path").GetString()!) ?? string.Empty;
            var name = ou.GetProperty("name").GetString()!;
            var path = parent.Length == 0 ? name : $"{parent}/{name}";
            model.OrganizationalUnits.Add(new TierModelOu
            {
                Path = path, Tier = TierOf(path), Role = RoleOf(path),
                Description = ou.TryGetProperty("comment", out var c) ? c.GetString() ?? string.Empty : string.Empty
            });
        }

        foreach (var g in (await Json("config/tiermodel-groups.json")).GetProperty("groups").EnumerateArray())
        {
            var path = Source.OuPath(g.GetProperty("path").GetString()!)!;
            model.Groups.Add(new TierModelGroup
            {
                Name = g.GetProperty("samaccountname").GetString()!,
                Path = path,
                Tier = TierOf(path),
                Scope = Enum.Parse<ADGroupScope>(g.GetProperty("groupscope").GetString()!, ignoreCase: true),
                Description = g.GetProperty("description").GetString() ?? string.Empty
            });
        }

        foreach (var u in (await Json("config/tiermodel-users.json")).GetProperty("users").EnumerateArray())
        {
            var path = Source.OuPath(u.GetProperty("ouPath").GetString()!)!;
            model.Accounts.Add(new TierModelAccount
            {
                Name = u.GetProperty("samAccountName").GetString()!,
                Path = path,
                Tier = TierOf(path),
                Enabled = u.TryGetProperty("enabled", out var e) && e.GetBoolean(),
                Description = u.GetProperty("description").GetString() ?? string.Empty,
                MemberOf = u.TryGetProperty("memberOf", out var m) ? m.EnumerateArray().Select(x => x.GetString()!).ToList() : []
            });
        }

        var delegations = new List<TierModelDelegation>();
        foreach (var a in (await Json("config/tiermodel-acls.json")).GetProperty("aclDelegations").EnumerateArray())
        {
            if (a.TryGetProperty("accesscontroltype", out var type) && !string.Equals(type.GetString(), "Allow", StringComparison.OrdinalIgnoreCase)) continue;
            var rights = a.GetProperty("activedirectoryrights").EnumerateArray().Select(r => r.GetString()!).ToList();
            var objectType = a.TryGetProperty("objecttype", out var o) ? o.GetString() : null;
            var target = Source.OuPath(a.GetProperty("targetOUPath").GetString()!)!;
            foreach (var (category, right) in Rights.Map(rights, objectType, null, unmapped))
                delegations.Add(new TierModelDelegation
                {
                    Trustee = a.GetProperty("identityreference").GetString()!, Target = target, Right = right, Category = category,
                    SourceRights = $"{string.Join('/', rights)} {objectType}".Trim()
                });
        }
        model.Delegations = Rights.Dedupe(delegations);

        var gpos = new Dictionary<string, TierModelGpo>(StringComparer.OrdinalIgnoreCase);
        foreach (var target in (await Json("config/tiermodel-gpos.json")).GetProperty("gpos").EnumerateObject().Where(t => t.Name != "TemplateGpos"))
        {
            var link = Source.OuPath(target.Name) ?? string.Empty;
            foreach (var entry in Entries(target.Value))
            {
                var name = Regex.Replace(entry.GetProperty("name").GetString()!, @"^\*-\s*", string.Empty);
                if (!gpos.TryGetValue(name, out var gpo))
                    gpos[name] = gpo = new TierModelGpo
                    {
                        Name = name, Role = Enum.Parse<TierModelGpoRole>(Rights.Gpo(name)),
                        Tier = Regex.Match(name, @"Tier ([0-2])") is { Success: true } t ? $"Tier {t.Groups[1].Value}" : TierOf(link)
                    };
                gpo.Links.Add(new TierModelGpoLink
                {
                    Target = link, Enabled = !entry.TryGetProperty("linkEnabled", out var le) || le.GetBoolean()
                });
                gpo.DenyInteractiveLogon ??= Right(entry, "SeDenyInteractiveLogonRight");
                gpo.DenyRemoteInteractiveLogon ??= Right(entry, "SeDenyRemoteInteractiveLogonRight");
                if (gpo.LocalAdmins == null && entry.TryGetProperty("restrictedGroups", out var restricted) && Administrators(restricted) is { } admins)
                    (gpo.LocalAdmins, gpo.ReplaceLocalAdmins) = (admins, true);
            }
        }
        model.GroupPolicies = gpos.Values.ToList();
        return model;
    }

    /// <summary>Mandataires d'un droit utilisateur que la GPO définit ; null si elle ne le définit pas.</summary>
    private static List<string>? Right(JsonElement entry, string right) =>
        entry.TryGetProperty("userRightsAssignments", out var rights)
            ? rights.EnumerateArray().Where(r => r.GetProperty("right").GetString() == right)
                .Select(r => Names(r.GetProperty("principals")).ToList()).FirstOrDefault()
            : null;

    /// <summary>Noms des mandataires d'un droit : groupes du domaine, de la racine de la forêt, conditionnels, littéraux.</summary>
    private static IEnumerable<string> Names(JsonElement principals)
    {
        foreach (var key in new[] { "resolvableGroups", "forestRootOnly", "literalStrings" })
            if (principals.TryGetProperty(key, out var list))
                foreach (var name in list.EnumerateArray()) yield return name.GetString()!;
        if (principals.TryGetProperty("conditionalGroups", out var conditional))
            foreach (var group in conditional.EnumerateArray())
                foreach (var name in group.GetProperty("names").EnumerateArray()) yield return name.GetString()!;
    }

    /// <summary>Groupes restreints : membres du groupe local Administrateurs (S-1-5-32-544), qui remplacent ceux de la machine.</summary>
    private static List<string>? Administrators(JsonElement restricted)
    {
        if (restricted.ValueKind != JsonValueKind.Object) return null;
        if (restricted.TryGetProperty("membershipGroups", out var groups))
            foreach (var group in groups.EnumerateArray())
                if (group.GetProperty("groupSidOrName").GetString() == "*S-1-5-32-544__Members")
                    return group.GetProperty("memberGroups").EnumerateArray().Select(m => m.GetString()!).ToList();
        return restricted.TryGetProperty("emptyGroups", out var empty) && empty.EnumerateArray().Any(e => e.GetString() == "*S-1-5-32-544__Members")
            ? [] : null;
    }

    /// <summary>Les entrées de GPO d'une cible : objets portant un nom et un mode de création.</summary>
    private static IEnumerable<JsonElement> Entries(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            if (node.TryGetProperty("name", out _) && node.TryGetProperty("mode", out _)) { yield return node; yield break; }
            foreach (var p in node.EnumerateObject())
                foreach (var e in Entries(p.Value)) yield return e;
        }
        else if (node.ValueKind == JsonValueKind.Array)
            foreach (var item in node.EnumerateArray())
                foreach (var e in Entries(item)) yield return e;
    }

    /// <summary>Tier d'une OU : le plus proche « Tier N » de son chemin ; aucun pour PAW Staging ou la quarantaine.</summary>
    private static string? TierOf(string path) =>
        path.Split('/').Reverse().Select(s => Regex.Match(s, @"Tier ([0-2])")).FirstOrDefault(m => m.Success) is { } m ? $"Tier {m.Groups[1].Value}" : null;

    private static TierModelOuRole RoleOf(string path)
    {
        var name = path.Split('/')[^1];
        var admin = path.StartsWith("Tier Model Administration/", StringComparison.OrdinalIgnoreCase);
        bool Has(string s) => name.Contains(s, StringComparison.OrdinalIgnoreCase);
        if (Has("Staging")) return TierModelOuRole.Staging;
        if (Has("Quarantine")) return TierModelOuRole.Quarantine;
        if (name.StartsWith("Disabled", StringComparison.OrdinalIgnoreCase)) return TierModelOuRole.Disabled;
        if (Has("PAW Devices")) return TierModelOuRole.AdminWorkstations;
        if (Has("Service Accounts")) return TierModelOuRole.ServiceAccounts;
        if (admin && name.EndsWith("Accounts", StringComparison.OrdinalIgnoreCase)) return TierModelOuRole.AdminAccounts;
        if (admin && name.EndsWith("Groups", StringComparison.OrdinalIgnoreCase)) return TierModelOuRole.AdminGroups;
        if (Has("Member Servers")) return TierModelOuRole.Servers;
        if (Has("End-User Devices")) return TierModelOuRole.Workstations;
        if (Has("End-User Accounts")) return TierModelOuRole.Users;
        if (Has("Groups")) return TierModelOuRole.Groups;
        if (Has("Contacts")) return TierModelOuRole.Contacts;
        return TierModelOuRole.Container;
    }
}

/// <summary>HardenAD : séquence de tâches en XML (Configs/TasksSequence_HardenAD.xml), toutes tâches comprises.</summary>
static class HardenAdTierModel
{
    private const string Repository = "LoicVeirman/HardenAD";
    private const string Sequence = "Configs/TasksSequence_HardenAD.xml";

    private static readonly Dictionary<string, string> WellKnownSids = new(StringComparer.OrdinalIgnoreCase)
    {
        ["S-1-5-32-544"] = "Administrators", ["-500"] = "Administrator", ["-512"] = "Domain Admins",
        ["-519"] = "Enterprise Admins", ["-525"] = "Protected Users"
    };

    public static async Task<TierModelDefinition> Generate(HttpClient http, string version, ISet<string> unmapped)
    {
        var commit = await Source.Commit(http, Repository, version);
        var xml = XDocument.Parse(await Source.File(http, Repository, commit, Sequence)).Root!;
        var license = await Source.File(http, Repository, commit, "LICENSE");

        var translations = xml.Element("Translation")!.Elements()
            .Where(w => w.Attribute("translateFrom") != null)
            .ToDictionary(w => w.Attribute("translateFrom")!.Value, w => w.Attribute("translateTo")?.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        string T(string? s)
        {
            s ??= string.Empty;
            for (var i = 0; i < 3; i++) s = Regex.Replace(s, "%[^%]+%", m => translations.GetValueOrDefault(m.Value, m.Value));
            return s;
        }

        var model = new TierModelDefinition
        {
            Id = "hardenad",
            Name = "HardenAD",
            Source = new TierModelSource
            {
                Repository = $"https://github.com/{Repository}",
                Version = version,
                Commit = commit,
                License = "MIT",
                Copyright = Regex.Match(license, @"Copyright \(c\)[^\r\n]*").Value.Trim(),
                Files = [Sequence, "Inputs/GroupPolicies/*/{BackupID}/gpreport.xml", "Inputs/GroupPolicies/*/{BackupID}/*.migtable", "Inputs/GroupPolicies/*/{BackupID}/translation.xml", "LICENSE"],
                Notes =
                [
                    Text.Note("Toutes les tâches de la séquence sont reprises, comme si elles étaient toutes activées (elles sont désactivées par défaut dans la source).",
                         "Every task of the sequence is included, as if all were enabled (they are disabled by default in the source)."),
                    Text.Note("Les comptes d'administration gardent les noms de la source (T0M-AHARDEN…).", "Administration accounts keep the names of the source (T0M-AHARDEN…)."),
                    Text.Note("Des paramètres des GPO, SMAD-X reprend les refus d'ouverture de session (session locale, Bureau à distance), les membres ajoutés au groupe local Administrateurs et le filtre WMI (serveurs ou postes) ; les autres paramètres ne sont pas modélisés.",
                         "Of the GPO settings, SMAD-X includes the logon denials (local session, Remote Desktop), the members added to the local Administrators group and the WMI filter (servers or workstations); other settings are not modelled."),
                    Text.Note("Le groupe d'administration locale propre à chaque ordinateur (L-S_LocalAdmins_%ComputerName%) n'est pas créé.",
                         "The local administration group specific to each computer (L-S_LocalAdmins_%ComputerName%) is not created."),
                    Text.Note("La redirection des conteneurs par défaut vers Provisioning (redircmp, redirusr) n'est pas modélisée.",
                         "Redirecting the default containers to Provisioning (redircmp, redirusr) is not modelled."),
                    Text.Note("Les PAW partagées (PawAccess, PawT12L) et leurs groupes de connexion n'ont pas de tier : ils servent plusieurs tiers.",
                         "Shared PAWs (PawAccess, PawT12L) and their logon groups have no tier: they serve several tiers.")
                ]
            },
            Tiers = Tiers.Standard()
        };
        model.Tiers.Add(new TierModelTier { Name = "Tier 1 Legacy", Level = 1, Color = "#D35400", Description = "Serveurs aux systèmes anciens, isolés" });
        model.Tiers.Add(new TierModelTier { Name = "Tier 2 Legacy", Level = 2, Color = "#16A085", Description = "Postes aux systèmes anciens, isolés" });

        void Walk(XElement node, string parent)
        {
            foreach (var ou in node.Elements().Where(e => e.Name.LocalName is "OU" or "ChildOU"))
            {
                var path = parent.Length == 0 ? T(ou.Attribute("Name")?.Value) : $"{parent}/{T(ou.Attribute("Name")?.Value)}";
                model.OrganizationalUnits.Add(new TierModelOu
                {
                    Path = path, Tier = TierOf(path), Role = RoleOf(path), Description = T(ou.Attribute("Description")?.Value)
                });
                Walk(ou, path);
            }
        }
        Walk(xml.Element("OrganizationalUnits")!.Element("ouTree")!, string.Empty);

        // Appartenances : la source liste les membres de chaque groupe ; SMAD-X les range côté membre (MemberOf)
        var memberOf = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in xml.Element("Groups")!.Elements("Group"))
        {
            var name = T(g.Attribute("name")?.Value);
            var members = g.Elements("Member").Select(m => T(m.Attribute("samAccountName")?.Value)).Where(m => m.Length > 0).ToList();
            var path = Source.OuPath(T(g.Attribute("Path")?.Value));
            if (path == null)
            {
                // Groupe intégré (CN=Users) : le modèle ne le crée pas, il y ajoute des membres
                model.DefaultMembers.Add(new TierModelMembership { Group = name, Members = members });
                continue;
            }
            foreach (var member in members)
                (memberOf.TryGetValue(member, out var list) ? list : memberOf[member] = []).Add(name);
            model.Groups.Add(new TierModelGroup
            {
                Name = name, Path = path, Tier = GroupTier(name, path),
                Scope = Enum.Parse<ADGroupScope>(g.Attribute("Scope")!.Value, ignoreCase: true),
                Description = T(g.Attribute("Description")?.Value)
            });
        }
        foreach (var group in model.Groups) group.MemberOf = memberOf.GetValueOrDefault(group.Name, []);

        foreach (var u in xml.Element("Accounts")!.Elements("User"))
        {
            var name = T(u.Attribute("samAccountName")?.Value);
            var path = Source.OuPath(T(u.Attribute("Path")?.Value))!;
            model.Accounts.Add(new TierModelAccount
            {
                Name = name, Path = path, Tier = TierOf(path), Enabled = true,
                Description = T(u.Attribute("Description")?.Value), MemberOf = memberOf.GetValueOrDefault(name, [])
            });
        }

        foreach (var g in xml.Element("DefaultMembers")!.Elements("Group"))
        {
            var target = Sid(T(g.Attribute("Target")?.Value));
            var members = g.Elements().Select(m => Sid(T(m.Value.Length > 0 ? m.Value : m.Attribute("samAccountName")?.Value))).Where(m => m.Length > 0).ToList();
            if (model.DefaultMembers.FirstOrDefault(d => d.Group.Equals(target, StringComparison.OrdinalIgnoreCase)) is { } existing)
                existing.Members = existing.Members.Union(members, StringComparer.OrdinalIgnoreCase).ToList();
            else
                model.DefaultMembers.Add(new TierModelMembership { Group = target, Members = members });
        }

        var delegations = new List<TierModelDelegation>();
        foreach (var acl in xml.Element("DelegationACEs")!.Elements("ACL"))
        {
            if (string.Equals(acl.Attribute("Audit")?.Value, "True", StringComparison.OrdinalIgnoreCase)) continue; // SACL, pas une permission
            var trustee = T(acl.Attribute("Trustee")?.Value);
            var target = Source.OuPath(T(acl.Attribute("TargetDN")?.Value));
            if (target == null) continue;
            if (acl.Attribute("CustomAccessRule")?.Value == "Computer_DomJoin")
            {
                delegations.Add(new TierModelDelegation
                {
                    Trustee = trustee, Target = target, Right = "CreateChild:computer", Category = RightCategory.ComputerManagement,
                    SourceRights = "Computer_DomJoin"
                });
                continue;
            }
            var rights = (acl.Attribute("Right")?.Value ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var objectType = acl.Attribute("ObjectType")?.Value;
            var inherited = acl.Attribute("InheritedObjects")?.Value;
            foreach (var (category, right) in Rights.Map(rights, objectType, inherited, unmapped))
                delegations.Add(new TierModelDelegation
                {
                    Trustee = trustee, Target = target, Right = right, Category = category,
                    SourceRights = $"{string.Join('/', rights)} {objectType} {inherited}".Trim()
                });
        }
        // LAPS : la lecture du mot de passe ; sa réinitialisation écrit seulement la date d'expiration, sans le lire
        foreach (var laps in xml.Element("LocalAdminPasswordSolution")?.Elements() ?? [])
        {
            var kind = laps.Name.LocalName;
            var reader = kind.Equals("AdmPwdPasswordReader", StringComparison.OrdinalIgnoreCase);
            if (!reader && !kind.Equals("AdmPwdPasswordReset", StringComparison.OrdinalIgnoreCase)) continue;
            if (Source.OuPath(T(laps.Attribute("Target")?.Value)) is not { } target) continue;
            var trustee = Regex.Replace(T(laps.Attribute("Id")?.Value), @"^[^\\]*\\", string.Empty);
            delegations.Add(reader
                ? new TierModelDelegation { Trustee = trustee, Target = target, Right = "ReadProperty:ms-Mcs-AdmPwd", Category = RightCategory.ReadLapsPassword, SourceRights = kind }
                : new TierModelDelegation { Trustee = trustee, Target = target, Right = "WriteProperty:ms-Mcs-AdmPwdExpirationTime", Category = RightCategory.AttributeWrite, SourceRights = kind });
        }
        model.Delegations = Rights.Dedupe(delegations);

        var tree = await Source.Tree(http, Repository, commit);
        var settings = xml.Element("GroupPolicies")!.Element("GlobalGpoSettings")!;
        // groupPolicy.psm1 range les groupes de filtrage dans l'OU de GpoTier0, quel que soit le tier de la GPO
        var gpoGroupsPath = Source.OuPath(T(settings.Element("GpoTier0")!.Attribute("OU")!.Value))!;
        var keywords = xml.Element("Translation")!.Elements("Keyword")
            .Select(k => (k.Attribute("LongName")!.Value, k.Attribute("ShortenName")!.Value)).ToList();
        foreach (var gpo in xml.Element("GroupPolicies")!.Elements("GPO"))
        {
            var name = T(gpo.Attribute("Name")?.Value);

            // Filtrage de sécurité : un groupe pour appliquer la GPO, un autre pour l'en exclure (mode BOTH par défaut).
            // Le nom suit groupPolicy.psm1 : gabarit, traductions, abréviations du dictionnaire, majuscule de chaque mot.
            var mode = gpo.Element("GpoMode");
            var tierKey = mode?.Attribute("Tier")?.Value ?? "Tier0";
            var kinds = (mode?.Attribute("Mode")?.Value ?? "BOTH").ToUpperInvariant() switch
            {
                "APPLY" => new[] { "APPLY" },
                "DENY" => new[] { "DENY" },
                _ => new[] { "APPLY", "DENY" }
            };
            var groupName = settings.Attribute("GroupName")!.Value
                .Replace("%tier%", settings.Attributes().FirstOrDefault(a => a.Name.LocalName.Equals(tierKey, StringComparison.OrdinalIgnoreCase))?.Value ?? "T0", StringComparison.OrdinalIgnoreCase)
                .Replace("%GpoName%", name, StringComparison.OrdinalIgnoreCase);
            groupName = T(groupName.Replace("%mode%", "\u0001")).Replace("\u0001", "%mode%");
            foreach (var (longName, shortName) in keywords)
                groupName = Regex.Replace(groupName, longName, shortName.Replace("$", "$$"), RegexOptions.IgnoreCase);
            groupName = string.Concat(groupName.Split(' ').Where(w => w.Length > 0).Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
            foreach (var kind in kinds)
                model.Groups.Add(new TierModelGroup
                {
                    Name = groupName.Replace("%mode%", kind, StringComparison.OrdinalIgnoreCase), Path = gpoGroupsPath, Tier = TierOf(gpoGroupsPath),
                    Scope = ADGroupScope.DomainLocal, Description = $"{kind} GPO: {name}"
                });

            var definition = new TierModelGpo
            {
                Name = name,
                Role = Enum.Parse<TierModelGpoRole>(Rights.Gpo(name)),
                Tier = GpoTier(name),
                Links = gpo.Elements("GpoLink").Select(l => new TierModelGpoLink
                {
                    Target = Source.OuPath(T(l.Attribute("Path")?.Value)) ?? string.Empty,
                    Enabled = !string.Equals(l.Attribute("Enabled")?.Value, "No", StringComparison.OrdinalIgnoreCase)
                }).ToList(),
                ComputerFilter = await Filter(http, commit, tree, gpo.Element("GpoFilter")?.Attribute("WMI")?.Value)
            };
            if (gpo.Attribute("BackupID")?.Value is { Length: > 0 } backup)
                await ReadSettings(http, commit, tree, backup, definition, T);
            model.GroupPolicies.Add(definition);
        }
        return model;
    }

    private static readonly Dictionary<string, GpoComputerFilter?> Filters = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Filtre WMI de HardenAD : ses requêtes (Inputs/GroupPolicies/WmiFilters/&lt;nom&gt;.mof) disent s'il vise les serveurs,
    /// les postes ou tous les ordinateurs, lues comme l'import les lit dans msWMI-Parm2 (<see cref="WmiQueryFilter"/>).
    /// Le nom ne suffit pas : Windows-x64-NoDC ou Windows-Laps-Supported visent postes et serveurs.
    /// </summary>
    private static async Task<GpoComputerFilter?> Filter(HttpClient http, string commit, List<string> tree, string? wmi)
    {
        if (string.IsNullOrEmpty(wmi)) return null;
        if (Filters.TryGetValue(wmi, out var known)) return known;
        var file = tree.FirstOrDefault(p => p.StartsWith("Inputs/GroupPolicies/WmiFilters/", StringComparison.OrdinalIgnoreCase)
                                            && p.EndsWith($"/{wmi}.mof", StringComparison.OrdinalIgnoreCase));
        GpoComputerFilter? filter = null;
        if (file != null)
        {
            var mof = await Source.File(http, Repository, commit, file);
            var queries = Regex.Matches(mof, @"Query\s*=\s*""((?:[^""\\]|\\.)*)""").Select(m => Regex.Unescape(m.Groups[1].Value));
            filter = WmiQueryFilter.Classify(string.Join(";", queries));
        }
        else
            Console.Error.WriteLine($"  Filtre WMI sans fichier MOF : {wmi}");
        return Filters[wmi] = filter;
    }

    /// <summary>
    /// Paramètres d'une GPO, lus dans le rapport de sa sauvegarde (gpreport.xml) : refus d'ouverture de session, groupes
    /// restreints et préférences du groupe local Administrateurs. Les noms y sont ceux du domaine où la sauvegarde a été
    /// faite ; la table de migration et translation.xml de la sauvegarde les ramènent aux variables de la séquence.
    /// </summary>
    private static async Task ReadSettings(HttpClient http, string commit, List<string> tree, string backup, TierModelGpo gpo, Func<string?, string> translate)
    {
        var folder = tree.Select(p => p[..(p.LastIndexOf('/') + 1)])
            .FirstOrDefault(p => p.StartsWith("Inputs/GroupPolicies/", StringComparison.Ordinal) && p.EndsWith($"/{backup}/", StringComparison.OrdinalIgnoreCase));
        if (folder == null || !tree.Contains(folder + "gpreport.xml")) return;
        var report = XDocument.Parse(await Source.File(http, Repository, commit, folder + "gpreport.xml"));
        static IEnumerable<XElement> Children(XElement e, string name) => e.Elements().Where(c => c.Name.LocalName == name);
        static string? NameOf(XElement member) => Children(member, "Name").FirstOrDefault()?.Value;

        List<string>? Right(string right) => report.Descendants().Where(e => e.Name.LocalName == "UserRightsAssignment")
            .Where(e => Children(e, "Name").Any(n => n.Value == right))
            .Select(e => Children(e, "Member").Select(NameOf).OfType<string>().ToList())
            .FirstOrDefault();
        var local = Right("SeDenyInteractiveLogonRight");
        var remote = Right("SeDenyRemoteInteractiveLogonRight");

        // Groupes restreints (remplacent les membres), puis préférences (ajoutent ; « supprimer tous les groupes membres » remplace)
        List<string>? admins = null;
        var replace = false;
        foreach (var restricted in report.Descendants().Where(e => e.Name.LocalName == "RestrictedGroups"))
            if (Children(restricted, "GroupName").Any(g => Children(g, "SID").Any(s => s.Value == "S-1-5-32-544")))
                (admins, replace) = ([.. Children(restricted, "Member").Select(NameOf).OfType<string>()], true);
        foreach (var properties in report.Descendants().Where(e => e.Name.LocalName == "Properties" && e.Attribute("groupSid")?.Value == "S-1-5-32-544"))
        {
            admins ??= [];
            admins.AddRange(properties.Descendants()
                .Where(m => m.Name.LocalName == "Member" && string.Equals(m.Attribute("action")?.Value, "ADD", StringComparison.OrdinalIgnoreCase))
                .Select(m => m.Attribute("name")?.Value).OfType<string>());
            replace |= properties.Attribute("deleteAllGroups")?.Value == "1";
        }
        if (local == null && remote == null && admins == null) return;

        // sAMAccountName de la source -> nom de destination, dans la table de migration puis dans translation.xml
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in tree.Where(p => p.StartsWith(folder, StringComparison.Ordinal) && p.LastIndexOf('/') == folder.Length - 1
                                             && (p.EndsWith(".migtable", StringComparison.OrdinalIgnoreCase) || p.EndsWith("/translation.xml", StringComparison.OrdinalIgnoreCase))))
        {
            var xml = XDocument.Parse(await Source.File(http, Repository, commit, file));
            foreach (var mapping in xml.Descendants().Where(e => e.Name.LocalName == "Mapping"))
                if (Children(mapping, "Source").FirstOrDefault()?.Value is { } from && Children(mapping, "Destination").FirstOrDefault()?.Value is { } to)
                    map.TryAdd(Sam(from), to);
            foreach (var replacement in xml.Descendants().Where(e => e.Name.LocalName == "replace"))
                if (replacement.Attribute("find")?.Value is { } from && !from.StartsWith("S-1-", StringComparison.Ordinal)
                    && replacement.Attribute("replaceBy")?.Value is { } to)
                    map.TryAdd(Sam(from), to);
        }
        // Un nom resté variable (groupe propre à chaque ordinateur) n'existe pas dans l'annuaire : il est laissé
        List<string> Resolve(IEnumerable<string> names) => names
            .Select(n => translate(Sam(map.TryGetValue(Sam(n), out var to) ? to : n)))
            .Where(n => n.Length > 0 && !n.Contains('%'))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (local != null) gpo.DenyInteractiveLogon = Resolve(local);
        if (remote != null) gpo.DenyRemoteInteractiveLogon = Resolve(remote);
        if (admins != null) (gpo.LocalAdmins, gpo.ReplaceLocalAdmins) = (Resolve(admins), replace);
    }

    /// <summary>« DOMAINE\nom » ou « nom@domaine » -> « nom ».</summary>
    private static string Sam(string name)
    {
        var sam = name[(name.LastIndexOf('\\') + 1)..];
        return sam.Contains('@') ? sam[..sam.IndexOf('@')] : sam;
    }

    private static string Sid(string value)
    {
        foreach (var (sid, name) in WellKnownSids)
            if (value.Equals(sid, StringComparison.OrdinalIgnoreCase) || value.EndsWith(sid, StringComparison.OrdinalIgnoreCase)) return name;
        return value;
    }

    private static readonly Dictionary<string, string?> Suffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["T0"] = "Tier 0", ["T1"] = "Tier 1", ["T2"] = "Tier 2", ["T1L"] = "Tier 1 Legacy", ["T2L"] = "Tier 2 Legacy",
        // Les PAW du Tier 0 lui appartiennent ; celles que partagent les tiers 1, 2 et Legacy (PawT12L) ne se
        // classent dans aucun
        ["PawT0"] = "Tier 0", ["PawT12L"] = null
    };

    /// <summary>
    /// Tier d'un groupe : celui de son OU, sauf pour les groupes de connexion aux PAW, rangés dans GroupsT0 mais
    /// classés comme les PAW auxquelles ils donnent accès (PawAccess et PawT12L sont partagées par plusieurs tiers).
    /// </summary>
    private static string? GroupTier(string name, string path) =>
        Regex.Match(name, "_(PawT0|PawT12L|PawAccess)_") is { Success: true } m
            ? m.Groups[1].Value == "PawT0" ? "Tier 0" : null
            : TierOf(path);

    /// <summary>
    /// Tier d'une OU de HardenAD : suffixe de la branche d'administration (GroupsT0, UsersT1L…), ou rôle de l'OU dans
    /// Harden_T0, Harden_T12 (serveurs en Tier 1, le reste en Tier 2) et Harden_TL (même partage, tiers « Legacy »).
    /// </summary>
    private static string? TierOf(string path)
    {
        var parts = path.Split('/');
        if (parts.Length < 2) return parts[0] == "Harden_T0" ? "Tier 0" : null;
        var serverSide = parts[1] is "Servers" or "Services";
        return parts[0] switch
        {
            "_Administration" => Regex.Match(parts[1], "(PawT0|PawT12L|T0|T1L|T2L|T1|T2)$") is { Success: true } m ? Suffixes[m.Value] : null,
            "Harden_T0" => "Tier 0",
            "Harden_T12" => serverSide ? "Tier 1" : "Tier 2",
            "Harden_TL" => serverSide ? "Tier 1 Legacy" : "Tier 2 Legacy",
            _ => null
        };
    }

    private static TierModelOuRole RoleOf(string path)
    {
        var parts = path.Split('/');
        if (parts[0] == "Provisioning") return TierModelOuRole.Provisioning;
        if (parts[^1] == "Disabled") return TierModelOuRole.Disabled;
        if (parts.Length == 1) return TierModelOuRole.Container;
        if (parts[0] == "_Administration")
            return parts[1].StartsWith("Groups") ? TierModelOuRole.AdminGroups
                 : parts[1].StartsWith("Users") ? TierModelOuRole.AdminAccounts
                 : TierModelOuRole.AdminWorkstations;
        return parts[1] switch
        {
            "Servers" => TierModelOuRole.Servers,
            "Workstations" => TierModelOuRole.Workstations,
            "Users" => TierModelOuRole.Users,
            "Services" => TierModelOuRole.ServiceAccounts,
            "Groups" => TierModelOuRole.Groups,
            "Contacts" => TierModelOuRole.Contacts,
            _ => TierModelOuRole.Container
        };
    }

    private static string? GpoTier(string name) =>
        Regex.Match(name, "-(PawT0|PawT12L|T0|T1L|T2L|T1|T2)(?=-|$)") is { Success: true } m ? Suffixes[m.Groups[1].Value] : null;
}
