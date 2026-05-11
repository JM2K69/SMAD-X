using System;
using System.Collections.Generic;
using System.Linq;

namespace SMADX.Graph
{
    /// <summary>
    /// Simulation force-directed (Fruchterman-Reingold simplifié) pour positionner les nœuds
    /// </summary>
    public class ForceSimulation
    {
        private const double RepulsionK = 8000.0;
        private const double AttractionK = 0.05;
        private const double Damping = 0.85;
        private const double MaxDisplace = 30.0;

        private readonly List<GraphNode> _nodes;
        private readonly List<GraphEdge> _edges;

        public ForceSimulation(List<GraphNode> nodes, List<GraphEdge> edges)
        {
            _nodes = nodes;
            _edges = edges;
        }

        /// <summary>
        /// Effectue N itérations de simulation
        /// </summary>
        public void Step(int iterations = 1)
        {
            for (int iter = 0; iter < iterations; iter++)
            {
                // Reset forces
                foreach (var n in _nodes) { n.Fx = 0; n.Fy = 0; }

                // Répulsion entre tous les nœuds
                for (int i = 0; i < _nodes.Count; i++)
                {
                    for (int j = i + 1; j < _nodes.Count; j++)
                    {
                        var a = _nodes[i];
                        var b = _nodes[j];
                        double dx = a.X - b.X;
                        double dy = a.Y - b.Y;
                        double distSq = dx * dx + dy * dy + 1.0;
                        double dist = Math.Sqrt(distSq);
                        double force = RepulsionK / distSq;
                        double fx = force * dx / dist;
                        double fy = force * dy / dist;
                        a.Fx += fx; a.Fy += fy;
                        b.Fx -= fx; b.Fy -= fy;
                    }
                }

                // Attraction le long des arêtes
                foreach (var edge in _edges)
                {
                    var a = edge.Source;
                    var b = edge.Target;
                    double dx = b.X - a.X;
                    double dy = b.Y - a.Y;
                    double dist = Math.Sqrt(dx * dx + dy * dy) + 0.001;
                    double force = AttractionK * dist;
                    double fx = force * dx / dist;
                    double fy = force * dy / dist;
                    a.Fx += fx; a.Fy += fy;
                    b.Fx -= fx; b.Fy -= fy;
                }

                // Mise à jour des positions (ne pas déplacer les nœuds en drag)
                foreach (var n in _nodes)
                {
                    if (n.IsDragging) continue;
                    n.Vx = (n.Vx + n.Fx) * Damping;
                    n.Vy = (n.Vy + n.Fy) * Damping;
                    double disp = Math.Sqrt(n.Vx * n.Vx + n.Vy * n.Vy);
                    if (disp > MaxDisplace)
                    {
                        n.Vx = n.Vx / disp * MaxDisplace;
                        n.Vy = n.Vy / disp * MaxDisplace;
                    }
                    n.X += n.Vx;
                    n.Y += n.Vy;
                }
            }
        }
    }
}
