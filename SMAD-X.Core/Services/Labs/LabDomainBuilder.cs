using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SMADX.Models;
using SMADX.Services.Audit;

namespace SMADX.Services.Labs
{
    /// <summary>
    /// Construit les domaines des exercices et des scénarios : un domaine neuf durci (aucun constat d'audit),
    /// peuplé comme un vrai AD (services, utilisateurs, postes), dont toutes les dates sont figées à une date de
    /// référence. Le résultat est reproductible : mêmes noms, mêmes identifiants, à chaque génération.
    /// </summary>
    public sealed class LabDomainBuilder
    {
        private readonly ADDataService _data = new();

        public LabDomainBuilder(string culture, DateTime reference)
        {
            Culture = culture;
            Reference = reference;
        }

        /// <summary>Langue des noms et des descriptions (« fr-FR » ou « en-US »).</summary>
        public string Culture { get; }

        /// <summary>Date à laquelle les dates du domaine sont figées (voir LabDefinition.ReferenceDate).</summary>
        public DateTime Reference { get; }

        public bool French => !Culture.StartsWith("en", StringComparison.OrdinalIgnoreCase);

        public string T(string french, string english) => French ? french : english;

        // ─── Objets ───────────────────────────────────────────────────────────────

        public static IEnumerable<ADObject> Flatten(ADObject o) => new[] { o }.Concat(o.Children.SelectMany(Flatten));

        public static ADObject Find(ADObject root, string name, ADObjectType? type = null) =>
            Flatten(root).First(o => o.Name == name && (type is null || o.Type == type));

        public static ADObject Add(ADObject parent, ADObject child)
        {
            child.Parent = parent;
            parent.Children.Add(child);
            child.UpdateDistinguishedNameTree();
            return child;
        }

        public ADObject Ou(ADObject parent, string name, string tier) =>
            Add(parent, new ADObject(name, ADObjectType.OrganizationalUnit) { Tier = tier, Owner = "Domain Admins", CreatedDate = Reference.AddDays(-400) });

        public ADObject User(ADObject parent, string name, string tier, string description) => Add(parent, new ADObject(name, ADObjectType.User)
        {
            Tier = tier, Owner = "Domain Admins", Description = description,
            PwdLastSet = Reference.AddDays(-30), LastLogonTimestamp = Reference.AddDays(-1), CreatedDate = Reference.AddDays(-400)
        });

        public ADObject Group(ADObject parent, string name, string tier, string description) => Add(parent, new ADObject(name, ADObjectType.Group)
        {
            Tier = tier, Owner = "Domain Admins", Description = description, CreatedDate = Reference.AddDays(-400),
            GroupScope = ADGroupScope.Global, GroupCategory = ADGroupCategory.Security
        });

        public ADObject Computer(ADObject parent, string name, string tier, string description) => Add(parent, new ADObject(name, ADObjectType.Computer)
        {
            Tier = tier, Owner = "Domain Admins", Description = description,
            PwdLastSet = Reference.AddDays(-20), LastLogonTimestamp = Reference.AddDays(-1), CreatedDate = Reference.AddDays(-400)
        });

        public static void Delegate(ADObject target, string trustee, TrusteeType type, string right, RightCategory category, string tier) =>
            target.Delegations.Add(new ADDelegation
            {
                TrusteeName = trustee, TrusteeType = type, TargetDN = target.DistinguishedName,
                Right = right, RightCategory = category, Tier = tier
            });

        // ─── Domaine sain ─────────────────────────────────────────────────────────

        /// <summary>
        /// Domaine neuf, durci (les constats d'un AD sortie d'installation sont corrigés) et peuplé.
        /// Les descriptions des objets intégrés sont écrites dans la langue du constructeur.
        /// </summary>
        /// <exception cref="InvalidOperationException">Le domaine remonte un constat d'audit : il ne peut pas servir de départ.</exception>
        public ADObject Healthy(string domainName)
        {
            var loc = LocalizationService.Instance;
            var previous = loc.CurrentCulture.Name;
            if (!string.Equals(previous, Culture, StringComparison.OrdinalIgnoreCase))
                loc.SetLanguage(Culture);
            try
            {
                TierConfigurationService.Instance.ResetToDefaults();
                var d = _data.CreateFreshDomainStructure(domainName);
                foreach (var o in Flatten(d))
                {
                    o.CreatedDate = Reference.AddDays(-400);
                    o.ModifiedDate = Reference.AddDays(-400);
                    if (o.PwdLastSet != null) o.PwdLastSet = Reference.AddDays(o.Type == ADObjectType.Computer ? -20 : -30);
                    if (o.LastLogonTimestamp != null) o.LastLogonTimestamp = Reference.AddDays(-1);
                }
                var admin = Find(d, "Administrator", ADObjectType.User);
                admin.AccountFlags = ADAccountFlags.NotDelegated;
                admin.MemberOf.Remove("Enterprise Admins");
                admin.MemberOf.Remove("Schema Admins");
                d.Tier = "Tier 0";
                Populate(d);
                var findings = new ADSecurityAuditService().Run(d, Reference).Findings;
                if (findings.Count > 0)
                    throw new InvalidOperationException("Domaine de départ non propre : " + string.Join(", ", findings.Select(f => f.Rule.Id + " " + f.Object.Name)));
                return d;
            }
            finally
            {
                if (!string.Equals(previous, Culture, StringComparison.OrdinalIgnoreCase))
                    loc.SetLanguage(previous);
            }
        }

        /// <summary>
        /// Le quotidien de l'organisation : services, utilisateurs et postes, pour que la faiblesse soit à chercher
        /// parmi d'autres objets comme dans un vrai AD. Tirage reproductible, propre à chaque domaine.
        /// </summary>
        public void Populate(ADObject d)
        {
            var rng = new Random(BitConverter.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes(d.Name)), 0));
            string[] first = French
                ? ["camille", "lucas", "chloe", "hugo", "manon", "louis", "ines", "jules", "lea", "nathan", "sarah", "theo", "emma", "paul", "zoe", "gabriel"]
                : ["olivia", "liam", "emma", "noah", "ava", "james", "sophia", "henry", "mia", "jack", "grace", "leo", "ella", "ethan", "chloe", "oscar"];
            string[] last = French
                ? ["martin", "bernard", "thomas", "robert", "richard", "durand", "leroy", "simon", "laurent", "michel", "garcia", "fournier", "rousseau", "blanc", "henry", "lemaire"]
                : ["smith", "jones", "williams", "brown", "taylor", "davies", "evans", "thomas", "roberts", "johnson", "lewis", "walker", "hall", "clarke", "young", "king"];
            (string Ou, string Group)[] departments = French
                ? [("Direction", "GG-Direction"), ("Comptabilité", "GG-Comptabilite"), ("Ressources humaines", "GG-RH"), ("Production", "GG-Production")]
                : [("Management", "GG-Management"), ("Finance", "GG-Finance"), ("Human Resources", "GG-HR"), ("Logistics", "GG-Logistics")];

            var people = Ou(d, T("Personnel", "People"), "Tier 2");
            var used = new HashSet<string>();
            foreach (var (ouName, groupName) in departments)
            {
                var ou = Ou(people, ouName, "Tier 2");
                Group(ou, groupName, "Tier 2", T($"Membres du service {ouName}.", $"{ouName} department members."));
                for (var i = 0; i < 3; i++)
                {
                    string name;
                    do name = $"{first[rng.Next(first.Length)]}.{last[rng.Next(last.Length)]}"; while (!used.Add(name));
                    var user = User(ou, name, "Tier 2", T($"Service {ouName}.", $"{ouName} department."));
                    user.LastLogonTimestamp = Reference.AddDays(-rng.Next(1, 8));
                    user.PwdLastSet = Reference.AddDays(-rng.Next(10, 80));
                    user.MemberOf.Add(groupName);
                }
            }
            var workstations = Ou(d, T("Postes de travail", "Workstations"), "Tier 2");
            for (var i = 1; i <= 6; i++)
                Computer(workstations, $"{T("PC", "WKS")}-{i:000}", "Tier 2", T("Poste de travail.", "Workstation."));
        }

        /// <summary>Document complet (domaine et topologie de sites), aux identifiants et dates reproductibles.</summary>
        public ADRootDocument Document(ADObject domain)
        {
            var document = new ADRootDocument { Version = ADRootDocument.CurrentVersion, Domain = domain, SitesTopology = _data.CreateFreshTopology(domain.Name) };
            AssignStableIds(document, Reference);
            return document;
        }

        // ─── Reproductibilité ─────────────────────────────────────────────────────

        /// <summary>Identifiant tiré d'une clé stable, pour qu'une régénération ne modifie pas les fichiers inutilement.</summary>
        public static Guid StableId(string key)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key))[..16];
            bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50); // version 5 (identifiant dérivé d'un nom)
            bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // variante RFC 4122
            return new Guid(bytes);
        }

        public static void AssignStableIds(ADRootDocument document, DateTime reference)
        {
            var used = new HashSet<Guid>();
            foreach (var o in Flatten(document.Domain))
            {
                var key = $"{document.Domain.Name}|{o.Type}|{o.DistinguishedName}";
                var id = StableId(key);
                for (var n = 2; !used.Add(id); n++)
                    id = StableId($"{key}|{n}");
                o.Id = id;

                // Les objets ajoutés après coup portent l'heure de la génération : on les ramène avant la date de référence
                if (o.CreatedDate > reference) o.CreatedDate = reference.AddDays(-400);
                if (o.ModifiedDate > reference) o.ModifiedDate = reference.AddDays(-1);
            }
            foreach (var site in document.SitesTopology?.Sites ?? [])
            {
                site.Id = StableId($"{document.Domain.Name}|Site|{site.Name}");
                site.CreatedDate = reference.AddDays(-400);
                site.ModifiedDate = reference.AddDays(-400);
            }
        }
    }
}
