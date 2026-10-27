using System;
using System.Collections.Generic;
using System.Linq;
using SMADX.Models;
using SMADX.Services.Audit;

namespace SMADX.Services.AttackPaths
{
    /// <summary>Façon dont la source d'une arête prend le contrôle de sa cible.</summary>
    public enum AttackEdgeKind
    {
        /// <summary>Membre du groupe : hérite de ses droits.</summary>
        MemberOf,
        /// <summary>Groupe dont les membres contrôlent le domaine (Domain Admins, Operators, DnsAdmins…).</summary>
        PrivilegedGroup,
        /// <summary>Contrôleur de domaine : le compromettre, c'est compromettre le domaine (DCSync).</summary>
        DomainController,
        /// <summary>Propriétaire de l'objet : peut toujours réécrire ses permissions.</summary>
        Owner,
        /// <summary>Contrôle total délégué sur l'objet ou son OU.</summary>
        FullControl,
        /// <summary>Peut réinitialiser le mot de passe du compte.</summary>
        ResetPassword,
        /// <summary>Peut modifier les membres du groupe.</summary>
        WriteMembers,
        /// <summary>GPO liée à l'OU de l'objet : exécution de code sur les machines et sessions visées.</summary>
        GpoLink,
        /// <summary>Kerberoasting : tout utilisateur obtient un ticket chiffré avec le mot de passe du compte.</summary>
        Kerberoasting,
        /// <summary>AS-REP roasting : réponse chiffrée avec le mot de passe, sans préauthentification.</summary>
        AsRepRoasting,
        /// <summary>Mot de passe vide accepté (PASSWD_NOTREQD).</summary>
        EmptyPassword,
        /// <summary>Délégation non contrainte : un DC forcé à s'authentifier y laisse son TGT.</summary>
        UnconstrainedDelegation,
        /// <summary>Délégation contrainte vers un service d'un contrôleur de domaine.</summary>
        ConstrainedDelegationToDc
    }

    /// <summary>Nœud du graphe d'attaque : un objet AD, ou le point de départ « tout utilisateur du domaine ».</summary>
    public sealed record AttackNode(string Id, string Label, ADObject? Object)
    {
        public bool IsAnyUser => Object == null;
    }

    public sealed record AttackEdge(AttackNode From, AttackNode To, AttackEdgeKind Kind, string Detail);

    /// <summary>Chemin d'une source jusqu'au contrôle du domaine.</summary>
    public sealed record AttackPath(AttackNode Start, IReadOnlyList<AttackEdge> Steps)
    {
        public int Length => Steps.Count;
    }

    /// <summary>
    /// Graphe « X peut prendre le contrôle de Y » construit à partir des appartenances, des délégations,
    /// des propriétaires, des GPO, de la délégation Kerberos et des comptes kerberoastables ou
    /// AS-REP roastables, puis recherche du plus court chemin vers le contrôle du domaine
    /// (à la manière de BloodHound, en beaucoup plus simple).
    /// </summary>
    public sealed class AttackPathService
    {
        /// <summary>Groupes dont l'appartenance donne, directement ou par des chemins connus, le contrôle du domaine.</summary>
        public static readonly IReadOnlySet<string> DomainControlGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Domain Admins", "Enterprise Admins", "Administrators", "Schema Admins",
            "Account Operators", "Server Operators", "Backup Operators", "Print Operators", "DnsAdmins"
        };

        /// <summary>Mandataires qui désignent n'importe quel compte du domaine.</summary>
        private static readonly IReadOnlySet<string> AnyUserTrustees = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Domain Users", "Authenticated Users", "Everyone"
        };

        public const string AnyUserId = "any-domain-user";

        private readonly Dictionary<AttackNode, List<AttackEdge>> _incoming = new();
        private readonly Dictionary<ADObject, AttackNode> _nodes = new();
        private readonly AuditContext _context;

        public AttackNode AnyUser { get; }
        public AttackNode Target { get; }
        public IReadOnlyList<AttackEdge> Edges { get; }

        public AttackPathService(ADObject domain, DateTime? now = null)
        {
            _context = new AuditContext(domain, now ?? DateTime.Now);
            AnyUser = new AttackNode(AnyUserId, LocalizationService.Instance["AttackPath.AnyUser"], null);
            Target = Node(domain);
            Edges = BuildEdges().ToList();
            foreach (var edge in Edges)
            {
                if (!_incoming.TryGetValue(edge.To, out var list))
                    _incoming[edge.To] = list = new List<AttackEdge>();
                list.Add(edge);
            }
        }

        public AttackNode Node(ADObject obj)
        {
            if (!_nodes.TryGetValue(obj, out var node))
                _nodes[obj] = node = new AttackNode(obj.Id.ToString(), obj.Name, obj);
            return node;
        }

        private AttackNode Principal(string name) =>
            AnyUserTrustees.Contains(name) ? AnyUser : _context.Principal(name) is { } obj ? Node(obj) : null!;

        // ─── Construction des arêtes ─────────────────────────────────────────────

        private IEnumerable<AttackEdge> BuildEdges()
        {
            var loc = LocalizationService.Instance;
            foreach (var obj in _context.Objects)
            {
                var node = Node(obj);

                // Appartenance directe : le membre hérite des droits du groupe
                foreach (var groupName in obj.MemberOf)
                    if (_context.Group(groupName) is { } group)
                        yield return new AttackEdge(node, Node(group), AttackEdgeKind.MemberOf, string.Format(loc["AttackPath.Edge.MemberOf"], obj.Name, group.Name));

                if (obj.Type == ADObjectType.Group && DomainControlGroups.Contains(obj.Name))
                    yield return new AttackEdge(node, Target, AttackEdgeKind.PrivilegedGroup, string.Format(loc["AttackPath.Edge.PrivilegedGroup"], obj.Name));

                if (AuditContext.IsDomainController(obj))
                    yield return new AttackEdge(node, Target, AttackEdgeKind.DomainController, string.Format(loc["AttackPath.Edge.DomainController"], obj.Name));

                // Propriétaire : peut réécrire les permissions de l'objet
                if (!string.IsNullOrWhiteSpace(obj.Owner) && Principal(obj.Owner!) is { } owner && owner != node)
                    yield return new AttackEdge(owner, node, AttackEdgeKind.Owner, string.Format(loc["AttackPath.Edge.Owner"], obj.Owner, obj.Name));

                foreach (var edge in DelegationEdges(obj, node))
                    yield return edge;

                foreach (var edge in AccountEdges(obj, node))
                    yield return edge;

                // GPO liée : quiconque contrôle la GPO exécute du code sur les objets de l'OU
                foreach (var gpoName in obj.LinkedGPOs)
                {
                    var gpo = _context.Objects.FirstOrDefault(o => o.Type == ADObjectType.Policy && o.Name == gpoName);
                    if (gpo == null) continue;
                    foreach (var target in Descendants(obj).Where(o => o.IsAccount))
                        yield return new AttackEdge(Node(gpo), Node(target), AttackEdgeKind.GpoLink, string.Format(loc["AttackPath.Edge.GpoLink"], gpo.Name, obj.Name, target.Name));
                }
            }
        }

        /// <summary>
        /// Délégations directes posées sur un conteneur. Elles s'appliquent à ses descendants, sauf aux objets
        /// protégés par AdminSDHolder (adminCount = 1), dont l'héritage des permissions est désactivé.
        /// </summary>
        private IEnumerable<AttackEdge> DelegationEdges(ADObject container, AttackNode containerNode)
        {
            var loc = LocalizationService.Instance;
            foreach (var d in container.Delegations.Where(d => !d.IsInherited))
            {
                var trustee = Principal(d.TrusteeName);
                if (trustee == null) continue;

                if (d.RightCategory == RightCategory.FullControl)
                {
                    yield return new AttackEdge(trustee, containerNode, AttackEdgeKind.FullControl, string.Format(loc["AttackPath.Edge.FullControl"], d.TrusteeName, container.Name));
                    foreach (var target in Descendants(container).Where(o => !o.AdminCount && (o.IsAccount || o.Type == ADObjectType.Group)))
                        yield return new AttackEdge(trustee, Node(target), AttackEdgeKind.FullControl, string.Format(loc["AttackPath.Edge.FullControlInherited"], d.TrusteeName, container.Name, target.Name));
                }
                else if (d.RightCategory == RightCategory.PasswordReset)
                {
                    foreach (var target in Descendants(container).Where(o => o.Type == ADObjectType.User && !o.AdminCount))
                        yield return new AttackEdge(trustee, Node(target), AttackEdgeKind.ResetPassword, string.Format(loc["AttackPath.Edge.ResetPassword"], d.TrusteeName, target.Name, container.Name));
                }
                else if (d.RightCategory == RightCategory.AttributeWrite
                         && (d.Right.EndsWith(":member", StringComparison.OrdinalIgnoreCase) || d.Right.Equals("GenericWrite", StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (var target in Descendants(container).Where(o => o.Type == ADObjectType.Group && !o.AdminCount))
                        yield return new AttackEdge(trustee, Node(target), AttackEdgeKind.WriteMembers, string.Format(loc["AttackPath.Edge.WriteMembers"], d.TrusteeName, target.Name, container.Name));
                }
            }
        }

        /// <summary>Faiblesses propres à un compte : points d'entrée depuis n'importe quel utilisateur, délégation Kerberos.</summary>
        private IEnumerable<AttackEdge> AccountEdges(ADObject obj, AttackNode node)
        {
            if (!obj.IsAccount || !AuditContext.IsEnabled(obj)) yield break;
            var loc = LocalizationService.Instance;

            if (obj.Type == ADObjectType.User && obj.Name != "krbtgt" && obj.ServicePrincipalNames.Count > 0)
                yield return new AttackEdge(AnyUser, node, AttackEdgeKind.Kerberoasting, string.Format(loc["AttackPath.Edge.Kerberoasting"], obj.Name));
            if (obj.AccountFlags.HasFlag(ADAccountFlags.DontRequirePreauth))
                yield return new AttackEdge(AnyUser, node, AttackEdgeKind.AsRepRoasting, string.Format(loc["AttackPath.Edge.AsRepRoasting"], obj.Name));
            if (obj.AccountFlags.HasFlag(ADAccountFlags.PasswordNotRequired))
                yield return new AttackEdge(AnyUser, node, AttackEdgeKind.EmptyPassword, string.Format(loc["AttackPath.Edge.EmptyPassword"], obj.Name));

            if (obj.AccountFlags.HasFlag(ADAccountFlags.TrustedForDelegation) && !AuditContext.IsDomainController(obj))
                yield return new AttackEdge(node, Target, AttackEdgeKind.UnconstrainedDelegation, string.Format(loc["AttackPath.Edge.Unconstrained"], obj.Name));

            foreach (var spn in obj.AllowedToDelegateTo)
            {
                var host = spn.Contains('/') ? spn[(spn.IndexOf('/') + 1)..].Split(':', '/')[0] : spn;
                var dc = _context.DomainControllers.FirstOrDefault(d =>
                    string.Equals(host, d.Name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(host, $"{d.Name}.{_context.Domain.Name}", StringComparison.OrdinalIgnoreCase));
                if (dc != null)
                    yield return new AttackEdge(node, Node(dc), AttackEdgeKind.ConstrainedDelegationToDc, string.Format(loc["AttackPath.Edge.ConstrainedToDc"], obj.Name, spn));
            }
        }

        private static IEnumerable<ADObject> Descendants(ADObject root) => root.Children.SelectMany(c => new[] { c }.Concat(Descendants(c)));

        // ─── Recherche des chemins ──────────────────────────────────────────────

        /// <summary>Plus courts chemins vers le contrôle du domaine, depuis chaque source qui en a un.</summary>
        public IReadOnlyList<AttackPath> PathsToDomainControl()
        {
            // Parcours en largeur à rebours depuis la cible : next[n] = arête qui rapproche n de la cible
            var next = new Dictionary<AttackNode, AttackEdge>();
            var queue = new Queue<AttackNode>();
            queue.Enqueue(Target);
            var seen = new HashSet<AttackNode> { Target };
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!_incoming.TryGetValue(current, out var edges)) continue;
                foreach (var edge in edges)
                {
                    if (!seen.Add(edge.From)) continue;
                    next[edge.From] = edge;
                    queue.Enqueue(edge.From);
                }
            }

            return next.Keys
                .Select(start => new AttackPath(start, Walk(start, next)))
                .OrderBy(p => p.Start.IsAnyUser ? 0 : 1)
                .ThenBy(p => p.Length)
                .ThenBy(p => p.Start.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Source de Tier 0 : elle contrôle déjà le domaine par définition, son chemin n'est pas une faiblesse.
        /// « N'importe quel utilisateur » n'est jamais de Tier 0.
        /// </summary>
        public static bool IsTier0(AttackNode node) => node.Object is { } o && AuditContext.TierLevel(o) == 0;

        /// <summary>Chemins qui partent d'une source hors Tier 0 : ceux qu'il faut supprimer.</summary>
        public IReadOnlyList<AttackPath> PathsOutsideTier0() =>
            PathsToDomainControl().Where(p => p.Start != Target && !IsTier0(p.Start)).ToList();

        /// <summary>Chemin depuis un objet, ou null s'il ne peut pas atteindre le contrôle du domaine.</summary>
        public AttackPath? PathFrom(ADObject start) =>
            PathsToDomainControl().FirstOrDefault(p => p.Start.Object == start);

        private static IReadOnlyList<AttackEdge> Walk(AttackNode start, Dictionary<AttackNode, AttackEdge> next)
        {
            var steps = new List<AttackEdge>();
            for (var node = start; next.TryGetValue(node, out var edge); node = edge.To)
                steps.Add(edge);
            return steps;
        }
    }
}
