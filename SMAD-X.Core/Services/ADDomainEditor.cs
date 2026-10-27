using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SMADX.Models;

namespace SMADX.Services
{
    /// <summary>
    /// Modifications du domaine qui gardent ses références cohérentes. AD relie les objets par SID et par DN ;
    /// SMAD-X les relie par nom : appartenances (MemberOf), sujets des PSO, GPO liées aux OU et aux sites,
    /// propriétaires, mandataires des délégations et contrôleurs de domaine des sites. Renommer un objet
    /// réécrit ces références et le supprimer les retire, comme le ferait l'annuaire.
    /// </summary>
    public static class ADDomainEditor
    {
        private static LocalizationService Loc => LocalizationService.Instance;

        private static string Rdn(ADObjectType type, string name) =>
            (type == ADObjectType.OrganizationalUnit ? "OU=" : "CN=") + name;

        /// <summary>Vrai si un autre enfant de <paramref name="parent"/> aurait le même DN.</summary>
        public static bool HasSiblingConflict(ADObject parent, ADObjectType type, string name, ADObject? except = null) =>
            parent.Children.Any(c => c != except && string.Equals(Rdn(c.Type, c.Name), Rdn(type, name), StringComparison.OrdinalIgnoreCase));

        /// <summary>Nom libre parmi les enfants de <paramref name="parent"/> : « NewOU », puis « NewOU 2 », « NewOU 3 »…</summary>
        public static string UniqueChildName(ADObject parent, ADObjectType type, string baseName, ADObject? except = null)
        {
            var name = baseName;
            for (var i = 2; HasSiblingConflict(parent, type, name, except); i++)
                name = $"{baseName} {i}";
            return name;
        }

        /// <summary>
        /// Renomme un objet : nom valide et libre parmi ses frères, DN de tout son sous-arbre recalculés,
        /// références par nom réécrites. Renvoie le message d'erreur, ou null si le renommage est fait.
        /// </summary>
        public static string? Rename(ADObject root, ADObject obj, string newName, ADSitesTopology? sites, out int references)
        {
            references = 0;
            newName = newName.Trim();
            if (!new ADValidationService().ValidateName(newName, out var error))
                return error;
            if (obj.Parent != null && HasSiblingConflict(obj.Parent, obj.Type, newName, obj))
                return string.Format(Loc["Validation.Name.Duplicate"], newName);

            var oldName = obj.Name;
            if (oldName == newName)
                return null;

            obj.Name = newName;
            obj.ModifiedDate = DateTime.Now;
            obj.UpdateDistinguishedNameTree();
            references = RewriteReferences(root, obj, oldName, newName, sites);
            return null;
        }

        /// <summary>
        /// Retire les références aux objets d'un sous-arbre supprimé, déjà retiré du domaine : appartenances,
        /// sujets des PSO, GPO liées, mandataires des délégations et propriétaires. Un nom encore porté par un
        /// autre objet du même genre est gardé. Renvoie le nombre de références retirées.
        /// </summary>
        public static int RemoveReferences(ADObject root, ADObject deleted, ADSitesTopology? sites)
        {
            var remaining = Flatten(root).ToList();
            var gone = Flatten(deleted).ToList();
            HashSet<string> Names(Func<ADObjectType, bool> kind) =>
                gone.Where(o => kind(o.Type)).Select(o => o.Name)
                    .Where(n => !remaining.Any(o => kind(o.Type) && Same(o.Name, n)))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var groups = Names(t => t == ADObjectType.Group);
            var principals = Names(IsPrincipal);
            var policies = Names(t => t == ADObjectType.Policy);
            var computers = Names(t => t == ADObjectType.Computer);

            var count = 0;
            foreach (var o in remaining)
            {
                count += RemoveAll(o.MemberOf, groups);
                count += RemoveAll(o.PSOAppliesTo, principals);
                count += RemoveAll(o.LinkedGPOs, policies);
                if (o.Owner != null && principals.Contains(o.Owner))
                {
                    o.Owner = null;
                    count++;
                }
                foreach (var d in o.Delegations.Where(d => principals.Contains(d.TrusteeName)).ToList())
                {
                    o.Delegations.Remove(d);
                    count++;
                }
            }
            foreach (var site in sites?.Sites ?? Enumerable.Empty<ADSite>())
            {
                count += RemoveAll(site.LinkedGPOs, policies);
                foreach (var dc in site.DomainControllers.Where(dc => computers.Contains(HostName(dc))).ToList())
                {
                    site.DomainControllers.Remove(dc);
                    count++;
                }
            }
            return count;
        }

        private static int RewriteReferences(ADObject root, ADObject obj, string oldName, string newName, ADSitesTopology? sites)
        {
            // Un autre objet du même genre porte encore l'ancien nom : ses références restent les siennes
            var namesakes = Flatten(root).Where(o => o != obj && Same(o.Name, oldName)).ToList();
            var group = obj.Type == ADObjectType.Group && !namesakes.Any(o => o.Type == ADObjectType.Group);
            var principal = IsPrincipal(obj.Type) && !namesakes.Any(o => IsPrincipal(o.Type));
            var policy = obj.Type == ADObjectType.Policy && !namesakes.Any(o => o.Type == ADObjectType.Policy);
            var computer = obj.Type == ADObjectType.Computer && principal;

            var count = 0;
            foreach (var o in Flatten(root))
            {
                if (group) count += Replace(o.MemberOf, oldName, newName);
                if (policy) count += Replace(o.LinkedGPOs, oldName, newName);
                if (!principal) continue;
                count += Replace(o.PSOAppliesTo, oldName, newName);
                if (o.Owner != null && Same(o.Owner, oldName))
                {
                    o.Owner = newName;
                    count++;
                }
                foreach (var d in o.Delegations.Where(d => Same(d.TrusteeName, oldName)))
                {
                    d.TrusteeName = newName;
                    count++;
                }
            }
            foreach (var site in sites?.Sites ?? Enumerable.Empty<ADSite>())
            {
                if (policy) count += Replace(site.LinkedGPOs, oldName, newName);
                if (!computer) continue;
                for (var i = 0; i < site.DomainControllers.Count; i++)
                {
                    var dc = site.DomainControllers[i];
                    if (!Same(HostName(dc), oldName)) continue;
                    site.DomainControllers[i] = newName + dc[HostName(dc).Length..];
                    count++;
                }
            }
            return count;
        }

        private static bool IsPrincipal(ADObjectType type) =>
            type is ADObjectType.User or ADObjectType.Group or ADObjectType.Computer or ADObjectType.GMSA;

        private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        /// <summary>« DC01 » pour « DC01.contoso.com » : les sites nomment leurs DC par leur nom DNS.</summary>
        private static string HostName(string dnsName) => dnsName.Split('.')[0];

        private static int Replace(ObservableCollection<string> names, string oldName, string newName)
        {
            var count = 0;
            for (var i = names.Count - 1; i >= 0; i--)
            {
                if (!Same(names[i], oldName)) continue;
                // Le nouveau nom y est déjà : pas de doublon
                var index = i;
                if (names.Where((n, j) => j != index && Same(n, newName)).Any()) names.RemoveAt(i);
                else names[i] = newName;
                count++;
            }
            return count;
        }

        private static int RemoveAll(ObservableCollection<string> names, HashSet<string> removed)
        {
            var count = 0;
            for (var i = names.Count - 1; i >= 0; i--)
            {
                if (!removed.Contains(names[i])) continue;
                names.RemoveAt(i);
                count++;
            }
            return count;
        }

        private static IEnumerable<ADObject> Flatten(ADObject root) => new[] { root }.Concat(root.Children.SelectMany(Flatten));
    }
}
