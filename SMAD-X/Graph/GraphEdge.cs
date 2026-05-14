namespace SMADX.Graph
{
    public enum EdgeType
    {
        MemberOf,
        GroupNesting,      // Groupe membre d'un groupe
        GpoLink,
        GpoInheritance,   // GPO héritée d'un parent (Domain → OU ou OU parente → OU enfant)
        PsoSubject,
        ParentChild
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

        public string Label => Type switch
        {
            EdgeType.MemberOf        => "MemberOf",
            EdgeType.GroupNesting    => "Group in Group",
            EdgeType.GpoLink         => "GPO Link",
            EdgeType.GpoInheritance  => $"⬇ Héritage GPO",
            EdgeType.PsoSubject      => "PSO Subject",
            EdgeType.ParentChild     => "Contains",
            _                        => string.Empty
        };

        public GraphEdge(GraphNode source, GraphNode target, EdgeType type, string? gpoName = null)
        {
            Source   = source;
            Target   = target;
            Type     = type;
            GpoName  = gpoName;
        }
    }
}
