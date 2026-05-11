using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using SMADX.Models;

namespace SMADX.Graph
{
    /// <summary>
    /// Canvas custom Avalonia — rendu force-directed du graphe AD (style BloodHound).
    /// </summary>
    public class GraphCanvas : Control
    {
        // ── Typeface partagé ─────────────────────────────────────────────────
        private static readonly Typeface DefaultTypeface =
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal);

        private static readonly Typeface BoldTypeface =
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold, FontStretch.Normal);

        // ── Couleurs par type ────────────────────────────────────────────────
        private static readonly Dictionary<ADObjectType, Color> NodeColors = new()
        {
            { ADObjectType.Domain,                  Color.FromRgb(0x00, 0x78, 0xD4) },
            { ADObjectType.OrganizationalUnit,      Color.FromRgb(0x10, 0x7C, 0x10) },
            { ADObjectType.User,                    Color.FromRgb(0xCA, 0x50, 0x10) },
            { ADObjectType.Group,                   Color.FromRgb(0x88, 0x17, 0x98) },
            { ADObjectType.Computer,                Color.FromRgb(0x00, 0x4E, 0x8C) },
            { ADObjectType.Policy,                  Color.FromRgb(0xB1, 0x46, 0x00) },
            { ADObjectType.PasswordSettingsObject,  Color.FromRgb(0xC5, 0x07, 0x1F) },
            { ADObjectType.GMSA,                    Color.FromRgb(0x00, 0x64, 0x4D) },
            { ADObjectType.Container,               Color.FromRgb(0x60, 0x60, 0x60) },
        };

        private static readonly Dictionary<EdgeType, Color> EdgeColors = new()
        {
            { EdgeType.MemberOf,        Color.FromRgb(0x88, 0x17, 0x98) },
            { EdgeType.GpoLink,         Color.FromRgb(0xB1, 0x46, 0x00) },
            { EdgeType.GpoInheritance,  Color.FromRgb(0xD0, 0x80, 0x00) },
            { EdgeType.PsoSubject,      Color.FromRgb(0xC5, 0x07, 0x1F) },
            { EdgeType.ParentChild,     Color.FromRgb(0x60, 0x60, 0x60) },
        };

        private static readonly Dictionary<ADObjectType, string> NodeIcons = new()
        {
            { ADObjectType.Domain,               "🌐" },
            { ADObjectType.OrganizationalUnit,   "📁" },
            { ADObjectType.User,                 "👤" },
            { ADObjectType.Group,                "👥" },
            { ADObjectType.Computer,             "💻" },
            { ADObjectType.Policy,               "📋" },
            { ADObjectType.PasswordSettingsObject,"🔑" },
            { ADObjectType.GMSA,                 "🔐" },
            { ADObjectType.Container,            "📦" },
        };

        // ── État ─────────────────────────────────────────────────────────────
        private List<GraphNode> _nodes = new();
        private List<GraphEdge> _edges = new();
        private ForceSimulation? _simulation;

        private double _offsetX, _offsetY;
        private double _scale = 1.0;

        private bool _isPanning;
        private Point _panStart;

        private GraphNode? _dragNode;
        private Point _dragOffset;

        private GraphNode? _hoveredNode;
        private GraphNode? _selectedNode;

        private readonly DispatcherTimer _timer;
        private int _simStepsRemaining;

        public event Action<GraphNode?>? NodeSelected;

        public GraphCanvas()
        {
            ClipToBounds = true;
            Focusable = true;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _timer.Tick += (_, _) =>
            {
                if (_simStepsRemaining > 0)
                {
                    _simulation?.Step(2);
                    _simStepsRemaining -= 2;
                    InvalidateVisual();
                }
                else
                {
                    _timer.Stop();
                }
            };

            // Démarrer le timer uniquement quand le control est dans le visual tree,
            // l'arrêter dès qu'il en est retiré (fermeture de GraphWindow).
            AttachedToVisualTree   += (_, _) => { if (_simStepsRemaining > 0) _timer.Start(); };
            DetachedFromVisualTree += (_, _) => _timer.Stop();
        }

        // ── API publique ──────────────────────────────────────────────────────

        public void LoadGraph(List<GraphNode> nodes, List<GraphEdge> edges)
        {
            _nodes = nodes;
            _edges = edges;
            _selectedNode = null;
            _hoveredNode = null;
            _simulation = new ForceSimulation(_nodes, _edges);
            _simStepsRemaining = 300;
            _timer.Start();
            FitView();
            InvalidateVisual();
        }

        public void ReLayout()
        {
            var rng = new Random();
            foreach (var n in _nodes)
            {
                double angle = 2 * Math.PI * rng.NextDouble();
                double r = 150 + rng.NextDouble() * 200;
                n.X = Math.Cos(angle) * r;
                n.Y = Math.Sin(angle) * r;
                n.Vx = n.Vy = 0;
            }
            _simulation = new ForceSimulation(_nodes, _edges);
            _simStepsRemaining = 300;
            _timer.Start();
        }

        public void FitView()
        {
            if (_nodes.Count == 0) return;
            double minX = _nodes.Min(n => n.X - n.Radius);
            double maxX = _nodes.Max(n => n.X + n.Radius);
            double minY = _nodes.Min(n => n.Y - n.Radius);
            double maxY = _nodes.Max(n => n.Y + n.Radius);

            double graphW = maxX - minX + 100;
            double graphH = maxY - minY + 100;
            double bw = Bounds.Width > 0 ? Bounds.Width : 800;
            double bh = Bounds.Height > 0 ? Bounds.Height : 600;

            _scale = Math.Min(bw / graphW, bh / graphH);
            _scale = Math.Max(0.05, Math.Min(_scale, 3.0));

            double cx = (minX + maxX) / 2.0;
            double cy = (minY + maxY) / 2.0;
            _offsetX = bw / 2.0 - cx * _scale;
            _offsetY = bh / 2.0 - cy * _scale;
            InvalidateVisual();
        }

        public void ZoomIn()  => Zoom(1.25, new Point(Bounds.Width / 2, Bounds.Height / 2));
        public void ZoomOut() => Zoom(0.80, new Point(Bounds.Width / 2, Bounds.Height / 2));

        private void Zoom(double factor, Point pivot)
        {
            _offsetX = pivot.X - (pivot.X - _offsetX) * factor;
            _offsetY = pivot.Y - (pivot.Y - _offsetY) * factor;
            _scale = Math.Max(0.05, Math.Min(_scale * factor, 6.0));
            InvalidateVisual();
        }

        // ── Rendu ─────────────────────────────────────────────────────────────

        public override void Render(DrawingContext ctx)
        {
            ctx.DrawRectangle(new SolidColorBrush(Color.FromRgb(18, 20, 26)), null,
                new Rect(0, 0, Bounds.Width, Bounds.Height));

            if (_nodes.Count == 0)
            {
                DrawCenteredText(ctx,
                    "Aucune relation à afficher.\n" +
                    "Configurez des relations (Memberships, GPO, PSO) puis rechargez.",
                    14, Color.FromArgb(160, 180, 180, 180),
                    Bounds.Width / 2, Bounds.Height / 2);
                return;
            }

            var transform = Matrix.CreateTranslation(_offsetX, _offsetY) *
                            Matrix.CreateScale(_scale, _scale);
            using (ctx.PushTransform(transform))
            {
                DrawEdges(ctx);
                DrawNodes(ctx);
            }

            DrawLegend(ctx);
            DrawHint(ctx);
            DrawSelectionInfo(ctx);
            DrawTooltip(ctx);
        }

        private void DrawEdges(DrawingContext ctx)
        {
            foreach (var edge in _edges)
            {
                var color = EdgeColors.TryGetValue(edge.Type, out var c) ? c : Colors.Gray;
                bool highlighted = _selectedNode != null &&
                    (edge.Source == _selectedNode || edge.Target == _selectedNode);
                bool dimmed = _selectedNode != null && !highlighted;
                bool isInherited = edge.Type == EdgeType.GpoInheritance;

                byte alpha = dimmed ? (byte)20 : (byte)(highlighted ? 255 : (isInherited ? 110 : 150));
                double width = highlighted ? 2.5 : (isInherited ? 1.2 : 1.0);

                Pen pen;
                if (isInherited)
                {
                    pen = new Pen(
                        new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B)),
                        width,
                        new DashStyle(new double[] { 6, 4 }, 0));
                }
                else
                {
                    pen = new Pen(new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B)), width);
                }

                double sx = edge.Source.X, sy = edge.Source.Y;
                double tx = edge.Target.X, ty = edge.Target.Y;
                double dx = tx - sx, dy = ty - sy;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len < 1) continue;

                double nx = dx / len, ny = dy / len;
                var start = new Point(sx + nx * edge.Source.Radius, sy + ny * edge.Source.Radius);
                var end   = new Point(tx - nx * (edge.Target.Radius + 9), ty - ny * (edge.Target.Radius + 9));

                ctx.DrawLine(pen, start, end);
                if (!dimmed) DrawArrow(ctx, pen, end, nx, ny, 9);

                if (highlighted)
                {
                    string label = isInherited && !string.IsNullOrEmpty(edge.GpoName)
                        ? $"⬇ {edge.GpoName}"
                        : edge.Label;
                    DrawEdgeLabel(ctx, label, (start.X + end.X) / 2, (start.Y + end.Y) / 2, color);
                }
            }
        }

        private static void DrawArrow(DrawingContext ctx, Pen pen, Point tip, double nx, double ny, double size)
        {
            double px = -ny, py = nx;
            ctx.DrawLine(pen, tip, new Point(tip.X - nx * size + px * size * 0.4, tip.Y - ny * size + py * size * 0.4));
            ctx.DrawLine(pen, tip, new Point(tip.X - nx * size - px * size * 0.4, tip.Y - ny * size - py * size * 0.4));
        }

        private void DrawEdgeLabel(DrawingContext ctx, string text, double x, double y, Color color)
        {
            var ft = MakeFT(text, 9, Color.FromArgb(220, color.R, color.G, color.B));
            double w = ft.Width, h = ft.Height;
            ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(160, 18, 20, 26)), null,
                new Rect(x - w / 2 - 3, y - h / 2 - 2, w + 6, h + 4));
            ctx.DrawText(ft, new Point(x - w / 2, y - h / 2));
        }

        private void DrawNodes(DrawingContext ctx)
        {
            // Calculer les nœuds connectés au nœud sélectionné
            HashSet<GraphNode>? connected = null;
            if (_selectedNode != null)
            {
                connected = new HashSet<GraphNode> { _selectedNode };
                foreach (var e in _edges)
                {
                    if (e.Source == _selectedNode) connected.Add(e.Target);
                    if (e.Target == _selectedNode) connected.Add(e.Source);
                }
            }

            // Dessiner d'abord les nœuds non-sélectionnés
            foreach (var node in _nodes)
            {
                if (node == _selectedNode) continue;
                DrawNode(ctx, node, connected);
            }

            // Dessiner le nœud sélectionné en dernier (toujours au-dessus)
            if (_selectedNode != null)
                DrawNode(ctx, _selectedNode, connected);
        }

        private void DrawNode(DrawingContext ctx, GraphNode node, HashSet<GraphNode>? connected)
        {
            var baseColor = NodeColors.TryGetValue(node.NodeType, out var nc) ? nc : Colors.Gray;
            bool isSel  = node == _selectedNode;
            bool isHov  = node == _hoveredNode && !isSel;
            bool isDim  = connected != null && !connected.Contains(node);
            var center  = new Point(node.X, node.Y);

            // ── Halo de sélection jaune vif ──────────────────────────────────
            if (isSel)
            {
                // Anneau externe pulsé (double ring)
                ctx.DrawEllipse(null,
                    new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 215, 0)), 12),
                    center, node.Radius + 18, node.Radius + 18);
                ctx.DrawEllipse(null,
                    new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 215, 0)), 3),
                    center, node.Radius + 9, node.Radius + 9);
            }

            // ── Halo hover ───────────────────────────────────────────────────
            if (isHov)
            {
                ctx.DrawEllipse(
                    new SolidColorBrush(Color.FromArgb(50, baseColor.R, baseColor.G, baseColor.B)),
                    new Pen(new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)), 1.5),
                    center, node.Radius + 8, node.Radius + 8);
            }

            // ── Cercle principal ─────────────────────────────────────────────
            byte dimAlpha = (byte)(isDim ? 50 : 255);
            double br = isSel ? 1.25 : isHov ? 1.12 : 1.0;
            var fillColor = ScaleBrightness(baseColor, br);
            var fill = new SolidColorBrush(Color.FromArgb(dimAlpha, fillColor.R, fillColor.G, fillColor.B));
            var borderAlpha = (byte)(isDim ? 40 : (isSel ? 255 : 180));
            var border = new Pen(new SolidColorBrush(Color.FromArgb(borderAlpha, 255, 255, 255)),
                                 isSel ? 2.5 : 1.2);
            ctx.DrawEllipse(fill, border, center, node.Radius, node.Radius);

            // ── Icône ────────────────────────────────────────────────────────
            var icon = NodeIcons.TryGetValue(node.NodeType, out var ic) ? ic : "?";
            double iconSize = node.Radius * 0.85;
            var iconBrush = new SolidColorBrush(Color.FromArgb(dimAlpha, 255, 255, 255));
            var ift = MakeFT(icon, iconSize, iconBrush);
            ctx.DrawText(ift, new Point(node.X - ift.Width / 2, node.Y - ift.Height / 2));

            // ── Label sous le nœud ───────────────────────────────────────────
            var labelTypeface = isSel ? BoldTypeface : DefaultTypeface;
            var lColor = isSel
                ? Colors.White
                : isDim
                    ? Color.FromArgb(60, 180, 180, 180)
                    : Color.FromArgb(220, 220, 220, 220);
            var lft = MakeFTWith(Truncate(node.Label, 22), 11, new SolidColorBrush(lColor), labelTypeface);
            double lx = node.X - lft.Width / 2;
            double ly = node.Y + node.Radius + 5;
            if (!isDim)
            {
                ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(150, 18, 20, 26)), null,
                    new Rect(lx - 3, ly - 1, lft.Width + 6, lft.Height + 2));
            }
            ctx.DrawText(lft, new Point(lx, ly));

            // ── Tier badge ───────────────────────────────────────────────────
            if (!string.IsNullOrEmpty(node.Tier) && !isDim)
            {
                var tft = MakeFT(node.Tier, 8, Colors.White);
                double bx = node.X + node.Radius * 0.55 - tft.Width / 2;
                double by = node.Y - node.Radius * 0.85;
                ctx.DrawEllipse(new SolidColorBrush(Color.FromRgb(60, 60, 60)), null,
                    new Point(bx + tft.Width / 2, by + tft.Height / 2),
                    tft.Width / 2 + 5, tft.Height / 2 + 3);
                ctx.DrawText(tft, new Point(bx, by));
            }
        }

        private void DrawLegend(DrawingContext ctx)
        {
            var nodeEntries = new (ADObjectType type, string label)[]
            {
                (ADObjectType.Domain,               "Domain"),
                (ADObjectType.OrganizationalUnit,   "OU"),
                (ADObjectType.User,                 "User"),
                (ADObjectType.Group,                "Group"),
                (ADObjectType.Computer,             "Computer"),
                (ADObjectType.Policy,               "GPO"),
                (ADObjectType.PasswordSettingsObject,"PSO"),
            };

            // Arêtes de légende (trait plein + pointillé pour héritage)
            var edgeEntries = new (Color color, bool dashed, string label)[]
            {
                (EdgeColors[EdgeType.GpoLink],        false, "GPO lié"),
                (EdgeColors[EdgeType.GpoInheritance], true,  "GPO hérité"),
                (EdgeColors[EdgeType.MemberOf],       false, "MemberOf"),
                (EdgeColors[EdgeType.PsoSubject],     false, "PSO Subject"),
            };

            const double lx = 10;
            double ly = 10;
            const double rowH = 20;
            double boxH = rowH * (nodeEntries.Length + edgeEntries.Length + 2) + 14;

            ctx.DrawRectangle(
                new SolidColorBrush(Color.FromArgb(170, 18, 20, 26)),
                new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 1),
                new Rect(lx - 4, ly - 4, 145, boxH));

            var title = MakeFT("Légende", 11, Color.FromArgb(200, 255, 255, 255));
            ctx.DrawText(title, new Point(lx + 2, ly));
            ly += rowH;

            foreach (var (type, label) in nodeEntries)
            {
                var color = NodeColors.TryGetValue(type, out var c) ? c : Colors.Gray;
                ctx.DrawEllipse(new SolidColorBrush(color), null, new Point(lx + 7, ly + 7), 7, 7);
                var ft = MakeFT(label, 11, Color.FromArgb(200, 220, 220, 220));
                ctx.DrawText(ft, new Point(lx + 20, ly + 1));
                ly += rowH;
            }

            // Séparateur
            ctx.DrawLine(
                new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 1),
                new Point(lx, ly), new Point(lx + 133, ly));
            ly += 6;

            foreach (var (color, dashed, label) in edgeEntries)
            {
                Pen linePen = dashed
                    ? new Pen(new SolidColorBrush(Color.FromArgb(200, color.R, color.G, color.B)), 1.5,
                        new DashStyle(new double[] { 5, 3 }, 0))
                    : new Pen(new SolidColorBrush(Color.FromArgb(200, color.R, color.G, color.B)), 1.5);
                ctx.DrawLine(linePen, new Point(lx + 2, ly + 8), new Point(lx + 18, ly + 8));
                var ft = MakeFT(label, 11, Color.FromArgb(200, 220, 220, 220));
                ctx.DrawText(ft, new Point(lx + 22, ly + 1));
                ly += rowH;
            }
        }

        private void DrawHint(DrawingContext ctx)
        {
            string hint = $"Nœuds: {_nodes.Count}  |  Arêtes: {_edges.Count}" +
                          "    [Scroll: zoom  •  Glisser fond: pan  •  Glisser nœud: déplacer  •  Clic: sélectionner]";
            var ft = MakeFT(hint, 11, Color.FromArgb(120, 200, 200, 200));
            ctx.DrawText(ft, new Point(Bounds.Width / 2 - ft.Width / 2, Bounds.Height - 22));
        }

        private void DrawSelectionInfo(DrawingContext ctx)
        {
            var node = _selectedNode;
            if (node == null) return;

            // Compter les connexions
            int connIn  = _edges.Count(e => e.Target == node);
            int connOut = _edges.Count(e => e.Source == node);

            string typeName = node.NodeType.ToString();
            string line1 = node.Label;
            string line2 = $"Type: {typeName}";
            string line3 = $"Relations entrantes: {connIn}   sortantes: {connOut}";
            if (!string.IsNullOrEmpty(node.Tier))
                line3 += $"   {node.Tier}";

            var ft1 = MakeFTWith(line1, 13, new SolidColorBrush(Colors.White), BoldTypeface);
            var ft2 = MakeFT(line2, 11, Color.FromArgb(200, 200, 200, 200));
            var ft3 = MakeFT(line3, 11, Color.FromArgb(160, 180, 180, 180));

            double panelW = Math.Max(ft1.Width, Math.Max(ft2.Width, ft3.Width)) + 20;
            double panelH = ft1.Height + ft2.Height + ft3.Height + 18;
            double px = Bounds.Width - panelW - 10;
            double py = 10;

            // Fond avec bordure jaune pour indiquer la sélection
            ctx.DrawRectangle(
                new SolidColorBrush(Color.FromArgb(210, 18, 20, 26)),
                new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 215, 0)), 1.5),
                new Rect(px, py, panelW, panelH), 6, 6);

            // Pastille couleur du type
            var nodeColor = NodeColors.TryGetValue(node.NodeType, out var nc) ? nc : Colors.Gray;
            ctx.DrawEllipse(new SolidColorBrush(nodeColor), null,
                new Point(px + 12, py + 11), 6, 6);

            double ty = py + 6;
            ctx.DrawText(ft1, new Point(px + 24, ty));
            ty += ft1.Height + 3;
            ctx.DrawText(ft2, new Point(px + 10, ty));
            ty += ft2.Height + 2;
            ctx.DrawText(ft3, new Point(px + 10, ty));
        }

        private void DrawTooltip(DrawingContext ctx)
        {
            var node = _hoveredNode;
            if (node == null || node == _selectedNode) return;

            // Position en coordonnées écran du nœud
            double sx = node.X * _scale + _offsetX;
            double sy = node.Y * _scale + _offsetY;

            string text = node.Label + " (" + node.NodeType + ")";
            var ft = MakeFT(text, 11, Colors.White);
            double tw = ft.Width + 12, th = ft.Height + 8;
            double tx = sx - tw / 2;
            double ty = sy - node.Radius * _scale - th - 6;
            tx = Math.Max(2, Math.Min(tx, Bounds.Width - tw - 2));
            ty = Math.Max(2, ty);

            ctx.DrawRectangle(
                new SolidColorBrush(Color.FromArgb(220, 30, 32, 40)),
                new Pen(new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)), 1),
                new Rect(tx, ty, tw, th), 4, 4);
            ctx.DrawText(ft, new Point(tx + 6, ty + 4));
        }

        private void DrawCenteredText(DrawingContext ctx, string text, double size, Color color, double cx, double cy)
        {
            var ft = MakeFT(text, size, color);
            ctx.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2));
        }

        // ── Helpers FormattedText ─────────────────────────────────────────────

        private static FormattedText MakeFT(string text, double size, Color color)
            => new FormattedText(text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                DefaultTypeface,
                size,
                new SolidColorBrush(color));

        private static FormattedText MakeFT(string text, double size, IBrush brush)
            => new FormattedText(text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                DefaultTypeface,
                size,
                brush);

        private static FormattedText MakeFTWith(string text, double size, IBrush brush, Typeface typeface)
            => new FormattedText(text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                size,
                brush);

        // ── Interactions ──────────────────────────────────────────────────────

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            var pos = e.GetPosition(this);
            double factor = e.Delta.Y > 0 ? 1.12 : 1.0 / 1.12;
            Zoom(factor, pos);
            e.Handled = true;
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            var pos = e.GetPosition(this);
            var world = ScreenToWorld(pos);
            var hit = HitTest(world);

            if (hit != null)
            {
                _dragNode = hit;
                _dragOffset = new Point(hit.X - world.X, hit.Y - world.Y);
                hit.IsDragging = true;
                _selectedNode = hit;
                NodeSelected?.Invoke(hit);
            }
            else
            {
                _isPanning = true;
                _panStart  = new Point(pos.X - _offsetX, pos.Y - _offsetY);
                _selectedNode = null;
                NodeSelected?.Invoke(null);
            }
            e.Handled = true;
            InvalidateVisual();
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            var pos   = e.GetPosition(this);
            var world = ScreenToWorld(pos);

            if (_dragNode != null)
            {
                _dragNode.X  = world.X + _dragOffset.X;
                _dragNode.Y  = world.Y + _dragOffset.Y;
                _dragNode.Vx = _dragNode.Vy = 0;
                InvalidateVisual();
            }
            else if (_isPanning)
            {
                _offsetX = pos.X - _panStart.X;
                _offsetY = pos.Y - _panStart.Y;
                InvalidateVisual();
            }
            else
            {
                var prev = _hoveredNode;
                _hoveredNode = HitTest(world);
                if (_hoveredNode != prev)
                {
                    Cursor = _hoveredNode != null
                        ? new Cursor(StandardCursorType.Hand)
                        : Cursor.Default;
                    InvalidateVisual();
                }
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (_dragNode != null)
            {
                _dragNode.IsDragging = false;
                _dragNode = null;
            }
            _isPanning = false;
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            if (_hoveredNode != null)
            {
                _hoveredNode = null;
                Cursor = Cursor.Default;
                InvalidateVisual();
            }
        }

        // ── Utilitaires ───────────────────────────────────────────────────────

        private Point ScreenToWorld(Point screen)
            => new Point((screen.X - _offsetX) / _scale, (screen.Y - _offsetY) / _scale);

        private GraphNode? HitTest(Point world)
        {
            foreach (var n in _nodes)
            {
                double dx = world.X - n.X, dy = world.Y - n.Y;
                if (dx * dx + dy * dy <= (n.Radius + 8) * (n.Radius + 8))
                    return n;
            }
            return null;
        }

        private static Color ScaleBrightness(Color c, double factor)
        {
            byte Clamp(double v) => (byte)Math.Max(0, Math.Min(255, v));
            return Color.FromRgb(Clamp(c.R * factor), Clamp(c.G * factor), Clamp(c.B * factor));
        }

        private static string Truncate(string s, int max)
            => s.Length <= max ? s : s[..max] + "…";
    }
}
