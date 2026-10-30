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
        ConstrainedDelegationToDc,
        /// <summary>Écrire tous les attributs : SPN, msDS-KeyCredentialLink, membres, délégation, gPLink…</summary>
        GenericWrite,
        /// <summary>Modifier les permissions de l'objet : on s'y accorde le contrôle total.</summary>
        WriteDacl,
        /// <summary>Prendre la propriété de l'objet, puis réécrire ses permissions.</summary>
        WriteOwner,
        /// <summary>Droits de réplication sur le domaine : extraire toutes les clés (DCSync).</summary>
        DCSync,
        /// <summary>Écrire msDS-KeyCredentialLink : s'authentifier comme le compte avec sa propre clé (Shadow Credentials).</summary>
        ShadowCredentials,
        /// <summary>Écrire msDS-AllowedToActOnBehalfOfOtherIdentity : délégation contrainte basée sur la ressource (RBCD).</summary>
        Rbcd,
        /// <summary>Écrire servicePrincipalName : Kerberoasting ciblé.</summary>
        WriteSpn,
        /// <summary>Lire le mot de passe administrateur local géré par LAPS.</summary>
        ReadLapsPassword,
        /// <summary>Lire le mot de passe d'un compte de service géré.</summary>
        ReadGmsaPassword,
        /// <summary>Administrateur local de l'ordinateur.</summary>
        AdminTo,
        /// <summary>Session ouverte sur l'ordinateur : ses identifiants y sont en mémoire.</summary>
        HasSession,
        /// <summary>Contrôle de l'OU : y lier une GPO exécute du code sur tous ses comptes, protégés compris.</summary>
        OuGpoLink,
        /// <summary>Contrôle du conteneur : s'accorder un droit héritable sur ses objets non protégés.</summary>
        ContainerContent,
        /// <summary>AdminSDHolder : SDProp recopie ses permissions sur tous les comptes et groupes protégés.</summary>
        AdminSdHolder,
        /// <summary>Lier une GPO à l'OU ou au domaine (écriture de gPLink).</summary>
        WriteGPLink
    }

    /// <summary>Nœud du graphe d'attaque : un objet AD, ou le point de départ « tout utilisateur du domaine ».</summary>
    public sealed record AttackNode(string Id, string Label, ADObject? Object)
    {
        public bool IsAnyUser => Object == null;
    }

    /// <param name="Holder">Objet qui porte la permission (l'ACE, ou le propriétaire), là où elle se corrige ; null hors permission.</param>
    public sealed record AttackEdge(AttackNode From, AttackNode To, AttackEdgeKind Kind, string Detail, ADObject? Holder = null);

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
        /// <summary>
        /// Groupes dont l'appartenance donne, directement ou par des chemins connus, le contrôle du domaine : les
        /// administrateurs, les opérateurs, DnsAdmins (DLL chargée par le service DNS des DC), Key Admins et Enterprise
        /// Key Admins (msDS-KeyCredentialLink de tous les comptes, Shadow Credentials) et Domain Controllers (DCSync).
        /// </summary>
        public static readonly IReadOnlySet<string> DomainControlGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Domain Admins", "Enterprise Admins", "Administrators", "Schema Admins",
            "Account Operators", "Server Operators", "Backup Operators", "Print Operators", "DnsAdmins",
            "Key Admins", "Enterprise Key Admins", "Domain Controllers"
        };

        /// <summary>Mandataires qui désignent n'importe quel compte du domaine.</summary>
        public static readonly IReadOnlySet<string> AnyUserTrustees = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
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
            // À longueur égale, le chemin le plus parlant : un groupe privilégié ou un DC contrôle le domaine par son rôle,
            // même s'il a aussi une permission sur la racine (Domain Admins modifie ses permissions sur un domaine neuf)
            foreach (var edge in Edges.OrderBy(e => e.Kind is AttackEdgeKind.PrivilegedGroup or AttackEdgeKind.DomainController ? 0 : 1))
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

        private AttackNode? Principal(string name) =>
            AnyUserTrustees.Contains(name) ? AnyUser : _context.Principal(name) is { } obj ? Node(obj) : null;

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
                    yield return new AttackEdge(owner, node, AttackEdgeKind.Owner, string.Format(loc["AttackPath.Edge.Owner"], obj.Owner, obj.Name), obj);

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

                foreach (var edge in ContainerEdges(obj, node))
                    yield return edge;

                foreach (var edge in ComputerEdges(obj, node))
                    yield return edge;
            }
        }

        /// <summary>
        /// Ce que donne le contrôle d'un conteneur, quand une délégation ou la propriété y mène : s'accorder un droit
        /// héritable sur ses objets non protégés ; pour une OU, y lier une GPO, qui atteint tous ses comptes, même
        /// protégés (une GPO ne passe pas par leurs ACL : c'est la technique OUned). Pour AdminSDHolder, SDProp recopie
        /// ses permissions sur tous les comptes et groupes protégés.
        /// </summary>
        private IEnumerable<AttackEdge> ContainerEdges(ADObject container, AttackNode node)
        {
            if (container.Type is not (ADObjectType.OrganizationalUnit or ADObjectType.Container)) yield break;
            var loc = LocalizationService.Instance;
            foreach (var target in Descendants(container).Where(o => !o.AdminCount && (o.IsAccount || o.Type == ADObjectType.Group)))
                yield return new AttackEdge(node, Node(target), AttackEdgeKind.ContainerContent, string.Format(loc["AttackPath.Edge.ContainerContent"], container.Name, target.Name));
            if (container.Type == ADObjectType.OrganizationalUnit)
                foreach (var target in Descendants(container).Where(o => o.IsAccount && o.AdminCount))
                    yield return new AttackEdge(node, Node(target), AttackEdgeKind.OuGpoLink, string.Format(loc["AttackPath.Edge.OuGpoLink"], container.Name, target.Name));
            if (container.Type == ADObjectType.Container && container.Name == "AdminSDHolder")
                foreach (var target in _context.Objects.Where(o => o.AdminCount && (o.IsAccount || o.Type == ADObjectType.Group)))
                    yield return new AttackEdge(node, Node(target), AttackEdgeKind.AdminSdHolder, string.Format(loc["AttackPath.Edge.AdminSdHolder"], target.Name));
        }

        /// <summary>
        /// Administration locale et sessions : un administrateur de la machine y vole les identifiants des comptes qui
        /// y ont une session (à la manière d'AdminTo et HasSession dans BloodHound).
        /// </summary>
        private IEnumerable<AttackEdge> ComputerEdges(ADObject computer, AttackNode node)
        {
            if (computer.Type != ADObjectType.Computer) yield break;
            var loc = LocalizationService.Instance;
            foreach (var admin in computer.LocalAdmins ?? [])
                if (Principal(admin) is { } principal && principal != node)
                    yield return new AttackEdge(principal, node, AttackEdgeKind.AdminTo, string.Format(loc["AttackPath.Edge.AdminTo"], admin, computer.Name));
            foreach (var account in computer.Sessions ?? [])
                if (_context.Principal(account) is { IsAccount: true } user && user != computer)
                    yield return new AttackEdge(node, Node(user), AttackEdgeKind.HasSession, string.Format(loc["AttackPath.Edge.HasSession"], user.Name, computer.Name));
        }

        /// <summary>
        /// Délégations directes posées sur un objet. Elles s'appliquent à l'objet lui-même et à ses descendants, sauf aux
        /// objets protégés par AdminSDHolder (adminCount = 1) : l'héritage y est coupé, et SDProp réécrit leurs permissions.
        /// </summary>
        private IEnumerable<AttackEdge> DelegationEdges(ADObject holder, AttackNode holderNode)
        {
            var loc = LocalizationService.Instance;
            // Objets atteints : l'objet (s'il n'est pas protégé) puis ses descendants non protégés, du type donné
            IEnumerable<ADObject> Reached(Func<ADObject, bool> type) =>
                new[] { holder }.Where(o => !(o.AdminCount && (o.IsAccount || o.Type == ADObjectType.Group)))
                    .Concat(Descendants(holder).Where(o => !o.AdminCount))
                    .Where(type);
            AttackEdge To(AttackNode trustee, ADObject target, AttackEdgeKind kind, string trusteeName) =>
                new(trustee, Node(target), kind, string.Format(loc[$"AttackPath.Edge.{kind}"], trusteeName, target.Name, holder.Name), holder);

            foreach (var d in holder.Delegations.Where(d => !d.IsInherited))
            {
                if (Principal(d.TrusteeName) is not { } trustee) continue;
                var name = d.TrusteeName;
                switch (d.Effective)
                {
                    case RightCategory.FullControl:
                        yield return new AttackEdge(trustee, holderNode, AttackEdgeKind.FullControl, string.Format(loc["AttackPath.Edge.FullControl"], name, holder.Name), holder);
                        foreach (var target in Descendants(holder).Where(o => !o.AdminCount && (o.IsAccount || o.Type == ADObjectType.Group)))
                            yield return new AttackEdge(trustee, Node(target), AttackEdgeKind.FullControl, string.Format(loc["AttackPath.Edge.FullControlInherited"], name, holder.Name, target.Name), holder);
                        break;
                    // Réécrire les permissions ou le propriétaire : le contrôle de l'objet, et de ce qu'il contient
                    case RightCategory.WriteDacl or RightCategory.WriteOwner:
                        var kind = d.Effective == RightCategory.WriteDacl ? AttackEdgeKind.WriteDacl : AttackEdgeKind.WriteOwner;
                        yield return new AttackEdge(trustee, holderNode, kind, string.Format(loc[$"AttackPath.Edge.{kind}"], name, holder.Name, holder.Name), holder);
                        break;
                    case RightCategory.GenericWrite:
                        yield return new AttackEdge(trustee, holderNode, AttackEdgeKind.GenericWrite, string.Format(loc["AttackPath.Edge.GenericWrite"], name, holder.Name, holder.Name), holder);
                        foreach (var target in Descendants(holder).Where(o => !o.AdminCount && (o.IsAccount || o.Type == ADObjectType.Group)))
                            yield return To(trustee, target, AttackEdgeKind.GenericWrite, name);
                        break;
                    case RightCategory.PasswordReset:
                        foreach (var target in Reached(o => o.Type == ADObjectType.User))
                            yield return new AttackEdge(trustee, Node(target), AttackEdgeKind.ResetPassword, string.Format(loc["AttackPath.Edge.ResetPassword"], name, target.Name, holder.Name), holder);
                        break;
                    // Tous les droits étendus : réinitialiser un mot de passe, lire le mot de passe LAPS ; sur le domaine, répliquer
                    case RightCategory.AllExtendedRights:
                        if (holder.Type == ADObjectType.Domain)
                            yield return new AttackEdge(trustee, Target, AttackEdgeKind.DCSync, string.Format(loc["AttackPath.Edge.DCSync"], name, holder.Name, holder.Name), holder);
                        foreach (var target in Reached(o => o.Type == ADObjectType.User))
                            yield return new AttackEdge(trustee, Node(target), AttackEdgeKind.ResetPassword, string.Format(loc["AttackPath.Edge.ResetPassword"], name, target.Name, holder.Name), holder);
                        foreach (var target in Reached(o => o.Type == ADObjectType.Computer && o.LapsPasswordExpiration != null))
                            yield return To(trustee, target, AttackEdgeKind.ReadLapsPassword, name);
                        break;
                    case RightCategory.WriteMembers:
                        foreach (var target in Reached(o => o.Type == ADObjectType.Group))
                            yield return new AttackEdge(trustee, Node(target), AttackEdgeKind.WriteMembers, string.Format(loc["AttackPath.Edge.WriteMembers"], name, target.Name, holder.Name), holder);
                        break;
                    case RightCategory.Replication when holder.Type == ADObjectType.Domain:
                        yield return new AttackEdge(trustee, Target, AttackEdgeKind.DCSync, string.Format(loc["AttackPath.Edge.DCSync"], name, holder.Name, holder.Name), holder);
                        break;
                    case RightCategory.WriteKeyCredentialLink:
                        foreach (var target in Reached(o => o.Type is ADObjectType.User or ADObjectType.Computer))
                            yield return To(trustee, target, AttackEdgeKind.ShadowCredentials, name);
                        break;
                    case RightCategory.WriteAllowedToAct:
                        foreach (var target in Reached(o => o.Type == ADObjectType.Computer))
                            yield return To(trustee, target, AttackEdgeKind.Rbcd, name);
                        break;
                    case RightCategory.WriteSpn:
                        foreach (var target in Reached(o => o.Type == ADObjectType.User && o.Name != "krbtgt"))
                            yield return To(trustee, target, AttackEdgeKind.WriteSpn, name);
                        break;
                    case RightCategory.ReadLapsPassword:
                        foreach (var target in Reached(o => o.Type == ADObjectType.Computer && o.LapsPasswordExpiration != null))
                            yield return To(trustee, target, AttackEdgeKind.ReadLapsPassword, name);
                        break;
                    case RightCategory.ReadGmsaPassword:
                        foreach (var target in Reached(o => o.Type == ADObjectType.GMSA))
                            yield return To(trustee, target, AttackEdgeKind.ReadGmsaPassword, name);
                        break;
                    case RightCategory.WriteGPLink when holder.Type is ADObjectType.OrganizationalUnit or ADObjectType.Domain:
                        yield return new AttackEdge(trustee, holderNode, AttackEdgeKind.WriteGPLink, string.Format(loc["AttackPath.Edge.WriteGPLink"], name, holder.Name, holder.Name), holder);
                        break;
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

            // Un conteneur n'est qu'une étape : seuls un compte, un groupe, une GPO ou « tout utilisateur » attaquent
            return next.Keys
                .Where(start => start.Object?.Type is not (ADObjectType.Container or ADObjectType.OrganizationalUnit or ADObjectType.Domain))
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
