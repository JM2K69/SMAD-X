using System;
using System.Linq;

namespace SMADX.Services
{
    /// <summary>
    /// Service de validation pour les objets AD
    /// </summary>
    public class ADValidationService
    {
        /// <summary>
        /// Valide le nom d'un objet AD
        /// </summary>
        public bool ValidateName(string name, out string? errorMessage)
        {
            var loc = LocalizationService.Instance;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(name))
            {
                errorMessage = loc["Validation.Name.Empty"];
                return false;
            }

            if (name.Length > 64)
            {
                errorMessage = loc["Validation.Name.TooLong"];
                return false;
            }

            // Un saut de ligne ou une tabulation dans un nom ne s'affiche pas dans l'arbre
            if (name.Any(char.IsControl))
            {
                errorMessage = loc["Validation.Name.Control"];
                return false;
            }

            // Caractères interdits dans les noms AD
            char[] invalidChars = { '/', '\\', '[', ']', ':', ';', '|', '=', ',', '+', '*', '?', '<', '>', '"' };

            foreach (var invalidChar in invalidChars)
            {
                if (name.Contains(invalidChar))
                {
                    errorMessage = string.Format(loc["Validation.Name.InvalidChar"], invalidChar);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Valide si un objet peut être ajouté dans un conteneur
        /// </summary>
        public bool ValidateContainerRule(Models.ADObjectType parentType, Models.ADObjectType childType, out string? errorMessage)
        {
            var loc = LocalizationService.Instance;
            errorMessage = null;

            switch (parentType)
            {
                case Models.ADObjectType.Domain:
                    // Un domaine peut contenir des OUs, Containers et Policies (pas les PSOs directement)
                    if (childType != Models.ADObjectType.OrganizationalUnit && 
                        childType != Models.ADObjectType.Container &&
                        childType != Models.ADObjectType.Policy)
                    {
                        errorMessage = string.Format(loc["Validation.Container.Domain"], loc[$"ObjectType.{childType}"]);
                        return false;
                    }
                    break;

                case Models.ADObjectType.OrganizationalUnit:
                    // Une OU peut contenir presque tout sauf un domaine, PSO et Container
                    if (childType == Models.ADObjectType.Domain || 
                        childType == Models.ADObjectType.PasswordSettingsObject ||
                        childType == Models.ADObjectType.Container)
                    {
                        errorMessage = string.Format(loc["Validation.Container.OU"], loc[$"ObjectType.{childType}"]);
                        return false;
                    }
                    break;

                case Models.ADObjectType.Container:
                    // Un Container peut contenir des Users, Groups, Computers, et PSOs (pour le container System)
                    if (childType == Models.ADObjectType.Domain || 
                        childType == Models.ADObjectType.OrganizationalUnit ||
                        childType == Models.ADObjectType.Container ||
                        childType == Models.ADObjectType.Policy)
                    {
                        errorMessage = string.Format(loc["Validation.Container.Container"], loc[$"ObjectType.{childType}"]);
                        return false;
                    }
                    break;

                case Models.ADObjectType.User:
                case Models.ADObjectType.Computer:
                case Models.ADObjectType.Group:
                case Models.ADObjectType.GMSA:
                case Models.ADObjectType.Policy:
                case Models.ADObjectType.PasswordSettingsObject:
                    // Ces types ne peuvent pas contenir d'autres objets
                    errorMessage = string.Format(loc["Validation.Container.Leaf"], loc[$"ObjectType.{parentType}"]);
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Suggère un nom basé sur des conventions
        /// </summary>
        public string SuggestName(Models.ADObjectType type, string? baseName = null)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

            return type switch
            {
                Models.ADObjectType.Domain => baseName ?? "domain.local",
                Models.ADObjectType.OrganizationalUnit => baseName ?? "NewOU",
                Models.ADObjectType.Container => baseName ?? "NewContainer",
                Models.ADObjectType.User => baseName ?? $"user{timestamp}",
                Models.ADObjectType.Group => baseName ?? $"group{timestamp}",
                Models.ADObjectType.Computer => baseName ?? $"COMPUTER{timestamp}",
                Models.ADObjectType.GMSA => baseName ?? $"svc-{timestamp}",
                Models.ADObjectType.Policy => baseName ?? "New Policy",
                Models.ADObjectType.PasswordSettingsObject => baseName ?? "PSO-Default",
                _ => baseName ?? $"Object{timestamp}"
            };
        }
    }
}
