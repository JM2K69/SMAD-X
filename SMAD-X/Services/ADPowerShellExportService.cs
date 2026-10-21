using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SMADX.Models;

namespace SMADX.Services
{
    /// <summary>
    /// Service pour exporter la structure AD en script PowerShell
    /// </summary>
    public class ADPowerShellExportService
    {
        /// <summary>
        /// Génère un script PowerShell pour créer la structure AD
        /// </summary>
        public async Task<bool> ExportToPowerShellAsync(ADObject root, string filePath)
        {
            try
            {
                var script = new StringBuilder();

                // En-tête du script
                script.AppendLine("# Script PowerShell généré par SMAD-X");
                script.AppendLine($"# Date de génération : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                script.AppendLine("# Ce script crée la structure Active Directory avec vérification d'existence");
                script.AppendLine();
                script.AppendLine("# Vérifier les permissions");
                script.AppendLine("if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {");
                script.AppendLine("    Write-Warning \"Ce script nécessite des privilèges administrateur AD\"");
                script.AppendLine("    exit");
                script.AppendLine("}");
                script.AppendLine();
                script.AppendLine("# Importer le module Active Directory");
                script.AppendLine("Import-Module ActiveDirectory -ErrorAction Stop");
                script.AppendLine();
                script.AppendLine("# Variables de configuration");
                script.AppendLine($"$DomainDN = \"{Esc(root.DistinguishedName)}\"");
                script.AppendLine("$ErrorCount = 0");
                script.AppendLine("$SuccessCount = 0");
                script.AppendLine();
                script.AppendLine("Write-Host \"========================================\" -ForegroundColor Cyan");
                script.AppendLine("Write-Host \"Création de la structure Active Directory\" -ForegroundColor Cyan");
                script.AppendLine("Write-Host \"========================================\" -ForegroundColor Cyan");
                script.AppendLine("Write-Host \"\"");
                script.AppendLine();

                // Fonction de vérification et création d'OU
                script.AppendLine("# Fonction pour créer une OU avec vérification");
                script.AppendLine("function New-ADOrganizationalUnitIfNotExists {");
                script.AppendLine("    param(");
                script.AppendLine("        [string]$Name,");
                script.AppendLine("        [string]$Path,");
                script.AppendLine("        [string]$Description = \"\"");
                script.AppendLine("    )");
                script.AppendLine();
                script.AppendLine("    $dn = \"OU=$Name,$Path\"");
                script.AppendLine("    ");
                script.AppendLine("    try {");
                script.AppendLine("        $ou = try { Get-ADOrganizationalUnit -Identity $dn -ErrorAction Stop } catch { $null }");
                script.AppendLine("        if ($ou) {");
                script.AppendLine("            Write-Host \"[EXISTE] OU: $Name\" -ForegroundColor Yellow");
                script.AppendLine("            return $true");
                script.AppendLine("        }");
                script.AppendLine("        else {");
                script.AppendLine("            $params = @{");
                script.AppendLine("                Name = $Name");
                script.AppendLine("                Path = $Path");
                script.AppendLine("            }");
                script.AppendLine("            if ($Description) {");
                script.AppendLine("                $params.Description = $Description");
                script.AppendLine("            }");
                script.AppendLine("            New-ADOrganizationalUnit @params");
                script.AppendLine("            Write-Host \"[CRÉÉ] OU: $Name\" -ForegroundColor Green");
                script.AppendLine("            $script:SuccessCount++");
                script.AppendLine("            return $true");
                script.AppendLine("        }");
                script.AppendLine("    }");
                script.AppendLine("    catch {");
                script.AppendLine("        Write-Host \"[ERREUR] OU: $Name - $_\" -ForegroundColor Red");
                script.AppendLine("        $script:ErrorCount++");
                script.AppendLine("        return $false");
                script.AppendLine("    }");
                script.AppendLine("}");
                script.AppendLine();

                // Fonction pour créer un utilisateur
                script.AppendLine("# Fonction pour créer un utilisateur avec vérification");
                script.AppendLine("function New-ADUserIfNotExists {");
                script.AppendLine("    param(");
                script.AppendLine("        [string]$SamAccountName,");
                script.AppendLine("        [string]$Name,");
                script.AppendLine("        [string]$Path,");
                script.AppendLine("        [string]$Description = \"\"");
                script.AppendLine("    )");
                script.AppendLine();
                script.AppendLine("    try {");
                script.AppendLine("        $user = try { Get-ADUser -Identity $SamAccountName -ErrorAction Stop } catch { $null }");
                script.AppendLine("        if ($user) {");
                script.AppendLine("            Write-Host \"[EXISTE] User: $SamAccountName\" -ForegroundColor Yellow");
                script.AppendLine("            return $true");
                script.AppendLine("        }");
                script.AppendLine("        else {");
                script.AppendLine("            $params = @{");
                script.AppendLine("                SamAccountName = $SamAccountName");
                script.AppendLine("                Name = $Name");
                script.AppendLine("                Path = $Path");
                script.AppendLine("                Enabled = $false");
                script.AppendLine("                AccountPassword = (ConvertTo-SecureString \"P@ssw0rd!\" -AsPlainText -Force)");
                script.AppendLine("            }");
                script.AppendLine("            if ($Description) {");
                script.AppendLine("                $params.Description = $Description");
                script.AppendLine("            }");
                script.AppendLine("            New-ADUser @params");
                script.AppendLine("            Write-Host \"[CRÉÉ] User: $SamAccountName\" -ForegroundColor Green");
                script.AppendLine("            $script:SuccessCount++");
                script.AppendLine("            return $true");
                script.AppendLine("        }");
                script.AppendLine("    }");
                script.AppendLine("    catch {");
                script.AppendLine("        Write-Host \"[ERREUR] User: $SamAccountName - $_\" -ForegroundColor Red");
                script.AppendLine("        $script:ErrorCount++");
                script.AppendLine("        return $false");
                script.AppendLine("    }");
                script.AppendLine("}");
                script.AppendLine();

                // Fonction pour créer un groupe
                script.AppendLine("# Fonction pour créer un groupe avec vérification");
                script.AppendLine("function New-ADGroupIfNotExists {");
                script.AppendLine("    param(");
                script.AppendLine("        [string]$Name,");
                script.AppendLine("        [string]$SamAccountName,");
                script.AppendLine("        [string]$Path,");
                script.AppendLine("        [string]$GroupScope = \"Global\",");
                script.AppendLine("        [string]$GroupCategory = \"Security\",");
                script.AppendLine("        [string]$Description = \"\"");
                script.AppendLine("    )");
                script.AppendLine();
                script.AppendLine("    try {");
                script.AppendLine("        $group = try { Get-ADGroup -Identity $SamAccountName -ErrorAction Stop } catch { $null }");
                script.AppendLine("        if ($group) {");
                script.AppendLine("            Write-Host \"[EXISTE] Group: $SamAccountName\" -ForegroundColor Yellow");
                script.AppendLine("            return $true");
                script.AppendLine("        }");
                script.AppendLine("        else {");
                script.AppendLine("            $params = @{");
                script.AppendLine("                Name = $Name");
                script.AppendLine("                SamAccountName = $SamAccountName");
                script.AppendLine("                Path = $Path");
                script.AppendLine("                GroupScope = $GroupScope");
                script.AppendLine("                GroupCategory = $GroupCategory");
                script.AppendLine("            }");
                script.AppendLine("            if ($Description) {");
                script.AppendLine("                $params.Description = $Description");
                script.AppendLine("            }");
                script.AppendLine("            New-ADGroup @params");
                script.AppendLine("            Write-Host \"[CRÉÉ] Group: $SamAccountName\" -ForegroundColor Green");
                script.AppendLine("            $script:SuccessCount++");
                script.AppendLine("            return $true");
                script.AppendLine("        }");
                script.AppendLine("    }");
                script.AppendLine("    catch {");
                script.AppendLine("        Write-Host \"[ERREUR] Group: $SamAccountName - $_\" -ForegroundColor Red");
                script.AppendLine("        $script:ErrorCount++");
                script.AppendLine("        return $false");
                script.AppendLine("    }");
                script.AppendLine("}");
                script.AppendLine();

                // Fonction pour créer un ordinateur
                script.AppendLine("# Fonction pour créer un ordinateur avec vérification");
                script.AppendLine("function New-ADComputerIfNotExists {");
                script.AppendLine("    param(");
                script.AppendLine("        [string]$Name,");
                script.AppendLine("        [string]$SamAccountName,");
                script.AppendLine("        [string]$Path,");
                script.AppendLine("        [string]$Description = \"\"");
                script.AppendLine("    )");
                script.AppendLine();
                script.AppendLine("    try {");
                script.AppendLine("        $computer = try { Get-ADComputer -Identity $SamAccountName -ErrorAction Stop } catch { $null }");
                script.AppendLine("        if ($computer) {");
                script.AppendLine("            Write-Host \"[EXISTE] Computer: $Name\" -ForegroundColor Yellow");
                script.AppendLine("            return $true");
                script.AppendLine("        }");
                script.AppendLine("        else {");
                script.AppendLine("            $params = @{");
                script.AppendLine("                Name = $Name");
                script.AppendLine("                SamAccountName = $SamAccountName");
                script.AppendLine("                Path = $Path");
                script.AppendLine("            }");
                script.AppendLine("            if ($Description) {");
                script.AppendLine("                $params.Description = $Description");
                script.AppendLine("            }");
                script.AppendLine("            New-ADComputer @params");
                script.AppendLine("            Write-Host \"[CRÉÉ] Computer: $Name\" -ForegroundColor Green");
                script.AppendLine("            $script:SuccessCount++");
                script.AppendLine("            return $true");
                script.AppendLine("        }");
                script.AppendLine("    }");
                script.AppendLine("    catch {");
                script.AppendLine("        Write-Host \"[ERREUR] Computer: $Name - $_\" -ForegroundColor Red");
                script.AppendLine("        $script:ErrorCount++");
                script.AppendLine("        return $false");
                script.AppendLine("    }");
                script.AppendLine("}");
                script.AppendLine();

                // Fonction pour ajouter un membre à un groupe avec vérification
                script.AppendLine("# Fonction pour ajouter un membre à un groupe avec vérification d'existence");
                script.AppendLine("function Add-ADGroupMemberIfNotMember {");
                script.AppendLine("    param(");
                script.AppendLine("        [string]$GroupName,");
                script.AppendLine("        [string]$MemberName");
                script.AppendLine("    )");
                script.AppendLine();
                script.AppendLine("    try {");
                script.AppendLine("        $group = try { Get-ADGroup -Identity $GroupName -ErrorAction Stop } catch { $null }");
                script.AppendLine("        if (-not $group) {");
                script.AppendLine("            Write-Host \"[ERREUR] Groupe '$GroupName' introuvable.\" -ForegroundColor Red");
                script.AppendLine("            $script:ErrorCount++");
                script.AppendLine("            return");
                script.AppendLine("        }");
                script.AppendLine("        $members = Get-ADGroupMember -Identity $GroupName -ErrorAction SilentlyContinue | Select-Object -ExpandProperty SamAccountName");
                script.AppendLine("        # Normalise : retire le $ final pour la comparaison");
                script.AppendLine("        $memberNameNorm = $MemberName.TrimEnd('$')");
                script.AppendLine("        $alreadyMember = $members | Where-Object { $_.TrimEnd('$') -eq $memberNameNorm }");
                script.AppendLine("        if ($alreadyMember) {");
                script.AppendLine("            Write-Host \"[EXISTE] $MemberName est déjà membre de $GroupName\" -ForegroundColor Yellow");
                script.AppendLine("            return");
                script.AppendLine("        }");
                script.AppendLine("        Add-ADGroupMember -Identity $GroupName -Members $MemberName -ErrorAction Stop");
                script.AppendLine("        Write-Host \"[RELATION] $MemberName → $GroupName\" -ForegroundColor Green");
                script.AppendLine("        $script:SuccessCount++");
                script.AppendLine("    }");
                script.AppendLine("    catch {");
                script.AppendLine("        Write-Host \"[ERREUR] Membership $MemberName → $GroupName : $_\" -ForegroundColor Red");
                script.AppendLine("        $script:ErrorCount++");
                script.AppendLine("    }");
                script.AppendLine("}");
                script.AppendLine();

                // Fonction pour créer une GPO si elle n'existe pas
                script.AppendLine("# Fonction pour créer une GPO si elle n'existe pas");
                script.AppendLine("function New-GPOIfNotExists {");
                script.AppendLine("    param(");
                script.AppendLine("        [string]$Name,");
                script.AppendLine("        [string]$Comment = \"\",");
                script.AppendLine("        [string]$Domain = $env:USERDNSDOMAIN");
                script.AppendLine("    )");
                script.AppendLine();
                script.AppendLine("    try {");
                script.AppendLine("        $gpo = try { Get-GPO -Name $Name -Domain $Domain -ErrorAction Stop } catch { $null }");
                script.AppendLine("        if ($gpo) {");
                script.AppendLine("            Write-Host \"[EXISTE] GPO: $Name\" -ForegroundColor Yellow");
                script.AppendLine("            return");
                script.AppendLine("        }");
                script.AppendLine("        $params = @{ Name = $Name; Domain = $Domain }");
                script.AppendLine("        if ($Comment) { $params.Comment = $Comment }");
                script.AppendLine("        New-GPO @params -ErrorAction Stop | Out-Null");
                script.AppendLine("        Write-Host \"[CRÉÉ] GPO: $Name\" -ForegroundColor Green");
                script.AppendLine("        $script:SuccessCount++");
                script.AppendLine("    }");
                script.AppendLine("    catch {");
                script.AppendLine("        Write-Host \"[ERREUR] GPO: $Name - $_\" -ForegroundColor Red");
                script.AppendLine("        $script:ErrorCount++");
                script.AppendLine("    }");
                script.AppendLine("}");
                script.AppendLine();

                // Fonction pour appliquer les attributs de sécurité d'un compte (strictement additive)
                script.AppendLine("# Fonction pour appliquer options de compte, SPN et délégation contrainte.");
                script.AppendLine("# Strictement additive : n'active que les options demandées et n'ajoute que les valeurs absentes,");
                script.AppendLine("# sans jamais rien retirer (les SPN d'un compte existant restent intacts).");
                script.AppendLine("function Set-ADAccountSecurityIfNeeded {");
                script.AppendLine("    param(");
                script.AppendLine("        [string]$Identity,");
                script.AppendLine("        [string]$DistinguishedName,");
                script.AppendLine("        [hashtable]$Flags = @{},");
                script.AppendLine("        [string[]]$Spns = @(),");
                script.AppendLine("        [string[]]$DelegateTo = @()");
                script.AppendLine("    )");
                script.AppendLine();
                script.AppendLine("    try {");
                script.AppendLine("        if ($Flags.Count -gt 0) {");
                script.AppendLine("            Set-ADAccountControl -Identity $Identity @Flags -ErrorAction Stop");
                script.AppendLine("        }");
                script.AppendLine("        $current = Get-ADObject -Identity $DistinguishedName -Properties servicePrincipalName,'msDS-AllowedToDelegateTo' -ErrorAction Stop");
                script.AppendLine("        $newSpns = @($Spns | Where-Object { $current.servicePrincipalName -notcontains $_ })");
                script.AppendLine("        if ($newSpns.Count -gt 0) {");
                script.AppendLine("            Set-ADObject -Identity $DistinguishedName -Add @{ servicePrincipalName = $newSpns } -ErrorAction Stop");
                script.AppendLine("        }");
                script.AppendLine("        $newTargets = @($DelegateTo | Where-Object { $current.'msDS-AllowedToDelegateTo' -notcontains $_ })");
                script.AppendLine("        if ($newTargets.Count -gt 0) {");
                script.AppendLine("            Set-ADObject -Identity $DistinguishedName -Add @{ 'msDS-AllowedToDelegateTo' = $newTargets } -ErrorAction Stop");
                script.AppendLine("        }");
                script.AppendLine("        Write-Host \"[SÉCURITÉ] $Identity\" -ForegroundColor Green");
                script.AppendLine("        $script:SuccessCount++");
                script.AppendLine("    }");
                script.AppendLine("    catch {");
                script.AppendLine("        Write-Host \"[ERREUR] Sécurité $Identity : $_\" -ForegroundColor Red");
                script.AppendLine("        $script:ErrorCount++");
                script.AppendLine("    }");
                script.AppendLine("}");
                script.AppendLine();

                // Générer les commandes de création
                script.AppendLine("# Création de la structure");
                script.AppendLine("Write-Host \"Début de la création de la structure...\" -ForegroundColor Cyan");
                script.AppendLine("Write-Host \"\"");
                script.AppendLine();

                // Générer les commandes récursivement
                GeneratePowerShellCommands(root, root.DistinguishedName, script);

                // Section relations : memberships, GPO links, PSO assignments
                script.AppendLine();
                script.AppendLine("# ========================================");
                script.AppendLine("# Relations : Memberships, GPO Links, PSO");
                script.AppendLine("# ========================================");
                script.AppendLine("Write-Host \"\"");
                script.AppendLine("Write-Host \"Configuration des relations...\" -ForegroundColor Cyan");
                script.AppendLine("Write-Host \"\"");
                script.AppendLine();

                GenerateRelationshipCommands(root, script);

                // Pied de page du script
                script.AppendLine();
                script.AppendLine("# Résumé");
                script.AppendLine("Write-Host \"\"");
                script.AppendLine("Write-Host \"========================================\" -ForegroundColor Cyan");
                script.AppendLine("Write-Host \"Résumé de l'exécution\" -ForegroundColor Cyan");
                script.AppendLine("Write-Host \"========================================\" -ForegroundColor Cyan");
                script.AppendLine("Write-Host \"Objets créés avec succès : $SuccessCount\" -ForegroundColor Green");
                script.AppendLine("Write-Host \"Erreurs rencontrées : $ErrorCount\" -ForegroundColor Red");
                script.AppendLine("Write-Host \"\"");
                script.AppendLine();
                script.AppendLine("if ($ErrorCount -eq 0) {");
                script.AppendLine("    Write-Host \"✓ Structure créée avec succès!\" -ForegroundColor Green");
                script.AppendLine("}");
                script.AppendLine("else {");
                script.AppendLine("    Write-Host \"⚠ Structure créée avec des erreurs\" -ForegroundColor Yellow");
                script.AppendLine("}");

                // Écrire le script dans le fichier
                // BOM requis : Windows PowerShell 5.1 lit sinon le script en ANSI et corrompt les accents
                await File.WriteAllTextAsync(filePath, script.ToString(), new UTF8Encoding(true));
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de l'export PowerShell : {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Génère les commandes PowerShell récursivement pour chaque objet
        /// </summary>
        private void GeneratePowerShellCommands(ADObject obj, string parentPath, StringBuilder script)
        {
            // Traiter les enfants (ne pas créer le domaine racine lui-même)
            foreach (var child in obj.Children.OrderBy(c => GetObjectPriority(c.Type)))
            {
                var description = SanitizeForPowerShell(child.Description);

                switch (child.Type)
                {
                    case ADObjectType.OrganizationalUnit:
                        script.AppendLine($"# OU: {child.Name}");
                        if (!string.IsNullOrWhiteSpace(child.Tier))
                        {
                            script.AppendLine($"# Tier: {child.Tier}");
                        }
                        script.AppendLine($"New-ADOrganizationalUnitIfNotExists -Name \"{Esc(child.Name)}\" -Path \"{Esc(parentPath)}\" -Description \"{description}\"");
                        script.AppendLine();

                        // Traiter les enfants de cette OU
                        GeneratePowerShellCommands(child, child.DistinguishedName, script);
                        break;

                    case ADObjectType.Container:
                        script.AppendLine($"# Container: {child.Name} (existant par défaut dans AD)");
                        if (!string.IsNullOrWhiteSpace(child.Tier))
                        {
                            script.AppendLine($"# Tier: {child.Tier}");
                        }
                        script.AppendLine($"# Les containers par défaut existent déjà: Users, Computers, Builtin, etc.");
                        script.AppendLine();

                        // Traiter les enfants de ce Container
                        GeneratePowerShellCommands(child, child.DistinguishedName, script);
                        break;

                    case ADObjectType.User:
                        script.AppendLine($"# User: {child.Name}");
                        if (!string.IsNullOrWhiteSpace(child.Tier))
                        {
                            script.AppendLine($"# Tier: {child.Tier}");
                        }
                        script.AppendLine($"New-ADUserIfNotExists -SamAccountName \"{Esc(child.Name)}\" -Name \"{Esc(child.Name)}\" -Path \"{Esc(parentPath)}\" -Description \"{description}\"");
                        script.AppendLine();
                        break;

                    case ADObjectType.Group:
                        script.AppendLine($"# Group: {child.Name}");
                        if (!string.IsNullOrWhiteSpace(child.Tier))
                        {
                            script.AppendLine($"# Tier: {child.Tier}");
                        }
                        var scope = child.GroupScope ?? ADGroupScope.Global;
                        var category = child.GroupCategory ?? ADGroupCategory.Security;
                        script.AppendLine($"New-ADGroupIfNotExists -Name \"{Esc(child.Name)}\" -SamAccountName \"{Esc(child.Name)}\" -Path \"{Esc(parentPath)}\" -GroupScope \"{scope}\" -GroupCategory \"{category}\" -Description \"{description}\"");
                        script.AppendLine();
                        break;

                    case ADObjectType.Computer:
                        script.AppendLine($"# Computer: {child.Name}");
                        if (!string.IsNullOrWhiteSpace(child.Tier))
                        {
                            script.AppendLine($"# Tier: {child.Tier}");
                        }
                        var computerSam = child.Name.EndsWith("$") ? child.Name : $"{child.Name}$";
                        script.AppendLine($"New-ADComputerIfNotExists -Name \"{Esc(child.Name)}\" -SamAccountName \"{Esc(computerSam)}\" -Path \"{Esc(parentPath)}\" -Description \"{description}\"");
                        script.AppendLine();
                        break;

                    case ADObjectType.GMSA:
                        script.AppendLine($"# GMSA: {child.Name} (création manuelle requise)");
                        script.AppendLine($"# New-ADServiceAccount -Name \"{child.Name}\" -DNSHostName \"{child.Name}.{GetDomainFromDN(parentPath)}\" -Path \"{parentPath}\"");
                        script.AppendLine();
                        break;

                    case ADObjectType.Policy:
                        script.AppendLine($"# GPO: {child.Name}");
                        if (!string.IsNullOrWhiteSpace(child.Tier))
                            script.AppendLine($"# Tier: {child.Tier}");
                        script.AppendLine($"New-GPOIfNotExists -Name \"{Esc(child.Name)}\" -Comment \"{description}\"");
                        script.AppendLine();
                        break;

                    case ADObjectType.PasswordSettingsObject:
                        script.AppendLine($"# PSO: {child.Name} (détecté - non recréé, les PSO sont gérés manuellement)");
                        if (!string.IsNullOrWhiteSpace(child.Tier))
                            script.AppendLine($"# Tier: {child.Tier}");
                        script.AppendLine($"# Pour créer manuellement : New-ADFineGrainedPasswordPolicy -Name \"{child.Name}\" -Precedence {child.PSOPrecedence ?? 10} ...");
                        script.AppendLine();
                        break;
                }
            }
        }

        /// <summary>
        /// Priorité de création des objets
        /// </summary>
        private int GetObjectPriority(ADObjectType type)
        {
            return type switch
            {
                ADObjectType.Container => 1,
                ADObjectType.OrganizationalUnit => 2,
                ADObjectType.PasswordSettingsObject => 3,
                ADObjectType.Group => 4,
                ADObjectType.User => 5,
                ADObjectType.Computer => 6,
                ADObjectType.GMSA => 7,
                ADObjectType.Policy => 8,
                _ => 99
            };
        }

        /// <summary>
        /// Nettoie une chaîne pour PowerShell (échappe les guillemets)
        /// </summary>
        private string SanitizeForPowerShell(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // Supprimer le markdown et les retours à la ligne
            var cleaned = input.Replace("\"", "'")
                               .Replace("\r\n", " ")
                               .Replace("\n", " ")
                               .Replace("#", "")
                               .Replace("**", "")
                               .Trim();

            // Limiter la longueur
            if (cleaned.Length > 200)
                cleaned = cleaned.Substring(0, 197) + "...";

            return Esc(cleaned);
        }

        /// <summary>
        /// Échappe une valeur insérée dans une chaîne PowerShell entre guillemets doubles
        /// (accent grave, $ et guillemets, y compris typographiques, sont interprétés par PowerShell)
        /// </summary>
        private static string Esc(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            var sb = new StringBuilder(input.Length);
            foreach (var c in input)
            {
                if (c is '`' or '$' or '"' or '“' or '”' or '„')
                    sb.Append('`');
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Génère les commandes de relations : memberships, liens GPO sur OU, et application PSO
        /// </summary>
        private void GenerateRelationshipCommands(ADObject root, StringBuilder script)
        {
            var allObjects = new List<ADObject>();
            CollectAllObjects(root, allObjects);

            // 1. Memberships : User/Group → Group
            var memberships = allObjects
                .Where(o => (o.Type == ADObjectType.User || o.Type == ADObjectType.Group || o.Type == ADObjectType.Computer) && o.MemberOf.Count > 0)
                .ToList();

            if (memberships.Count > 0)
            {
                script.AppendLine("# --- Appartenances aux groupes (MemberOf) ---");
                script.AppendLine("Write-Host \"Configuration des appartenances aux groupes...\" -ForegroundColor Cyan");
                script.AppendLine();
                foreach (var obj in memberships)
                {
                    foreach (var groupName in obj.MemberOf)
                    {
                        // Les ordinateurs utilisent leur SAMAccountName avec $ pour Add-ADGroupMember
                        var memberRef = obj.Type == ADObjectType.Computer
                            ? (obj.Name.EndsWith("$") ? obj.Name : $"{obj.Name}$")
                            : obj.Name;
                        script.AppendLine($"# Ajout de {obj.Name} dans le groupe {groupName}");
                        script.AppendLine($"Add-ADGroupMemberIfNotMember -GroupName \"{Esc(groupName)}\" -MemberName \"{Esc(memberRef)}\"");
                        script.AppendLine();
                    }
                }
            }

            // 2. Liens GPO sur OU
            var ouWithGPO = allObjects
                .Where(o => o.Type == ADObjectType.OrganizationalUnit && o.LinkedGPOs.Count > 0)
                .ToList();

            if (ouWithGPO.Count > 0)
            {
                script.AppendLine("# --- Liens GPO sur les OU ---");
                script.AppendLine("Write-Host \"Liaison des GPO aux OU...\" -ForegroundColor Cyan");
                script.AppendLine();
                foreach (var ou in ouWithGPO)
                {
                    foreach (var gpoName in ou.LinkedGPOs)
                    {
                        script.AppendLine($"# Lier la GPO '{gpoName}' à l'OU '{ou.Name}'");
                        script.AppendLine("try {");
                        script.AppendLine($"    $gpo = try {{ Get-GPO -Name \"{Esc(gpoName)}\" -ErrorAction Stop }} catch {{ $null }}");
                        script.AppendLine("    if (-not $gpo) {");
                        script.AppendLine($"        Write-Host \"[IGNORÉ] GPO '{Esc(gpoName)}' introuvable, lien ignoré.\" -ForegroundColor Yellow");
                        script.AppendLine("    } else {");
                        script.AppendLine($"        New-GPLink -Name \"{Esc(gpoName)}\" -Target \"{Esc(ou.DistinguishedName)}\" -LinkEnabled Yes -ErrorAction Stop");
                        script.AppendLine($"        Write-Host \"[GPO LINK] {Esc(gpoName)} → {Esc(ou.DistinguishedName)}\" -ForegroundColor Green");
                        script.AppendLine("        $script:SuccessCount++");
                        script.AppendLine("    }");
                        script.AppendLine("}");
                        script.AppendLine("catch {");
                        script.AppendLine($"    Write-Host \"[ERREUR] GPO Link {Esc(gpoName)} → {Esc(ou.Name)} : $_\" -ForegroundColor Red");
                        script.AppendLine("    $script:ErrorCount++");
                        script.AppendLine("}");
                        script.AppendLine();
                    }
                }
            }

            // 3. Attributs de sécurité des comptes
            GenerateAccountSecurityCommands(allObjects, script);

            // 4. Application des PSO aux utilisateurs/groupes
            var psoObjects = allObjects
                .Where(o => o.Type == ADObjectType.PasswordSettingsObject && o.PSOAppliesTo.Count > 0)
                .ToList();

            if (psoObjects.Count > 0)
            {
                script.AppendLine("# --- Application des PSO aux utilisateurs/groupes ---");
                script.AppendLine("Write-Host \"Application des PSO...\" -ForegroundColor Cyan");
                script.AppendLine();
                foreach (var pso in psoObjects)
                {
                    foreach (var target in pso.PSOAppliesTo)
                    {
                        script.AppendLine($"# Appliquer le PSO '{pso.Name}' à '{target}'");
                        script.AppendLine("try {");
                        script.AppendLine($"    Add-ADFineGrainedPasswordPolicySubject -Identity \"{Esc(pso.Name)}\" -Subjects \"{Esc(target)}\" -ErrorAction Stop");
                        script.AppendLine($"    Write-Host \"[PSO] {Esc(pso.Name)} → {Esc(target)}\" -ForegroundColor Green");
                        script.AppendLine("    $script:SuccessCount++");
                        script.AppendLine("}");
                        script.AppendLine("catch {");
                        script.AppendLine($"    Write-Host \"[ERREUR] PSO {Esc(pso.Name)} → {Esc(target)} : $_\" -ForegroundColor Red");
                        script.AppendLine("    $script:ErrorCount++");
                        script.AppendLine("}");
                        script.AppendLine();
                    }
                }
            }
        }

        /// <summary>Paramètre de Set-ADAccountControl correspondant à chaque option de compte exportée.</summary>
        private static readonly (ADAccountFlags Flag, string Parameter)[] AccountControlParameters =
        {
            (ADAccountFlags.PasswordNotRequired,        "PasswordNotRequired"),
            (ADAccountFlags.PasswordNeverExpires,       "PasswordNeverExpires"),
            (ADAccountFlags.DontRequirePreauth,         "DoesNotRequirePreAuth"),
            (ADAccountFlags.TrustedForDelegation,       "TrustedForDelegation"),
            (ADAccountFlags.TrustedToAuthForDelegation, "TrustedToAuthForDelegation"),
            (ADAccountFlags.NotDelegated,               "AccountNotDelegated")
        };

        /// <summary>
        /// Options de compte, SPN et délégation contrainte des utilisateurs et ordinateurs créés par le script.
        /// Exclus : krbtgt et les contrôleurs de domaine (objets critiques déjà configurés par AD), les gMSA
        /// (créés manuellement), l'état « désactivé » (les comptes sont toujours créés désactivés),
        /// adminCount (géré par SDProp) et le propriétaire (dépend du compte qui exécute le script).
        /// </summary>
        private void GenerateAccountSecurityCommands(List<ADObject> allObjects, StringBuilder script)
        {
            var accounts = allObjects
                .Where(o => o.Type is ADObjectType.User or ADObjectType.Computer)
                .Where(o => o.Name != "krbtgt" && o.Parent is not { Type: ADObjectType.OrganizationalUnit, Name: "Domain Controllers" })
                .Select(o => (Account: o, Parameters: AccountControlParameters.Where(p => o.AccountFlags.HasFlag(p.Flag)).Select(p => p.Parameter).ToList()))
                .Where(a => a.Parameters.Count > 0 || a.Account.ServicePrincipalNames.Count > 0 || a.Account.AllowedToDelegateTo.Count > 0)
                .ToList();

            if (accounts.Count == 0)
                return;

            script.AppendLine("# --- Attributs de sécurité des comptes (options, SPN, délégation contrainte) ---");
            script.AppendLine("Write-Host \"Application des attributs de sécurité...\" -ForegroundColor Cyan");
            script.AppendLine();
            foreach (var (account, parameters) in accounts)
            {
                var identity = account.Type == ADObjectType.Computer && !account.Name.EndsWith('$') ? $"{account.Name}$" : account.Name;
                var flags = parameters.Count > 0 ? "@{ " + string.Join("; ", parameters.Select(p => $"{p} = $true")) + " }" : "@{}";

                script.AppendLine($"# Sécurité de {account.Name}");
                script.AppendLine($"Set-ADAccountSecurityIfNeeded -Identity \"{Esc(identity)}\" -DistinguishedName \"{Esc(account.DistinguishedName)}\" `");
                script.AppendLine($"    -Flags {flags} `");
                script.AppendLine($"    -Spns {PsArray(account.ServicePrincipalNames)} `");
                script.AppendLine($"    -DelegateTo {PsArray(account.AllowedToDelegateTo)}");
                script.AppendLine();
            }
        }

        /// <summary>Tableau PowerShell de chaînes échappées, ex. @("HTTP/web", "HOST/web").</summary>
        private static string PsArray(IEnumerable<string> values) =>
            "@(" + string.Join(", ", values.Select(v => $"\"{Esc(v)}\"")) + ")";

        /// <summary>
        /// Collecte récursivement tous les objets de l'arbre
        /// </summary>
        private static void CollectAllObjects(ADObject obj, List<ADObject> result)
        {
            result.Add(obj);
            foreach (var child in obj.Children)
                CollectAllObjects(child, result);
        }

        /// <summary>
        /// Extrait le nom de domaine depuis un DN
        /// </summary>
        private string GetDomainFromDN(string dn)
        {
            var dcParts = dn.Split(',')
                            .Where(p => p.Trim().StartsWith("DC=", StringComparison.OrdinalIgnoreCase))
                            .Select(p => p.Substring(3));
            return string.Join(".", dcParts);
        }
    }
}
