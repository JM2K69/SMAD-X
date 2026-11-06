# Relève comment le vrai icacls affiche les droits d'une ACE, pour aligner l'affichage de SMAR-X (Ntfs.Display).
# À lancer en administrateur sur un Windows (le poste CLI du labo) :
#   powershell -NoProfile -ExecutionPolicy Bypass -File icacls-display.ps1 > icacls-display.txt
# Un fichier par combinaison des 14 droits spéciaux (16 383 masques), autorisée puis refusée à Backup Operators ; puis
# un dossier par combinaison des drapeaux d'héritage (OI, CI, IO, NP, et ID pour une ACE héritée), sur quelques masques.
# Les ACE sont posées telles quelles (SDDL), sans passer par icacls ; icacls ne sert qu'à les lire. Tout est retiré à la fin.
$ErrorActionPreference = 'Stop'
$root = 'C:\SmxIcacls'
if (Test-Path $root) { icacls $root /reset /T /C /Q | Out-Null; Remove-Item $root -Recurse -Force }
$os = Get-CimInstance Win32_OperatingSystem
"# $($os.Caption) $($os.Version), icacls"

# RD WD AD REA WEA X DC RA WA, puis DE RC WDAC WO S
$bits = @(0x1, 0x2, 0x4, 0x8, 0x10, 0x20, 0x40, 0x80, 0x100, 0x10000, 0x20000, 0x40000, 0x80000, 0x100000)
foreach ($kind in 'A', 'D') {
    $dir = "$root\$kind"
    New-Item -ItemType Directory $dir -Force | Out-Null
    for ($i = 1; $i -lt 16384; $i++) {
        $mask = 0
        for ($b = 0; $b -lt 14; $b++) { if ($i -band (1 -shl $b)) { $mask = $mask -bor $bits[$b] } }
        $hex = '{0:X}' -f $mask
        $path = "$dir\$hex"
        [System.IO.File]::WriteAllText($path, '')
        $sddl = if ($kind -eq 'A') { "D:P(A;;FA;;;BA)(A;;0x$hex;;;BO)" } else { "D:P(D;;0x$hex;;;BO)(A;;FA;;;BA)" }
        $fs = New-Object System.Security.AccessControl.FileSecurity
        $fs.SetSecurityDescriptorSddlForm($sddl, 'Access')
        [System.IO.File]::SetAccessControl($path, $fs)
    }
    $current = ''
    icacls "$dir\*" | ForEach-Object {
        if ($_ -match '\\([0-9A-F]+) ') { $current = $Matches[1] }
        if ($_ -match 'Backup Operators:(.*)$') { "MASK $kind 0x$current $($Matches[1])" }
    }
}

# Les drapeaux : chaque combinaison, autorisée et refusée, sur quelques masques
$flags = 'OI', 'CI', 'IO', 'NP', 'ID'
$masks = 0x1F01FF, 0x1301BF, 0x1200A9, 0x120089, 0x100116, 0x116, 0x1
$n = 0
foreach ($kind in 'A', 'D') {
    foreach ($mask in $masks) {
        for ($f = 0; $f -lt 32; $f++) {
            $set = -join (0..4 | Where-Object { $f -band (1 -shl $_) } | ForEach-Object { $flags[$_] })
            $n++
            $path = "$root\F$n"
            New-Item -ItemType Directory $path | Out-Null
            $hex = '{0:X}' -f $mask
            $ace = "($kind;$set;0x$hex;;;BO)"
            $sddl = if ($kind -eq 'A') { "D:P(A;OICI;FA;;;BA)$ace" } else { "D:P$ace(A;OICI;FA;;;BA)" }
            $ds = New-Object System.Security.AccessControl.DirectorySecurity
            $ds.SetSecurityDescriptorSddlForm($sddl, 'Access')
            [System.IO.Directory]::SetAccessControl($path, $ds)
            $line = (icacls $path) | Where-Object { $_ -match 'Backup Operators:(.*)$' } | ForEach-Object { $Matches[1] }
            "FLAGS $kind [$set] 0x$hex $line"
        }
    }
}

icacls $root /reset /T /C /Q | Out-Null
Remove-Item $root -Recurse -Force
