using SMADX.Services;

namespace SMADX.Graph
{
    public enum EdgeType
    {
        MemberOf,
        GroupNesting,      // Groupe membre d'un groupe
        GpoLink,
        GpoInheritance,   // GPO héritée d'un parent (Domain → OU ou OU parente → OU enfant)
        PsoSubject,
        ParentChild,
        Delegation        // Délégation ACL : trustee → OU/Container cible
    }

    /// <summary>
    /// Arête du graphe de relations AD
    /// </summary>
    public class GraphEdge
    {
        public GraphNode Source { get; set; }
        public GraphNode Target { get; set; }
        public EdgeType Type { get; set; }

        /// <summary>Nom de la GPO source pour les arêtes d'héritage</summary>
        public string? GpoName { get; set; }

        /// <summary>Libellé de l'arête dans la langue courante (relu à chaque dessin du graphe).</summary>
        public string Label => Type switch
        {
            EdgeType.MemberOf        => Loc["Relation.MemberOf"],
            EdgeType.GroupNesting    => Loc["Relation.GroupNesting"],
            EdgeType.GpoLink         => Loc["Relation.GpoLink"],
            EdgeType.GpoInheritance  => $"⬇ {Loc["Relation.GpoInheritance"]}",
            EdgeType.PsoSubject      => Loc["Relation.PsoSubject"],
            EdgeType.ParentChild     => Loc["Relation.ParentChild"],
            EdgeType.Delegation      => GpoName ?? Loc["Relation.Delegation"],
            _                        => string.Empty
        };

        private static LocalizationService Loc => LocalizationService.Instance;

        public GraphEdge(GraphNode source, GraphNode target, EdgeType type, string? gpoName = null)
        {
            Source   = source;
            Target   = target;
            Type     = type;
            GpoName  = gpoName;
        }
    }
}
