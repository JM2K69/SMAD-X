using System;
using System.Collections.Generic;
using System.Linq;
using SMADX.Models;

namespace SMADX.Services.Audit
{
    /// <summary>
    /// Effet des silos d'authentification du domaine : un compte d'un silo appliqué n'ouvre de session que sur les
    /// ordinateurs du silo, ou ceux que sa stratégie autorise en plus. AD n'affecte un compte qu'à un seul silo
    /// (msDS-AssignedAuthNPolicySilo) : le premier silo qui le cite est le sien.
    /// </summary>
    public sealed class AuthenticationSiloEffects
    {
        private readonly AuditContext _context;
        private readonly IReadOnlyList<ADAuthenticationSilo> _silos;

        public AuthenticationSiloEffects(AuditContext context)
        {
            _context = context;
            _silos = context.Domain.AuthenticationSilos?.ToList() ?? [];
        }

        public bool Any => _silos.Count > 0;

        /// <summary>Silo du compte ou de l'ordinateur, appliqué ou non ; null s'il n'est dans aucun.</summary>
        public ADAuthenticationSilo? SiloOf(ADObject account) => _silos.FirstOrDefault(s => Includes(s.Members, account));

        /// <summary>Autorisent tout ordinateur : un silo dont la stratégie ne restreint pas l'origine de l'authentification.</summary>
        private static readonly IReadOnlySet<string> AnyComputer = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Everyone", "Authenticated Users", "Domain Computers"
        };

        /// <summary>Silo appliqué qui interdit au compte de s'authentifier depuis cet ordinateur ; null s'il le peut.</summary>
        public ADAuthenticationSilo? LogonDeniedBy(ADObject account, ADObject computer) =>
            SiloOf(account) is { Enforced: true } silo && !silo.AllowedFrom.Any(AnyComputer.Contains)
            && !Includes(silo.Members, computer) && !Includes(silo.AllowedFrom, computer)
                ? silo
                : null;

        /// <summary>L'objet est-il cité, directement ou par l'un de ses groupes ?</summary>
        private bool Includes(IEnumerable<string> names, ADObject o)
        {
            var groups = _context.EffectiveGroups(o);
            return names.Any(n => string.Equals(n, o.Name, StringComparison.OrdinalIgnoreCase)
                                  || groups.Any(g => string.Equals(g.Name, n, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
