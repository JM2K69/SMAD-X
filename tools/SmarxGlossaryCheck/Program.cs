// Rejoue les commandes de l'aide-mémoire de SMAR-X sur de vrais systèmes, telles qu'elles y sont écrites, et vérifie
// leur effet. Les commandes viennent de Glossary (SMAR-X.Core) : une commande modifiée dans l'aide-mémoire n'a plus de
// relevé, et GlossaryFidelityTests échoue tant qu'elle n'est pas rejouée.
//
// Windows (le poste CLI du labo, en administrateur) : les chemins D:\Partages deviennent C:\SmxGlossary\A\Partages
// (et \B\ pour la commande d'un module, comparée à son équivalent icacls sur une copie identique) ; CORP\ devient le
// nom du poste, où des groupes locaux de même nom (DL_RH_LM, G_RH…) et un compte bob sont créés le temps du relevé ;
// le partage RH devient SmxRH. Tout est retiré à la fin, sauf le module NTFSSecurity, installé.
//
//   dotnet run --project tools/SmarxGlossaryCheck -- windows > glossary-windows.ps1
//   (sur CLI) powershell -NoProfile -ExecutionPolicy Bypass -File glossary-windows.ps1 > windows.out
//
// Linux (un Debian 13 dans podman) : le serveur d'exemple est recréé sous /w (un tmpfs, qui porte les ACL) avant
// chaque commande, et /srv/ devient /w/srv/.
//
//   dotnet run --project tools/SmarxGlossaryCheck -- linux > glossary-linux.sh
//   podman run --rm -v ./glossary-linux.sh:/check.sh:ro --tmpfs /w:rw,mode=0755 debian:trixie \
//     sh -c 'apt-get -qq update && apt-get -qq install -y acl samba >/dev/null 2>&1 && bash /check.sh' > linux.out
//
// Puis les relevés se figent dans SMAR-X.Tests/Fidelity :
//   dotnet run --project tools/SmarxGlossaryCheck -- freeze windows.out > SMAR-X.Tests/Fidelity/glossary-windows.txt
using System.Text;
using SMARX.Samples;
using SMARX.Services;
using SMARX.Services.Linux;

switch (args.FirstOrDefault())
{
    case "windows": Console.Write(Windows.Script()); break;
    case "linux": Console.Write(Linux.Script()); break;
    case "freeze": Console.Write(Freeze(File.ReadAllLines(args[1]))); break;
    default: Console.Error.WriteLine("usage : windows | linux | freeze <sortie>"); return 2;
}
return 0;

// Le relevé figé : l'en-tête, puis un verdict par commande ; les détails ne restent que pour un écart
static string Freeze(string[] lines)
{
    var s = new StringBuilder();
    foreach (var line in lines.Select(l => l.TrimEnd('\r')))
        if (line.StartsWith('#') || line.StartsWith("OK ") || line.StartsWith("SYNTAX ") || line.StartsWith("DIFF ") || line.StartsWith("FAIL "))
            s.Append(line).Append('\n');
    return s.ToString();
}

/// <summary>Comment rejouer une commande : avant (préparation), mode, et ce qui doit être vrai après.</summary>
sealed record Spec(Func<GlossaryCommand, bool> Match, string Mode, string Pre = "", string Assert = "$true", string Twin = "", string Post = "");

static class Windows
{
    private const string Lf = "\n";

    // Chaque commande de l'aide-mémoire, et comment la vérifier ; une commande sans règle arrête le générateur
    private static readonly Spec[] Specs =
    [
        // ─── icacls ───
        new(c => c.Command == @"icacls D:\Partages\RH", "single",
            Assert: @"$out -match 'BUILTIN\\Administrators'"),
        new(c => c.Command.Contains("/grant \"CORP\\DL_RH_LM:(OI)(CI)M\"") && c.Icacls == null, "single",
            Assert: @"[bool]((icacls ""$base\Partages\RH"") -match 'DL_RH_LM:\(OI\)\(CI\)\(M\)')"),
        new(c => c.Command.Contains("/grant:r"), "single",
            Pre: @"icacls ""$base\Partages\RH"" /grant ""$pc\DL_RH_L:(OI)(CI)M"" | Out-Null",
            Assert: @"$l = icacls ""$base\Partages\RH""; [bool]($l -match 'DL_RH_L:\(OI\)\(CI\)\(RX\)') -and -not ($l -match 'DL_RH_L:\(OI\)\(CI\)\(M\)')"),
        new(c => c.Command.Contains("/deny") && c.Icacls == null, "single",
            Assert: @"[bool]((icacls ""$base\Partages\Projets"") -match 'DL_Projets_Refuser:\(OI\)\(CI\)\(DENY\)\(WD,AD,WEA,WA\)')"),
        new(c => c.Command.Contains("/remove:g"), "single",
            Pre: @"icacls ""$base\Partages\RH"" /grant ""$pc\G_RH:(OI)(CI)M"" | Out-Null; icacls ""$base\Partages\RH"" /deny ""$pc\G_RH:(OI)(CI)(WD)"" | Out-Null",
            Assert: @"$g = @((icacls ""$base\Partages\RH"") -match 'G_RH:'); $g.Count -eq 1 -and $g[0] -match '\(DENY\)'"),
        new(c => c.Command == @"icacls D:\Partages\RH /inheritance:d", "single",
            Pre: @"$n = @((icacls ""$base\Partages\RH"") -match ':\(').Count",
            Assert: @"$l = icacls ""$base\Partages\RH""; (Get-Acl ""$base\Partages\RH"").AreAccessRulesProtected -and -not ($l -match '\(I\)') -and @($l -match ':\(').Count -eq $n"),
        new(c => c.Command.Contains("/setowner") && c.Icacls == null, "single",
            Assert: @"@(Get-ChildItem ""$base\Partages\RH"" -Recurse -Force | Where-Object { (Get-Acl $_.FullName).Owner -ne 'BUILTIN\Administrators' }).Count -eq 0 -and (Get-Acl ""$base\Partages\RH"").Owner -eq 'BUILTIN\Administrators'"),
        new(c => c.Command.Contains("/reset /T"), "single",
            Pre: @"icacls ""$base\Partages\RH\salaires.xlsx"" /grant ""$pc\G_RH:M"" | Out-Null; icacls ""$base\Partages\RH\sub"" /grant ""$pc\G_RH:(OI)(CI)M"" | Out-Null",
            Assert: @"$l = icacls ""$base\Partages\RH\*"" /T | Where-Object { $_ -match ':\(' }; @($l | Where-Object { $_ -notmatch '\(I\)' }).Count -eq 0"),
        new(c => c.Command.Contains("/findsid") && c.Icacls == null, "single",
            Pre: @"icacls ""$base\Partages\Projets"" /grant ""$pc\G_Stagiaires:(OI)(CI)RX"" | Out-Null",
            Assert: @"$out -match 'SID Found: .*Projets\.'"),
        new(c => c.Command.Contains("/save rh.acl") && c.Icacls == null, "roundtrip",
            Pre: @"icacls ""$base\Partages\RH\salaires.xlsx"" /grant ""$pc\G_RH:M"" | Out-Null; icacls ""$base\Partages\RH\sub"" /grant ""$pc\G_RH:(OI)(CI)M"" | Out-Null"),

        // ─── PowerShell ───
        new(c => c.Command.StartsWith("Get-Acl "), "single",
            Assert: @"$out -match 'Owner' -and $out -match 'AccessToString'"),
        new(c => c.Command.Contains("AddAccessRule"), "twin",
            Twin: @"icacls D:\Partages\RH /grant ""CORP\DL_RH_LM:(OI)(CI)M"""),
        new(c => c.Command.Contains("SetAccessRuleProtection"), "twin",
            Twin: @"icacls D:\Partages\RH /inheritance:d"),
        new(c => c.Command.StartsWith("New-SmbShare "), "share",
            Pre: @"if (Get-SmbShare SmxRH -ErrorAction SilentlyContinue) { Remove-SmbShare SmxRH -Force }; Fixture A",
            Assert: @"$a = Get-SmbShareAccess SmxRH; (Get-SmbShare SmxRH).FolderEnumerationMode -eq 'AccessBased' -and [bool]($a | Where-Object { $_.AccountName -eq 'NT AUTHORITY\Authenticated Users' -and $_.AccessRight -eq 'Change' }) -and [bool]($a | Where-Object { $_.AccountName -eq 'BUILTIN\Administrators' -and $_.AccessRight -eq 'Full' })"),
        new(c => c.Command.StartsWith("Get-SmbShareAccess "), "share",
            Assert: @"$out -match 'Authenticated Users'"),
        new(c => c.Command.StartsWith("Grant-SmbShareAccess "), "share",
            Assert: @"[bool](Get-SmbShareAccess SmxRH | Where-Object { $_.AccountName -eq ""$pc\G_RH"" -and $_.AccessRight -eq 'Change' })",
            Post: "Remove-SmbShare SmxRH -Force"),
        new(c => c.Command.StartsWith("Add-ADGroupMember "), "syntax",
            Assert: @"$p = (Get-Command Add-ADGroupMember).Parameters.Keys; ($p -contains 'Identity') -and ($p -contains 'Members')"),
        new(c => c.Command == "whoami /groups", "single",
            Assert: @"$out -match 'GROUP INFORMATION'"),

        // ─── NTFSSecurity, chaque commande en face de son équivalent icacls ───
        new(c => c.Command == "Install-Module -Name NTFSSecurity", "install"),
        new(c => c.Command.StartsWith("Get-NTFSAccess -Path D:\\Partages\\RH") && c.Icacls == @"icacls D:\Partages\RH", "count"),
        new(c => c.Command.StartsWith("Get-NTFSAccess ") && c.Command.Contains("Remove-NTFSAccess"), "pair",
            Pre: @"icacls ""$base\Partages\RH"" /grant ""$pc\G_RH:(OI)(CI)M"" | Out-Null; icacls ""$base\Partages\RH"" /deny ""$pc\G_RH:(OI)(CI)(WD)"" | Out-Null"),
        new(c => c.Command.StartsWith("Enable-NTFSAccessInheritance "), "pairset",
            Pre: @"icacls ""$base\Partages\RH"" /inheritance:d | Out-Null; icacls ""$base\Partages\RH"" /grant ""$pc\G_RH:(OI)(CI)M"" | Out-Null"),
        new(c => c.Command.StartsWith("Get-ChildItem2 ") && c.Icacls != null && c.Icacls.Contains("/findsid"), "findsid",
            Pre: @"icacls ""$base\Partages\Projets"" /grant ""$pc\G_Stagiaires:(OI)(CI)RX"" | Out-Null"),
        new(c => c.Command.Contains("Export-Csv"), "roundtrip",
            Pre: @"icacls ""$base\Partages\RH\salaires.xlsx"" /grant ""$pc\G_RH:M"" | Out-Null; icacls ""$base\Partages\RH\sub"" /grant ""$pc\G_RH:(OI)(CI)M"" | Out-Null"),
        new(c => c.Command.StartsWith("Get-NTFSEffectiveAccess "), "effective",
            Pre: @"icacls ""$base\Partages\RH"" /grant ""$pc\DL_RH_LM:(OI)(CI)M"" | Out-Null",
            Assert: @"($r | Out-String) -match 'Modify'"),
        // Les autres commandes du module : la même chose que leur équivalent icacls, sur deux copies identiques
        new(c => c.Icacls != null, "pair")
    ];

    private static string Here(string text)
    {
        if (text.Split('\n').Any(l => l.StartsWith("'@"))) throw new InvalidOperationException("Texte impossible dans une here-string : " + text);
        return "@'" + Lf + text + Lf + "'@";
    }

    private static string Key(GlossaryCommand c) => c.Key;

    public static string Script()
    {
        var s = new StringBuilder(Harness.Replace("\r\n", Lf));
        foreach (var command in Glossary.Windows("en-US").SelectMany(x => x.CommandList))
        {
            var spec = Specs.FirstOrDefault(p => p.Match(command)) ?? throw new InvalidOperationException("Aucune règle pour : " + command.Key);
            s.Append(Lf).Append(Block(command, spec)).Append(Lf);
        }
        s.Append(Lf).Append(Cleanup.Replace("\r\n", Lf));
        return s.ToString();
    }

    private static string Block(GlossaryCommand c, Spec spec)
    {
        var cmd = Here(c.Command);
        var verdict = spec.Mode == "syntax" ? "SYNTAX" : "OK";
        var body = spec.Mode switch
        {
            "single" => $$"""
                    Fixture A; $base = "$root\A"
                    {{spec.Pre}}
                    $out = Exec (Sub ({{cmd}}) 'A')
                    {{spec.Assert}}
                """,
            "share" => $$"""
                    $base = "$root\A"
                    {{spec.Pre}}
                    $out = Exec (Sub ({{cmd}}) 'A')
                    {{spec.Assert}}
                """,
            "syntax" => $$"""
                    {{spec.Assert}}
                """,
            "twin" => $$"""
                    Fixture A; Fixture B
                    Exec (Sub ({{cmd}}) 'A') | Out-Null
                    Exec (Sub ({{Here(spec.Twin)}}) 'B') | Out-Null
                    Same (Dump A) (Dump B)
                """,
            "pair" => $$"""
                    Fixture A; Fixture B
                    $base = "$root\A"; {{spec.Pre}}
                    $base = "$root\B"; {{spec.Pre}}
                    Exec (Sub ({{Here(c.Icacls!)}}) 'A') | Out-Null
                    Exec (Sub ({{cmd}}) 'B') | Out-Null
                    Same (Dump A) (Dump B)
                """,
            "pairset" => $$"""
                    Fixture A; Fixture B
                    $base = "$root\A"; {{spec.Pre}}
                    $base = "$root\B"; {{spec.Pre}}
                    Exec (Sub ({{Here(c.Icacls!)}}) 'A') | Out-Null
                    Exec (Sub ({{cmd}}) 'B') | Out-Null
                    $order = Same (Dump A) (Dump B)
                    $set = Same (AceSet A) (AceSet B)
                    if ($set -eq $true) { if ($order -ne $true) { '# same ACEs, in another order' | Out-Host }; $true } else { $set }
                """,
            "count" => $$"""
                    Fixture A
                    $i = Exec (Sub ({{Here(c.Icacls!)}}) 'A')
                    $n = @(Invoke-Expression (Sub ({{cmd}}) 'A')).Count
                    $aces = @(($i -split "`r?`n") | Where-Object { $_ -match ':\(' }).Count
                    if ($n -eq $aces) { $true } else { "icacls: $aces ACE, NTFSSecurity: $n"; $i }
                """,
            "findsid" => $$"""
                    Fixture A; Fixture B
                    $base = "$root\A"; {{spec.Pre}}
                    $base = "$root\B"; {{spec.Pre}}
                    $i = Exec (Sub ({{Here(c.Icacls!)}}) 'A')
                    $x = @(($i -split "`r?`n") | Where-Object { $_ -match '^SID Found: (.*)\.$' } | ForEach-Object { ($_ -replace '^SID Found: (.*)\.$', '$1').Replace("$root\A", '<root>') } | Sort-Object)
                    $y = @(Invoke-Expression (Sub ({{cmd}}) 'B') | ForEach-Object { $_.FullName.Replace("$root\B", '<root>') } | Sort-Object -Unique)
                    Same $x $y
                """,
            "roundtrip" => $$"""
                    $results = @()
                    foreach ($run in @(@('A', {{(c.Icacls != null ? Here(c.Icacls) : cmd)}}), @('B', {{cmd}}))) {
                        if ({{(c.Icacls == null ? "$true" : "$false")}} -and $run[0] -eq 'B') { continue }
                        $side = $run[0]; Fixture $side; $base = "$root\$side"
                        {{spec.Pre}}
                        $before = Dump $side
                        $lines = (Sub $run[1] $side) -split "`r?`n"
                        Exec $lines[0] | Out-Null
                        icacls "$base\Partages\RH\*" /reset /T /C /Q | Out-Null
                        $mid = Dump $side
                        Exec $lines[1] | Out-Null
                        $after = Dump $side
                        if ((Same $before $mid) -eq $true) { $results += "$side : nothing was removed before the restore" }
                        else { $r = @(Same $before $after); if (-not ($r.Count -eq 1 -and $r[0] -eq $true)) { $results += "$side :"; $results += $r } }
                    }
                    if ($results.Count -eq 0) { $true } else { $results }
                """,
            "effective" => $$"""
                    Fixture B; $base = "$root\B"
                    {{spec.Pre}}
                    $r = @(Invoke-Expression (Sub ({{cmd}}) 'B'))
                    {{spec.Assert}}
                """,
            "install" => $$"""
                    if (-not (Get-PackageProvider -ListAvailable | Where-Object Name -eq NuGet)) { Install-PackageProvider NuGet -MinimumVersion 2.8.5.201 -Force -Scope AllUsers | Out-Null }
                    if (-not (Get-Module NTFSSecurity -ListAvailable)) { Invoke-Expression (({{cmd}}) + ' -Force -Scope AllUsers') }
                    Import-Module NTFSSecurity
                    [bool](Get-Command Add-NTFSAccess -ErrorAction SilentlyContinue)
                """,
            _ => throw new InvalidOperationException(spec.Mode)
        };
        var post = spec.Post.Length > 0 ? Lf + spec.Post : "";
        return $"Check '{verdict}' ({Here(Key(c))}) {{{Lf}{body}{Lf}}}{post}";
    }

    private const string Harness = """
        # Rejoue les commandes de l'aide-mémoire de SMAR-X (généré par tools/SmarxGlossaryCheck)
        $ErrorActionPreference = 'Stop'
        $ProgressPreference = 'SilentlyContinue'
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $root = 'C:\SmxGlossary'
        $pc = $env:COMPUTERNAME
        $groups = 'DL_RH_LM', 'DL_RH_L', 'DL_Projets_Refuser', 'G_RH', 'G_Stagiaires'

        # D:\Partages devient une copie A ou B sous C:\SmxGlossary ; CORP\ devient ce poste ; le partage RH, SmxRH
        function Sub([string]$cmd, [string]$side) {
            $c = $cmd.Replace('D:\Partages', "$root\$side\Partages").Replace('CORP\', "$pc\")
            return ($c -replace '-Name RH(\s|$)', '-Name SmxRH$1')
        }
        function Fixture([string]$side) {
            $base = "$root\$side"
            if (Test-Path $base) { icacls $base /reset /T /C /Q | Out-Null; Remove-Item $base -Recurse -Force }
            foreach ($d in 'Partages\RH\sub', 'Partages\Projets', 'Partages\Compta') { New-Item -ItemType Directory "$base\$d" -Force | Out-Null }
            foreach ($f in 'Partages\RH\salaires.xlsx', 'Partages\RH\sub\note.txt', 'Partages\Projets\planning.xlsx', 'Partages\Compta\budget.xlsx') { Set-Content "$base\$f" 'x' }
        }
        # Les ACE de chaque objet dans l'ordre où elles sont stockées (icacls), son propriétaire et l'héritage
        function Dump([string]$side) {
            $base = "$root\$side"
            icacls "$base\Partages" /T /C | Where-Object { $_ -and $_ -notmatch '^Successfully processed' } | ForEach-Object { $_.Replace($base, '<root>') }
            Get-ChildItem "$base\Partages" -Recurse -Force | Sort-Object FullName | ForEach-Object {
                $acl = Get-Acl $_.FullName
                'OWNER ' + $_.FullName.Replace($base, '<root>') + ' ' + $acl.Owner + ' PROTECTED ' + $acl.AreAccessRulesProtected
            }
        }
        # Chaque ACE avec son objet, triées : les mêmes ACE, quel que soit leur ordre
        function AceSet([string]$side) {
            $base = "$root\$side"; $path = ''
            icacls "$base\Partages" /T /C | Where-Object { $_ -and $_ -notmatch '^Successfully processed' } | ForEach-Object {
                if ($_ -match '^(\S+) (.+)$') { $path = $Matches[1].Replace($base, '<root>'); "$path | $($Matches[2])" }
                else { "$path | $($_.Trim())" }
            } | Sort-Object
        }
        function Same($a, $b) {
            $d = Compare-Object @($a) @($b)
            if (-not $d) { return $true }
            $d | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" }
        }
        function Exec([string]$cmd) {
            $ErrorActionPreference = 'Continue'
            $global:LASTEXITCODE = 0
            $out = Invoke-Expression $cmd 2>&1 | Out-String
            if ($LASTEXITCODE -ne 0) { throw "exit code $LASTEXITCODE : $out" }
            return $out
        }
        function Check([string]$verdict, [string]$key, [scriptblock]$body) {
            try {
                Set-Location "$root\work"
                $r = @(& $body)
                if ($r.Count -eq 1 -and $r[0] -eq $true) { "$verdict $key" }
                else { "DIFF $key"; $r | ForEach-Object { "#   $_" } }
            } catch { "FAIL $key"; "#   $($_.Exception.Message)" }
        }

        $os = Get-CimInstance Win32_OperatingSystem
        "# $($os.Caption) $($os.Version), Windows PowerShell $($PSVersionTable.PSVersion)"
        New-Item -ItemType Directory "$root\work" -Force | Out-Null
        foreach ($g in $groups) { if (-not (Get-LocalGroup $g -ErrorAction SilentlyContinue)) { New-LocalGroup $g | Out-Null } }
        # bob, pour l'accès effectif : un mot de passe aléatoire, jamais affiché
        $chars = [char[]]'abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789!#%+'
        $secret = ConvertTo-SecureString (-join (1..24 | ForEach-Object { $chars | Get-Random })) -AsPlainText -Force
        if (-not (Get-LocalUser bob -ErrorAction SilentlyContinue)) { New-LocalUser bob -Password $secret -AccountNeverExpires | Out-Null }
        if (-not (Get-LocalGroupMember DL_RH_LM | Where-Object Name -like '*\bob')) { Add-LocalGroupMember DL_RH_LM -Member bob }
        "# Install-Module runs with -Force -Scope AllUsers, without prompts"
        """;

    private const string Cleanup = """
        Set-Location $env:TEMP
        if (Get-SmbShare SmxRH -ErrorAction SilentlyContinue) { Remove-SmbShare SmxRH -Force }
        foreach ($side in 'A', 'B') { if (Test-Path "$root\$side") { icacls "$root\$side" /reset /T /C /Q | Out-Null } }
        Remove-Item $root -Recurse -Force
        Remove-LocalUser bob -ErrorAction SilentlyContinue
        foreach ($g in $groups) { Remove-LocalGroup $g -ErrorAction SilentlyContinue }
        "# NTFSSecurity $((Get-Module NTFSSecurity -ListAvailable | Select-Object -First 1).Version)"
        """;
}

static class Linux
{
    private const string Lf = "\n";

    private static readonly Spec[] Specs =
    [
        new(c => c.Command == "ls -ld /srv/compta", "single", Assert: "[[ \"$out\" == d* ]]"),
        new(c => c.Command.StartsWith("stat -c "), "single", Assert: "grep -q 'budget.ods' <<<\"$out\""),
        new(c => c.Command.StartsWith("chown alice:compta"), "single", Pre: "chown bob:bob /w/srv/compta/budget.ods",
            Assert: "[ \"$(stat -c %U:%G /w/srv/compta/budget.ods)\" = alice:compta ]"),
        new(c => c.Command == "chmod 2770 /srv/compta", "single", Pre: "chmod 0755 /w/srv/compta",
            Assert: "[ \"$(stat -c %a /w/srv/compta)\" = 2770 ]"),
        new(c => c.Command.StartsWith("chmod u=rw,g=r,o="), "single", Pre: "chmod 0777 /w/srv/compta/budget.ods",
            Assert: "[ \"$(stat -c %a /w/srv/compta/budget.ods)\" = 640 ]"),
        new(c => c.Command.StartsWith("chmod -R g+rX"), "single",
            Pre: "setfacl -R -b -k /w/srv/projets; touch /w/srv/projets/plan.txt; chmod 0600 /w/srv/projets/plan.txt; mkdir /w/srv/projets/sub; chmod 0700 /w/srv/projets/sub",
            Assert: "[ \"$(stat -c %a /w/srv/projets/plan.txt)\" = 640 ] && [ \"$(stat -c %a /w/srv/projets/sub)\" = 2750 ]"),
        new(c => c.Command == "chmod 1777 /srv/depot", "single", Pre: "chmod 0755 /w/srv/depot",
            Assert: "[ \"$(stat -c %a /w/srv/depot)\" = 1777 ]"),
        new(c => c.Command.StartsWith("getfacl /srv/partage/doc.txt"), "single", Assert: "grep -q '^mask::' <<<\"$out\""),
        new(c => c.Command.StartsWith("setfacl -m u:bob:rw"), "single", Assert: "getfacl -p /w/srv/partage/doc.txt | grep -q '^user:bob:rw-'"),
        new(c => c.Command.StartsWith("setfacl -m g:projet:rwx"), "single", Assert: "getfacl -p /w/srv/projets | grep -q '^group:projet:rwx'"),
        new(c => c.Command.StartsWith("setfacl -d -m g:compta:rwX"), "single",
            Assert: "getfacl -p /w/srv/compta | grep -q '^default:group:compta:rwx' && touch /w/srv/compta/nouveau.txt && getfacl -p /w/srv/compta/nouveau.txt | grep -q '^group:compta:rw'"),
        new(c => c.Command.StartsWith("setfacl -R -m g:projet:rX"), "single",
            Pre: "touch /w/srv/projets/plan.txt; mkdir -p /w/srv/projets/sub",
            Assert: "[ \"$(getfacl -p -R /w/srv/projets 2>/dev/null | grep -c '^group:projet:r')\" = \"$(find /w/srv/projets | wc -l)\" ]"),
        new(c => c.Command.StartsWith("setfacl -x u:bob"), "single", Pre: "setfacl -m u:bob:rw /w/srv/partage/doc.txt",
            Assert: "! getfacl -p /w/srv/partage/doc.txt | grep -q '^user:bob:'"),
        new(c => c.Command.StartsWith("getfacl -R /srv/compta"), "roundtrip"),
        new(c => c.Command == "id bob", "single", Assert: "grep -q 'uid=1002(bob)' <<<\"$out\""),
        new(c => c.Command == "usermod -aG compta bob", "single", Pre: "gpasswd -d bob compta >/dev/null",
            Assert: "g=\" $(id -nG bob) \"; [[ \"$g\" == *' compta '* && \"$g\" == *' rh '* && \"$g\" == *' projet '* ]]"),
        new(c => c.Command == "umask 027", "umask"),
        new(c => c.Command == "testparm -s", "single", Assert: "grep -q '\\[global\\]' <<<\"$out\""),
        new(c => c.Command == "smbpasswd -a bob", "smbpasswd"),
        new(c => c.Command == "smbcontrol all reload-config", "single", Pre: "smbd -D; sleep 2", Assert: "true"),
    ];

    private static string Here(string text)
    {
        if (text.Split('\n').Any(l => l == "SMX_EOF")) throw new InvalidOperationException(text);
        return "$(cat <<'SMX_EOF'" + Lf + text + Lf + "SMX_EOF" + Lf + ")";
    }

    public static string Script()
    {
        var host = LinuxSamples.FileServer();
        var s = new StringBuilder();
        s.Append("#!/bin/bash").Append(Lf).Append("set -u").Append(Lf);
        s.Append("setup() {").Append(Lf);
        foreach (var line in LinuxScripts.Setup(host, "/w")) s.Append("  ").Append(line).Append(Lf);
        s.Append("  mkdir -p /w/srv/depot").Append(Lf).Append("}").Append(Lf);
        s.Append(Harness.Replace("\r\n", Lf)).Append(Lf);
        var index = 0;
        foreach (var command in Glossary.Linux("en-US").SelectMany(x => x.CommandList))
        {
            var spec = Specs.FirstOrDefault(p => p.Match(command)) ?? throw new InvalidOperationException("Aucune règle pour : " + command.Key);
            index++;
            s.Append(Lf).Append(Block(command, spec, index)).Append(Lf);
        }
        // Les blocs viennent de chaînes brutes du source : sans retour chariot, bash les lit
        return s.ToString().Replace("\r", "");
    }

    private static string Block(GlossaryCommand c, Spec spec, int index)
    {
        var body = spec.Mode switch
        {
            "single" => $$"""
                  {{(spec.Pre.Length > 0 ? spec.Pre : ":")}}
                  out=$(eval "$(sub "$cmd")" 2>&1) || { echo "exit $?: $out"; return 1; }
                  {{spec.Assert}} || { echo "$out"; return 1; }
                """,
            "roundtrip" => """
                  before=$(getfacl -p -R /w/srv/compta 2>/dev/null; stat -c '%n %a %U:%G' $(find /w/srv/compta))
                  first=$(sub "$cmd" | sed -n 1p); second=$(sub "$cmd" | sed -n 2p)
                  eval "$first" || return 1
                  setfacl -R -b -k /w/srv/compta; chmod -R 0700 /w/srv/compta
                  [ "$before" != "$(getfacl -p -R /w/srv/compta 2>/dev/null; stat -c '%n %a %U:%G' $(find /w/srv/compta))" ] || { echo "nothing changed before the restore"; return 1; }
                  (eval "$second") || return 1
                  after=$(getfacl -p -R /w/srv/compta 2>/dev/null; stat -c '%n %a %U:%G' $(find /w/srv/compta))
                  [ "$before" = "$after" ] || { diff <(echo "$before") <(echo "$after"); return 1; }
                """,
            "umask" => """
                  (eval "$(sub "$cmd")"; touch /w/u.f; mkdir /w/u.d)
                  [ "$(stat -c %a /w/u.f)" = 640 ] && [ "$(stat -c %a /w/u.d)" = 750 ] || { stat -c '%n %a' /w/u.f /w/u.d; return 1; }
                """,
            // smbpasswd demande le mot de passe deux fois : sans terminal, -s le lit sur l'entrée standard
            "smbpasswd" => """
                  pw=$(head -c 18 /dev/urandom | base64)
                  printf '%s\n%s\n' "$pw" "$pw" | eval "$(sub "$cmd") -s" >/dev/null || return 1
                  pdbedit -L | grep -q '^bob:' || { pdbedit -L; return 1; }
                """,
            _ => throw new InvalidOperationException(spec.Mode)
        };
        return $"c{index}() {{{Lf}  cmd={Here(c.Command)}{Lf}{body.TrimEnd()}{Lf}}}{Lf}check OK \"{Here(c.Key)}\" c{index}";
    }

    private const string Harness = """
        # Rejoue les commandes de l'aide-mémoire de SMAR-X (généré par tools/SmarxGlossaryCheck)
        sub() { printf '%s' "$1" | sed -e 's#/srv/#/w/srv/#g'; }
        check() {
          local verdict="$1" key="$2" fn="$3" out
          setup >/dev/null 2>&1; cd /root
          if out=$($fn 2>&1); then echo "$verdict $key"; else echo "DIFF $key"; printf '%s\n' "$out" | sed 's/^/#   /'; fi
        }
        echo "# $(uname -sr), $(. /etc/os-release && echo $PRETTY_NAME), $(setfacl --version | head -1), Samba $(smbd --version | sed 's/Version //')"
        echo "# smbpasswd -a runs with -s, the password on standard input"
        """;
}
