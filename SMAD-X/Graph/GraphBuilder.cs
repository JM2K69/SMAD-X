using System;
using System.Collections.Generic;
using System.Linq;
using SMADX.Models;

namespace SMADX.Graph
{
    /// <summary>
    /// Construit le graphe de relations depuis l'arbre ADObject et applique un layout force-directed
    /// </summary>
    public class GraphBuilder
    {
        public List<GraphNode> Nodes { get; } = new();
        public List<GraphEdge> Edges { get; } = new();

        private readonly Dictionary<string, GraphNode> _nodeMap = new();

        // Index nom → ADObject pour la résolution transitive
        private readonly Dictionary<string, ADObject> _objectMap = new();

        public void Build(ADObject root, GraphFilter filter)
        {
            Nodes.Clear();
            Edges.Clear();
            _nodeMap.Clear();
            _objectMap.Clear();

            // Collecter tous les objets
            var allObjects = new List<ADObject>();
            CollectAll(root, allObjects);

            // Créer les nœuds
            foreach (var obj in allObjects)
            {
                var node = new GraphNode(obj.Id.ToString(), obj.Name, obj.Type, obj.Tier);
                Nodes.Add(node);
                _nodeMap[obj.Name]   = node;
                _objectMap[obj.Name] = obj;
            }

            // Arêtes de hiérarchie parent-enfant (optionnel)
            if (filter.ShowHierarchy)
            {
                foreach (var obj in allObjects)
                {
                    foreach (var child in obj.Children)
                    {
                        if (_nodeMap.TryGetValue(obj.Name, out var srcNode) &&
                            _nodeMap.TryGetValue(child.Name, out var tgtNode))
                        {
                            Edges.Add(new GraphEdge(srcNode, tgtNode, EdgeType.ParentChild));
                        }
                    }
                }
            }

            // ── MemberOf transitif (User/Computer/GMSA → Group, profondeur illimitée) ──
            if (filter.ShowMemberOf)
            {
                foreach (var obj in allObjects.Where(o =>
                    o.Type is ADObjectType.User or ADObjectType.Computer or ADObjectType.GMSA
                    && o.MemberOf.Count > 0))
                {
                    if (!_nodeMap.TryGetValue(obj.Name, out var srcNode)) continue;

                    // Résolution transitive : BFS sur toute la chaîne de groupes
                    var visited = new HashSet<string>();
                    var queue   = new Queue<string>(obj.MemberOf);

                    while (queue.Count > 0)
                    {
                        var grpName = queue.Dequeue();
                        if (!visited.Add(grpName)) continue;
                        if (!_nodeMap.TryGetValue(grpName, out var tgtNode)) continue;

                        // Arête directe ou transitive vers ce groupe
                        bool alreadyEdge = Edges.Any(e =>
                            e.Type == EdgeType.MemberOf &&
                            e.Source == srcNode &&
                            e.Target == tgtNode);
                        if (!alreadyEdge)
                            Edges.Add(new GraphEdge(srcNode, tgtNode, EdgeType.MemberOf));

                        // Continuer vers les groupes parents de ce groupe
                        if (_objectMap.TryGetValue(grpName, out var grpObj))
                            foreach (var parent in grpObj.MemberOf)
                                if (!visited.Contains(parent))
                                    queue.Enqueue(parent);
                    }
                }
            }

            // ── Group-in-Group transitif (profondeur illimitée) ──
            if (filter.ShowGroupNesting)
            {
                foreach (var obj in allObjects.Where(o =>
                    o.Type == ADObjectType.Group && o.MemberOf.Count > 0))
                {
                    if (!_nodeMap.TryGetValue(obj.Name, out var srcNode)) continue;

                    var visited = new HashSet<string>();
                    var queue   = new Queue<string>(obj.MemberOf);

                    while (queue.Count > 0)
                    {
                        var grpName = queue.Dequeue();
                        if (!visited.Add(grpName)) continue;
                        if (!_nodeMap.TryGetValue(grpName, out var tgtNode)) continue;

                        bool alreadyEdge = Edges.Any(e =>
                            e.Type == EdgeType.GroupNesting &&
                            e.Source == srcNode &&
                            e.Target == tgtNode);
                        if (!alreadyEdge)
                            Edges.Add(new GraphEdge(srcNode, tgtNode, EdgeType.GroupNesting));

                        if (_objectMap.TryGetValue(grpName, out var grpObj))
                            foreach (var parent in grpObj.MemberOf)
                                if (!visited.Contains(parent))
                                    queue.Enqueue(parent);
                    }
                }
            }

            // ── GPO Links (Domain + OU) ──
            if (filter.ShowGpoLinks)
            {
                foreach (var container in allObjects.Where(o =>
                    (o.Type == ADObjectType.OrganizationalUnit || o.Type == ADObjectType.Domain)
                    && o.LinkedGPOs.Count > 0))
                {
                    if (!_nodeMap.TryGetValue(container.Name, out var srcNode)) continue;
                    foreach (var gpo in container.LinkedGPOs)
                    {
                        if (_nodeMap.TryGetValue(gpo, out var tgtNode))
                            Edges.Add(new GraphEdge(srcNode, tgtNode, EdgeType.GpoLink));
                    }
                }
            }

            // ── GPO Héritage : propagation récursive sur toute la profondeur ──
            if (filter.ShowGpoInheritance)
            {
                // Ne lancer la propagation que depuis les racines (Domain, ou OUs sans parent OU)
                foreach (var container in allObjects.Where(o =>
                    o.Type == ADObjectType.Domain ||
                    (o.Type == ADObjectType.OrganizationalUnit && o.LinkedGPOs.Count > 0)))
                {
                    foreach (var gpoName in container.LinkedGPOs)
                    {
                        if (!_nodeMap.TryGetValue(gpoName, out var gpoNode)) continue;
                        PropagateGpoInheritance(container, gpoNode, gpoName);
                    }
                }
            }

            // ── PSO Subjects ──
            if (filter.ShowPsoLinks)
            {
                foreach (var pso in allObjects.Where(o =>
                    o.Type == ADObjectType.PasswordSettingsObject && o.PSOAppliesTo.Count > 0))
                {
                    if (!_nodeMap.TryGetValue(pso.Name, out var srcNode)) continue;
                    foreach (var t in pso.PSOAppliesTo)
                    {
                        if (_nodeMap.TryGetValue(t, out var tgtNode))
                            Edges.Add(new GraphEdge(srcNode, tgtNode, EdgeType.PsoSubject));
                    }
                }
            }

            // Supprimer les nœuds isolés si le filtre le demande
            if (!filter.ShowIsolated)
            {
                var connected = new HashSet<GraphNode>(
                    Edges.SelectMany(e => new[] { e.Source, e.Target }));
                Nodes.RemoveAll(n => !connected.Contains(n));
            }

            ApplyInitialLayout();
        }

        /// <summary>
        /// Positionne les nœuds en cercle avant la simulation
        /// </summary>
        private void ApplyInitialLayout()
        {
            var rng = new Random(42);
            for (int i = 0; i < Nodes.Count; i++)
            {
                double angle = 2 * Math.PI * i / Math.Max(1, Nodes.Count);
                double r = 200 + rng.NextDouble() * 100;
                Nodes[i].X = Math.Cos(angle) * r;
                Nodes[i].Y = Math.Sin(angle) * r;
            }
        }

        /// <summary>
        /// Propage une arête d'héritage GPO de manière récursive et illimitée en profondeur.
        /// Traverse tous les types de conteneurs enfants (OU et Container) pour ne pas rater
        /// des OUs imbriquées dans des Containers intermédiaires.
        /// </summary>
        private void PropagateGpoInheritance(ADObject parent, GraphNode gpoNode, string gpoName)
        {
            foreach (var child in parent.Children)
            {
                if (child.Type == ADObjectType.OrganizationalUnit)
                {
                    if (!_nodeMap.TryGetValue(child.Name, out var childNode)) continue;

                    bool hasDirectLink = child.LinkedGPOs.Contains(gpoName);
                    if (!hasDirectLink)
                    {
                        bool alreadyExists = Edges.Any(e =>
                            e.Type == EdgeType.GpoInheritance &&
                            e.Source == childNode &&
                            e.Target == gpoNode);
                        if (!alreadyExists)
                            Edges.Add(new GraphEdge(childNode, gpoNode, EdgeType.GpoInheritance, gpoName));
                    }

                    // Propager dans la sous-OU (OUs imbriquées + objets enfants)
                    PropagateGpoInheritance(child, gpoNode, gpoName);
                }
                else if (child.Type == ADObjectType.Container)
                {
                    // Traverser les Containers sans ajouter d'arête
                    PropagateGpoInheritance(child, gpoNode, gpoName);
                }
                else if (child.Type is ADObjectType.Computer or ADObjectType.User or ADObjectType.GMSA)
                {
                    // Les objets dans une OU héritent des GPOs de leur OU parente
                    if (!_nodeMap.TryGetValue(child.Name, out var childNode)) continue;

                    bool alreadyExists = Edges.Any(e =>
                        e.Type == EdgeType.GpoInheritance &&
                        e.Source == childNode &&
                        e.Target == gpoNode);
                    if (!alreadyExists)
                        Edges.Add(new GraphEdge(childNode, gpoNode, EdgeType.GpoInheritance, gpoName));
                }
            }
        }

        private static void CollectAll(ADObject obj, List<ADObject> result)
        {
            result.Add(obj);
            foreach (var child in obj.Children)
                CollectAll(child, result);
        }
    }
}
