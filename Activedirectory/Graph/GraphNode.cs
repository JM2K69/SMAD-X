using System;
using SMADX.Models;

namespace SMADX.Graph
{
    /// <summary>
    /// Nœud du graphe de relations AD
    /// </summary>
    public class GraphNode
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public ADObjectType NodeType { get; set; }
        public string? Tier { get; set; }

        // Position dans le canvas (coordonnées du monde)
        public double X { get; set; }
        public double Y { get; set; }

        // Forces pour le layout force-directed
        public double Fx { get; set; }
        public double Fy { get; set; }
        public double Vx { get; set; }
        public double Vy { get; set; }

        public bool IsDragging { get; set; }
        public bool IsHovered { get; set; }
        public bool IsSelected { get; set; }

        // Rayon du nœud (px dans coordonnées monde)
        public double Radius => NodeType switch
        {
            ADObjectType.Domain => 36,
            ADObjectType.OrganizationalUnit => 28,
            ADObjectType.Group => 24,
            ADObjectType.User => 22,
            ADObjectType.Computer => 22,
            ADObjectType.Policy => 22,
            ADObjectType.PasswordSettingsObject => 22,
            ADObjectType.GMSA => 20,
            _ => 20
        };

        public GraphNode(string id, string label, ADObjectType type, string? tier = null)
        {
            Id = id;
            Label = label;
            NodeType = type;
            Tier = tier;
        }
    }
}
