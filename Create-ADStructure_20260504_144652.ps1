# Script PowerShell généré par MockAD
# Date de génération : 2026-05-04 14:46:56
# Ce script crée la structure Active Directory avec vérification d'existence

# Vérifier les permissions
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "Ce script nécessite des privilèges administrateur AD"
    exit
}

# Importer le module Active Directory
Import-Module ActiveDirectory -ErrorAction Stop

# Variables de configuration
$DomainDN = "DC=contoso,DC=com"
$ErrorCount = 0
$SuccessCount = 0

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Création de la structure Active Directory" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Fonction pour créer une OU avec vérification
function New-ADOrganizationalUnitIfNotExists {
    param(
        [string]$Name,
        [string]$Path,
        [string]$Description = ""
    )

    $dn = "OU=$Name,$Path"
    
    try {
        $ou = Get-ADOrganizationalUnit -Identity $dn -ErrorAction SilentlyContinue
        if ($ou) {
            Write-Host "[EXISTE] OU: $Name" -ForegroundColor Yellow
            return $true
        }
        else {
            $params = @{
                Name = $Name
                Path = $Path
            }
            if ($Description) {
                $params.Description = $Description
            }
            New-ADOrganizationalUnit @params
            Write-Host "[CRÉÉ] OU: $Name" -ForegroundColor Green
            $script:SuccessCount++
            return $true
        }
    }
    catch {
        Write-Host "[ERREUR] OU: $Name - $_" -ForegroundColor Red
        $script:ErrorCount++
        return $false
    }
}

# Fonction pour créer un utilisateur avec vérification
function New-ADUserIfNotExists {
    param(
        [string]$SamAccountName,
        [string]$Name,
        [string]$Path,
        [string]$Description = ""
    )

    try {
        $user = Get-ADUser -Identity $SamAccountName -ErrorAction SilentlyContinue
        if ($user) {
            Write-Host "[EXISTE] User: $SamAccountName" -ForegroundColor Yellow
            return $true
        }
        else {
            $params = @{
                SamAccountName = $SamAccountName
                Name = $Name
                Path = $Path
                Enabled = $false
                AccountPassword = (ConvertTo-SecureString "P@ssw0rd!" -AsPlainText -Force)
            }
            if ($Description) {
                $params.Description = $Description
            }
            New-ADUser @params
            Write-Host "[CRÉÉ] User: $SamAccountName" -ForegroundColor Green
            $script:SuccessCount++
            return $true
        }
    }
    catch {
        Write-Host "[ERREUR] User: $SamAccountName - $_" -ForegroundColor Red
        $script:ErrorCount++
        return $false
    }
}

# Fonction pour créer un groupe avec vérification
function New-ADGroupIfNotExists {
    param(
        [string]$Name,
        [string]$SamAccountName,
        [string]$Path,
        [string]$GroupScope = "Global",
        [string]$GroupCategory = "Security",
        [string]$Description = ""
    )

    try {
        $group = Get-ADGroup -Identity $SamAccountName -ErrorAction SilentlyContinue
        if ($group) {
            Write-Host "[EXISTE] Group: $SamAccountName" -ForegroundColor Yellow
            return $true
        }
        else {
            $params = @{
                Name = $Name
                SamAccountName = $SamAccountName
                Path = $Path
                GroupScope = $GroupScope
                GroupCategory = $GroupCategory
            }
            if ($Description) {
                $params.Description = $Description
            }
            New-ADGroup @params
            Write-Host "[CRÉÉ] Group: $SamAccountName" -ForegroundColor Green
            $script:SuccessCount++
            return $true
        }
    }
    catch {
        Write-Host "[ERREUR] Group: $SamAccountName - $_" -ForegroundColor Red
        $script:ErrorCount++
        return $false
    }
}

# Fonction pour créer un ordinateur avec vérification
function New-ADComputerIfNotExists {
    param(
        [string]$Name,
        [string]$SamAccountName,
        [string]$Path,
        [string]$Description = ""
    )

    try {
        $computer = Get-ADComputer -Identity $SamAccountName -ErrorAction SilentlyContinue
        if ($computer) {
            Write-Host "[EXISTE] Computer: $SamAccountName" -ForegroundColor Yellow
            return $true
        }
        else {
            $params = @{
                Name = $Name
                SamAccountName = $SamAccountName
                Path = $Path
            }
            if ($Description) {
                $params.Description = $Description
            }
            New-ADComputer @params
            Write-Host "[CRÉÉ] Computer: $SamAccountName" -ForegroundColor Green
            $script:SuccessCount++
            return $true
        }
    }
    catch {
        Write-Host "[ERREUR] Computer: $SamAccountName - $_" -ForegroundColor Red
        $script:ErrorCount++
        return $false
    }
}

# Création de la structure
Write-Host "Début de la création de la structure..." -ForegroundColor Cyan
Write-Host ""

# OU: Admin
# Tier: Tier 0
New-ADOrganizationalUnitIfNotExists -Name "Admin" -Path "DC=contoso,DC=com" -Description "OU Administration  Contient les comptes administrateurs Tier 0."

# Group: Domain Admins
# Tier: Tier 0
New-ADGroupIfNotExists -Name "Domain Admins" -SamAccountName "Domain Admins" -Path "OU=Admin,DC=contoso,DC=com" -Description "Groupe des administrateurs de domaine"

# User: Administrator
# Tier: Tier 0
New-ADUserIfNotExists -SamAccountName "Administrator" -Name "Administrator" -Path "OU=Admin,DC=contoso,DC=com" -Description "Compte administrateur principal"

# OU: Servers
# Tier: Tier 1
New-ADOrganizationalUnitIfNotExists -Name "Servers" -Path "DC=contoso,DC=com" -Description "Serveurs d'infrastructure  - Serveurs de fichiers - Serveurs d'applications"

# Computer: SRV-FILE-01
# Tier: Tier 1
New-ADComputerIfNotExists -Name "SRV-FILE-01" -SamAccountName "SRV-FILE-01$" -Path "OU=Servers,DC=contoso,DC=com" -Description "Serveur de fichiers principal"

# Computer: SRV-APP-01
# Tier: Tier 1
New-ADComputerIfNotExists -Name "SRV-APP-01" -SamAccountName "SRV-APP-01$" -Path "OU=Servers,DC=contoso,DC=com" -Description "Serveur d'applications"

# GMSA: svc-webapp (création manuelle requise)
# New-ADServiceAccount -Name "svc-webapp" -DNSHostName "svc-webapp.contoso.com" -Path "OU=Servers,DC=contoso,DC=com"

# OU: Users
# Tier: Tier 2
New-ADOrganizationalUnitIfNotExists -Name "Users" -Path "DC=contoso,DC=com" -Description "Utilisateurs de l'organisation"

# User: jdoe
# Tier: Tier 2
New-ADUserIfNotExists -SamAccountName "jdoe" -Name "jdoe" -Path "OU=Users,DC=contoso,DC=com" -Description "John Doe - Développeur"

# User: asmith
# Tier: Tier 2
New-ADUserIfNotExists -SamAccountName "asmith" -Name "asmith" -Path "OU=Users,DC=contoso,DC=com" -Description "Alice Smith - Manager"

# OU: Workstations
# Tier: Tier 2
New-ADOrganizationalUnitIfNotExists -Name "Workstations" -Path "DC=contoso,DC=com" -Description "Postes de travail des utilisateurs"

# Computer: WKS-001
# Tier: Tier 2
New-ADComputerIfNotExists -Name "WKS-001" -SamAccountName "WKS-001$" -Path "OU=Workstations,DC=contoso,DC=com" -Description "Poste de John Doe"

# OU: Policies
# Tier: Tier 0
New-ADOrganizationalUnitIfNotExists -Name "Policies" -Path "DC=contoso,DC=com" -Description "Stratégies de groupe"

# GPO: Password Policy (création manuelle requise via GPMC)
# New-GPO -Name "Password Policy" -Comment "Stratégie de mot de passe : complexité requise, 90 jours d'expiration"


# Résumé
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Résumé de l'exécution" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Objets créés avec succès : $SuccessCount" -ForegroundColor Green
Write-Host "Erreurs rencontrées : $ErrorCount" -ForegroundColor Red
Write-Host ""

if ($ErrorCount -eq 0) {
    Write-Host "✓ Structure créée avec succès!" -ForegroundColor Green
}
else {
    Write-Host "⚠ Structure créée avec des erreurs" -ForegroundColor Yellow
}
