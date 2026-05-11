<#
.SYNOPSIS
    Export Active Directory vers un fichier JSON importable dans Active Directory Simulator.

.DESCRIPTION
    Ce script lit la structure d'un domaine Active Directory (OUs, Containers, Users,
    Groups, Computers, GMSAs, GPOs, PSOs) et produit un JSON au format attendu
    par Active Directory Simulator.

.PARAMETER OutputPath
    Chemin du fichier JSON de sortie. Par defaut : .\AD_Export.json

.PARAMETER DomainDN
    DN du domaine cible. Par defaut : domaine courant detecte automatiquement.

.EXAMPLE
    .\Export-ADToSimulator.ps1
    .\Export-ADToSimulator.ps1 -OutputPath C:\Exports\mon_domaine.json

.NOTES
    Requires : ActiveDirectory PowerShell module (RSAT)
    Rights   : Domain Read
#>

param(
    [string]$OutputPath = '.\AD_Export.json',
    [string]$DomainDN   = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── 1. Prerequis ─────────────────────────────────────────────────────────────
if (-not (Get-Module -ListAvailable -Name ActiveDirectory)) {
    Write-Error 'Le module ActiveDirectory (RSAT) est requis.'
    exit 1
}
Import-Module ActiveDirectory -ErrorAction Stop

# ── 2. Domaine cible ─────────────────────────────────────────────────────────
if ([string]::IsNullOrWhiteSpace($DomainDN)) {
    $domain     = Get-ADDomain
    $DomainDN   = $domain.DistinguishedName
    $domainName = $domain.DNSRoot
} else {
    $domainName = ($DomainDN -replace 'DC=','' -replace ',DC=','.').Trim(',').Trim()
}
Write-Host "Domaine cible : $domainName ($DomainDN)" -ForegroundColor Cyan

# ── 3. Helpers ───────────────────────────────────────────────────────────────
function ConvertTo-JsonString([string]$str) {
    if ($null -eq $str) { return '' }
    $str = $str.Replace('\','\\').Replace('"','\"').Replace("`n",'\n').Replace("`r",'\r').Replace("`t",'\t')
    return $str
}

function Format-Date($d) {
    if ($null -eq $d) { return (Get-Date -Format 'o') }
    try { return ([datetime]$d).ToString('o') } catch { return (Get-Date -Format 'o') }
}

function Get-SimType([string]$objectClass) {
    switch ($objectClass) {
        'organizationalUnit'                  { return 'OrganizationalUnit' }
        'container'                           { return 'Container' }
        'builtinDomain'                       { return 'Container' }
        'user'                                { return 'User' }
        'group'                               { return 'Group' }
        'computer'                            { return 'Computer' }
        'msDS-GroupManagedServiceAccount'     { return 'GMSA' }
        'groupPolicyContainer'                { return 'Policy' }
        'msDS-PasswordSettings'               { return 'PasswordSettingsObject' }
        default                               { return 'Container' }
    }
}

# ── 4. Collecte des GPOs ─────────────────────────────────────────────────────
Write-Host 'Collecte des GPOs...' -ForegroundColor Yellow
$gpoMap = @{}
try {
    Get-ADObject -Filter { objectClass -eq 'groupPolicyContainer' } `
                 -SearchBase $DomainDN `
                 -Properties DisplayName,cn | ForEach-Object {
        $gpoMap[$_.DistinguishedName] = if ($_.DisplayName) { $_.DisplayName } else { $_.cn }
    }
} catch { Write-Warning "Impossible de lire les GPOs : $_" }

# ── 5. Liens GPO ─────────────────────────────────────────────────────────────
function Get-GPOLinksForDN([string]$dn) {
    try {
        $obj = Get-ADObject -Identity $dn -Properties gpLink
        if ($obj.gpLink) {
            $links = [regex]::Matches($obj.gpLink, '\[LDAP://([^\]]+);') | ForEach-Object { $_.Groups[1].Value }
            return @($links | ForEach-Object { if ($gpoMap.ContainsKey($_)) { $gpoMap[$_] } else { $_ } })
        }
    } catch {}
    return @()
}

# ── 6. PSOs ──────────────────────────────────────────────────────────────────
Write-Host 'Collecte des PSOs...' -ForegroundColor Yellow
$psoObjects = @()
try {
    $psoObjects = Get-ADFineGrainedPasswordPolicy -Filter * | Select-Object `
        Name,DistinguishedName,Precedence,MinPasswordLength,PasswordHistoryCount,
        ComplexityEnabled,MaxPasswordAge,MinPasswordAge,LockoutThreshold,
        LockoutDuration,LockoutObservationWindow,AppliesTo
} catch { Write-Warning "Impossible de lire les PSOs : $_" }

# ── 8. Construction recursive ────────────────────────────────────────────────
function Build-NodeJson {
    param([string]$dn, [string]$name, [string]$simType, [object]$adObj, [int]$depth = 0)

    $id          = [guid]::NewGuid().ToString()
    $description = if ($adObj -and $adObj.Description) { ConvertTo-JsonString (($adObj.Description | Out-String).Trim()) } else { '' }
    $created     = Format-Date $(if ($adObj) { $adObj.WhenCreated } else { $null })
    $modified    = Format-Date $(if ($adObj) { $adObj.WhenChanged } else { $null })

    # MemberOf
    $memberOfJson = '[]'
    if ($adObj -and $adObj.MemberOf) {
        $names = foreach ($gdn in $adObj.MemberOf) {
            try { $o = Get-ADObject -Identity $gdn -Properties SAMAccountName; $o.SAMAccountName } catch {}
        }
        $names = @($names | Where-Object { $_ })
        if ($names.Count -gt 0) {
            $memberOfJson = '[' + (($names | ForEach-Object { '"' + (ConvertTo-JsonString $_) + '"' }) -join ',') + ']'
        }
    }

    # LinkedGPOs
    $linkedGPOJson = '[]'
    if ($simType -in @('OrganizationalUnit','Domain')) {
        $links = Get-GPOLinksForDN $dn
        if ($links.Count -gt 0) {
            $linkedGPOJson = '[' + (($links | ForEach-Object { '"' + (ConvertTo-JsonString $_) + '"' }) -join ',') + ']'
        }
    }

    # PSO fields
    $psoAppliesToJson = '[]'
    $psoPrec = $psoMinLen = $psoHistory = $psoComplex = $psoMaxAge = $psoMinAge = $psoLockThr = $psoLockDur = $psoLockObs = 'null'
    if ($simType -eq 'PasswordSettingsObject') {
        $pso = $psoObjects | Where-Object { $_.DistinguishedName -eq $dn } | Select-Object -First 1
        if ($pso) {
            $psoPrec    = $pso.Precedence
            $psoMinLen  = $pso.MinPasswordLength
            $psoHistory = $pso.PasswordHistoryCount
            $psoComplex = ($pso.ComplexityEnabled).ToString().ToLower()
            $psoMaxAge  = if ($pso.MaxPasswordAge) { [int]$pso.MaxPasswordAge.TotalDays } else { 'null' }
            $psoMinAge  = if ($pso.MinPasswordAge) { [int]$pso.MinPasswordAge.TotalDays } else { 'null' }
            $psoLockThr = $pso.LockoutThreshold
            $psoLockDur = if ($pso.LockoutDuration) { [int]$pso.LockoutDuration.TotalMinutes } else { 'null' }
            $psoLockObs = if ($pso.LockoutObservationWindow) { [int]$pso.LockoutObservationWindow.TotalMinutes } else { 'null' }
            $sams = foreach ($sdn in $pso.AppliesTo) {
                try { $o = Get-ADObject -Identity $sdn -Properties SAMAccountName; $o.SAMAccountName } catch {}
            }
            $sams = @($sams | Where-Object { $_ })
            if ($sams.Count -gt 0) {
                $psoAppliesToJson = '[' + (($sams | ForEach-Object { '"' + (ConvertTo-JsonString $_) + '"' }) -join ',') + ']'
            }
        }
    }

    # Enfants (OUs/Containers + feuilles)
    $childParts = [System.Collections.Generic.List[string]]::new()

    try {
        $subOUs = Get-ADObject -Filter { (objectClass -eq 'organizationalUnit') -or (objectClass -eq 'container') -or (objectClass -eq 'builtinDomain') } `
                               -SearchBase $dn -SearchScope OneLevel `
                               -Properties WhenCreated,WhenChanged,Description,gpLink
        foreach ($c in $subOUs) {
            $childParts.Add((Build-NodeJson -dn $c.DistinguishedName -name $c.Name -simType (Get-SimType $c.ObjectClass) -adObj $c -depth ($depth+1)))
        }
    } catch {}

    try {
        $leaves = Get-ADObject -Filter { (objectClass -eq 'user') -or (objectClass -eq 'group') -or (objectClass -eq 'computer') -or (objectClass -eq 'msDS-GroupManagedServiceAccount') -or (objectClass -eq 'groupPolicyContainer') -or (objectClass -eq 'msDS-PasswordSettings') } `
                               -SearchBase $dn -SearchScope OneLevel `
                               -Properties SAMAccountName,MemberOf,WhenCreated,WhenChanged,Description,ObjectClass
        foreach ($l in $leaves) {
            $childParts.Add((Build-NodeJson -dn $l.DistinguishedName -name $l.Name -simType (Get-SimType $l.ObjectClass) -adObj $l -depth ($depth+1)))
        }
    } catch {}

    $childrenJson = if ($childParts.Count -gt 0) { '[' + ($childParts -join ',') + ']' } else { '[]' }

    return "{`"Id`":`"$id`",`"Name`":`"$(ConvertTo-JsonString $name)`",`"Type`":`"$simType`",`"Description`":`"$description`",`"DistinguishedName`":`"$(ConvertTo-JsonString $dn)`",`"Children`":$childrenJson,`"MemberOf`":$memberOfJson,`"LinkedGPOs`":$linkedGPOJson,`"PSOAppliesTo`":$psoAppliesToJson,`"PSOPrecedence`":$psoPrec,`"PSOMinPasswordLength`":$psoMinLen,`"PSOPasswordHistoryCount`":$psoHistory,`"PSOComplexityEnabled`":$psoComplex,`"PSOMaxPasswordAgeDays`":$psoMaxAge,`"PSOMinPasswordAgeDays`":$psoMinAge,`"PSOLockoutThreshold`":$psoLockThr,`"PSOLockoutDurationMinutes`":$psoLockDur,`"PSOLockoutObservationWindowMinutes`":$psoLockObs,`"CreatedDate`":`"$created`",`"ModifiedDate`":`"$modified`"}"
}

# ── 9. Racine Domain ─────────────────────────────────────────────────────────
Write-Host 'Construction de l arbre...' -ForegroundColor Yellow
$domainAdObj = Get-ADDomain
$rootJson    = Build-NodeJson -dn $DomainDN -name $domainName -simType 'Domain' -adObj $domainAdObj -depth 0

# ── 10. Ecriture ─────────────────────────────────────────────────────────────
$outDir = Split-Path $OutputPath -Parent
if ($outDir -and -not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }
$rootJson | Out-File -FilePath $OutputPath -Encoding UTF8 -Force

Write-Host ''
Write-Host '====================================' -ForegroundColor Green
Write-Host ' Export termine avec succes !'       -ForegroundColor Green
Write-Host " Fichier : $OutputPath"             -ForegroundColor Green
Write-Host '====================================' -ForegroundColor Green
Write-Host ' Importez ce fichier dans SMAD-X' -ForegroundColor Cyan
Write-Host ' via : Fichier > Ouvrir / File > Open'                -ForegroundColor Cyan
