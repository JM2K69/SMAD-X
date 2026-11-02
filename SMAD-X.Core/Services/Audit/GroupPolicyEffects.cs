using System;
using System.Collections.Generic;
using System.Linq;
using SMADX.Models;
using SMADX.Services.Tiering;

namespace SMADX.Services.Audit
{
    /// <summary>Membre du groupe local Administrateurs d'un ordinateur, et la GPO qui l'y a mis (null : la machine elle-même).</summary>
    public sealed record LocalAdministrator(string Name, ADObject? Gpo);

    /// <summary>
    /// Effets des GPO sur les ordinateurs, pour les paramètres qui changent les chemins d'attaque : le refus d'ouverture
    /// de session (un compte refusé ne laisse pas ses identifiants sur la machine) et les membres du groupe local
    /// Administrateurs. Une GPO s'applique à un ordinateur quand elle est liée à son OU ou à l'une de ses OU parentes
    /// (seuls les liens actifs sont gardés) et que son filtre WMI le vise. Pour un même paramètre, la GPO liée au plus
    /// près de l'ordinateur l'emporte ; à égalité, la première de la liste des GPO liées. Les liens appliqués, le blocage
    /// de l'héritage et le filtrage de sécurité ne sont pas modélisés.
    /// </summary>
    public sealed class GroupPolicyEffects
    {
        private readonly AuditContext _context;
        private readonly Dictionary<string, ADObject> _gpos;
        private readonly Dictionary<ADObject, IReadOnlyList<ADObject>> _applied = new();
        private readonly Dictionary<ADObject, IReadOnlyList<LocalAdministrator>> _administrators = new();
        private readonly TierModelDefinition? _model;

        /// <summary>Mandataires qui désignent tout compte qui s'authentifie.</summary>
        private static readonly IReadOnlySet<string> Everyone = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Everyone", "Authenticated Users"
        };

        public GroupPolicyEffects(AuditContext context)
        {
            _context = context;
            _gpos = context.Objects.Where(o => o.Type == ADObjectType.Policy)
                .GroupBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            _model = TierModelCatalog.Find(context.Domain.TierModel);
        }

        /// <summary>GPO qui s'appliquent à l'ordinateur, de la plus prioritaire à la moins prioritaire.</summary>
        public IReadOnlyList<ADObject> Applied(ADObject computer)
        {
            if (_applied.TryGetValue(computer, out var cached)) return cached;
            var result = new List<ADObject>();
            for (var container = computer.Parent; container != null; container = container.Parent)
                foreach (var name in container.LinkedGPOs)
                    if (_gpos.TryGetValue(name, out var gpo) && !result.Contains(gpo) && Targets(gpo, computer))
                        result.Add(gpo);
            return _applied[computer] = result;
        }

        /// <summary>
        /// GPO qui interdisent au compte d'ouvrir une session sur l'ordinateur, en local et par le Bureau à distance ; vide
        /// si l'une des deux voies lui reste ouverte. Chaque droit est pris dans la GPO prioritaire qui le définit.
        /// </summary>
        public IReadOnlyList<ADObject> LogonDeniedBy(ADObject account, ADObject computer)
        {
            var applied = Applied(computer);
            var local = applied.FirstOrDefault(g => g.GpoDenyInteractiveLogon != null);
            var remote = applied.FirstOrDefault(g => g.GpoDenyRemoteInteractiveLogon != null);
            if (local == null || remote == null
                || !local.GpoDenyInteractiveLogon!.Any(p => Designates(p, account, computer))
                || !remote.GpoDenyRemoteInteractiveLogon!.Any(p => Designates(p, account, computer)))
                return [];
            return local == remote ? [local] : [local, remote];
        }

        /// <summary>
        /// Membres effectifs du groupe local Administrateurs : ceux de la machine, ou ceux de la GPO prioritaire qui les
        /// remplace (groupes restreints), puis ceux qu'ajoutent les autres GPO (préférences).
        /// </summary>
        public IReadOnlyList<LocalAdministrator> LocalAdministrators(ADObject computer)
        {
            if (_administrators.TryGetValue(computer, out var cached)) return cached;
            var applied = Applied(computer);
            var result = applied.FirstOrDefault(g => g.GpoReplaceLocalAdmins && g.GpoLocalAdmins != null) is { } replacing
                ? replacing.GpoLocalAdmins!.Select(n => new LocalAdministrator(n, replacing)).ToList()
                : (computer.LocalAdmins ?? []).Select(n => new LocalAdministrator(n, null)).ToList();
            foreach (var gpo in applied.Where(g => !g.GpoReplaceLocalAdmins && g.GpoLocalAdmins != null))
                foreach (var name in gpo.GpoLocalAdmins!)
                    if (!result.Any(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)))
                        result.Add(new LocalAdministrator(name, gpo));
            return _administrators[computer] = result;
        }

        /// <summary>
        /// Le mandataire d'un droit désigne-t-il le compte : lui-même, l'un de ses groupes (Domain Users compris pour un
        /// utilisateur), tout compte authentifié, ou le groupe local Administrateurs de la machine.
        /// </summary>
        private bool Designates(string principal, ADObject account, ADObject computer)
        {
            var name = principal[(principal.LastIndexOf('\\') + 1)..];
            if (Everyone.Contains(name) || string.Equals(name, account.Name, StringComparison.OrdinalIgnoreCase)) return true;
            if (account.Type == ADObjectType.User && string.Equals(name, "Domain Users", StringComparison.OrdinalIgnoreCase)) return true;
            var groups = _context.EffectiveGroups(account);
            if (groups.Any(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase))) return true;
            // Sur un serveur membre ou un poste, « Administrators » est le groupe local de la machine
            return string.Equals(name, "Administrators", StringComparison.OrdinalIgnoreCase) && !AuditContext.IsDomainController(computer)
                   && LocalAdministrators(computer).Any(a => string.Equals(a.Name, account.Name, StringComparison.OrdinalIgnoreCase)
                                                            || groups.Any(g => string.Equals(g.Name, a.Name, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>Le filtre WMI de la GPO vise-t-il l'ordinateur ? Un type de système inconnu ne passe aucun filtre.</summary>
        private bool Targets(ADObject gpo, ADObject computer) => gpo.GpoComputerFilter switch
        {
            null => true,
            GpoComputerFilter.Servers => IsServer(computer) == true,
            _ => IsServer(computer) == false
        };

        /// <summary>
        /// Serveur ou poste de travail : SMAD-X ne garde pas le système d'exploitation. Un contrôleur de domaine est un
        /// serveur ; sinon, le rôle de l'OU du modèle de cloisonnement qui le contient (serveurs, postes, PAW), sinon son
        /// tier (Tier 0 et 1 : serveurs, Tier 2 : postes). Null si rien ne le dit.
        /// </summary>
        public bool? IsServer(ADObject computer)
        {
            if (AuditContext.IsDomainController(computer)) return true;
            if (_model != null)
                for (var ou = computer.Parent; ou is { Type: ADObjectType.OrganizationalUnit }; ou = ou.Parent)
                    switch (TierModelCompliance.RoleOf(_model, ou))
                    {
                        case TierModelOuRole.Servers: return true;
                        case TierModelOuRole.Workstations or TierModelOuRole.AdminWorkstations: return false;
                    }
            return AuditContext.TierLevel(computer) switch
            {
                null => null,
                <= 1 => true,
                _ => false
            };
        }
    }
}
