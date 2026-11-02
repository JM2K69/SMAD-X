using System;
using System.Collections.Generic;
using System.Linq;
using SMADX.Models;

namespace SMADX.Services.Audit
{
    /// <summary>
    /// Effet des silos d'authentification du domaine : un compte d'un silo appliqué n'ouvre de session que sur les
    /// ordinateurs du silo. La stratégie du silo ne restreint que Kerberos : un compte qui peut encore utiliser NTLM la
    /// contourne, et seul un membre de Protected Users n'utilise que Kerberos. AD n'affecte un compte qu'à un seul silo
    /// (msDS-AssignedAuthNPolicySilo) : le premier silo qui le cite est le sien.
    /// </summary>
    public sealed class AuthenticationSiloEffects
    {
        private readonly AuditContext _context;
        private readonly IReadOnlyList<ADAuthenticationSilo> _silos;

        /// <summary>Autorisent tout ordinateur : un silo dont la stratégie ne restreint pas l'origine de l'authentification.</summary>
        private static readonly IReadOnlySet<string> AnyComputer = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Everyone", "Authenticated Users", "Domain Computers"
        };

        public AuthenticationSiloEffects(AuditContext context)
        {
            _context = context;
            _silos = context.Domain.AuthenticationSilos?.ToList() ?? [];
        }

        public bool Any => _silos.Count > 0;

        /// <summary>Silo du compte, appliqué ou non ; null s'il n'est dans aucun.</summary>
        public ADAuthenticationSilo? SiloOf(ADObject account) => _silos.FirstOrDefault(s => Includes(s.Members, account));

        /// <summary>Le compte n'utilise que Kerberos (membre de Protected Users) : NTLM ne contourne pas la stratégie de son silo.</summary>
        public bool KerberosOnly(ADObject account) =>
            _context.Group("Protected Users") is { } protectedUsers && _context.EffectiveGroups(account).Contains(protectedUsers);

        /// <summary>Silo appliqué qui interdit au compte de s'authentifier depuis cet ordinateur ; null s'il le peut.</summary>
        public ADAuthenticationSilo? LogonDeniedBy(ADObject account, ADObject computer) =>
            SiloOf(account) is { Enforced: true } silo && KerberosOnly(account)
            && !silo.Computers.Any(AnyComputer.Contains) && !Includes(silo.Computers, computer)
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
