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

        /// <summary>Conteneur des GPO : CN=Policies,CN=System.</summary>
        public static bool IsPoliciesContainer(Models.ADObject o) =>
            o is { Type: Models.ADObjectType.Container, Name: "Policies", Parent: { Type: Models.ADObjectType.Container, Name: "System" } };

        /// <summary>Conteneur des PSO : CN=Password Settings Container,CN=System.</summary>
        public static bool IsPasswordSettingsContainer(Models.ADObject o) =>
            o is { Type: Models.ADObjectType.Container, Name: "Password Settings Container" };

        /// <summary>
        /// Vrai si un objet de ce type peut être créé sous <paramref name="parent"/>, comme dans AD (supérieurs possibles du
        /// schéma) : une OU sous le domaine ou une OU, jamais dans un conteneur ; un conteneur, un compte ou un groupe sous le
        /// domaine, une OU ou un conteneur ; une GPO seulement dans CN=Policies,CN=System ; un PSO seulement dans le
        /// Password Settings Container ; rien sous un compte, un groupe, une GPO ou un PSO.
        /// </summary>
        public bool ValidateContainerRule(Models.ADObject parent, Models.ADObjectType childType, out string? errorMessage)
        {
            var loc = LocalizationService.Instance;
            errorMessage = null;
            string Type(Models.ADObjectType t) => loc[$"ObjectType.{t}"];

            if (parent.Type is not (Models.ADObjectType.Domain or Models.ADObjectType.OrganizationalUnit or Models.ADObjectType.Container))
            {
                errorMessage = string.Format(loc["Validation.Container.Leaf"], Type(parent.Type));
                return false;
            }

            switch (childType)
            {
                case Models.ADObjectType.Domain:
                    errorMessage = loc["Validation.Container.NoDomain"];
                    return false;
                case Models.ADObjectType.OrganizationalUnit when parent.Type == Models.ADObjectType.Container:
                    errorMessage = loc["Validation.Container.OuInContainer"];
                    return false;
                case Models.ADObjectType.Policy when !IsPoliciesContainer(parent):
                    errorMessage = loc["Validation.Container.PolicyPlace"];
                    return false;
                case Models.ADObjectType.PasswordSettingsObject when !IsPasswordSettingsContainer(parent):
                    errorMessage = loc["Validation.Container.PsoPlace"];
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Vrai si <paramref name="member"/> peut entrer dans <paramref name="group"/>, selon les portées d'AD (AGDLP) :
        /// un groupe global ne contient que des comptes et des groupes globaux ; un groupe universel, aussi des groupes
        /// universels ; un groupe de domaine local, aussi des groupes de domaine local. Une portée inconnue n'est pas vérifiée.
        /// </summary>
        public bool ValidateMembership(Models.ADObject member, Models.ADObject group, out string? errorMessage)
        {
            var loc = LocalizationService.Instance;
            errorMessage = null;
            if (group.Type != Models.ADObjectType.Group)
            {
                errorMessage = string.Format(loc["Validation.Member.NotAGroup"], group.Name);
                return false;
            }
            if (member == group)
            {
                errorMessage = loc["Relations.Msg.SelfMember"];
                return false;
            }
            if (member.Type is not (Models.ADObjectType.User or Models.ADObjectType.Computer or Models.ADObjectType.GMSA or Models.ADObjectType.Group))
            {
                errorMessage = string.Format(loc["Validation.Member.Type"], loc[$"ObjectType.{member.Type}"]);
                return false;
            }
            if (member.Type != Models.ADObjectType.Group || member.GroupScope is not { } inner || group.GroupScope is not { } outer)
                return true;

            var allowed = outer switch
            {
                Models.ADGroupScope.Global => inner == Models.ADGroupScope.Global,
                Models.ADGroupScope.Universal => inner != Models.ADGroupScope.DomainLocal,
                _ => true
            };
            if (!allowed)
                errorMessage = string.Format(loc["Validation.Member.Scope"], member.Name, loc[$"GroupScope.{inner}"], group.Name, loc[$"GroupScope.{outer}"]);
            return allowed;
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
