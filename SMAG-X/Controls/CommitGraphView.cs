using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SMAGX.Git;

namespace SMAGX.Controls;

/// <summary>
/// Le graphe des commits : une ligne par commit, du plus récent au plus ancien, une colonne par lignée, les liens vers
/// les parents, et devant chaque commit ses étiquettes (HEAD, branches, tags), son identifiant, son message et son auteur.
/// </summary>
public sealed class CommitGraphView : Control
{
    public static readonly StyledProperty<CommitGraph?> GraphProperty =
        AvaloniaProperty.Register<CommitGraphView, CommitGraph?>(nameof(Graph));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextBlock.ForegroundProperty.AddOwner<CommitGraphView>();

    static CommitGraphView() => AffectsMeasure<CommitGraphView>(GraphProperty);

    public CommitGraph? Graph { get => GetValue(GraphProperty); set => SetValue(GraphProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    private const double RowHeight = 34;
    private const double LaneWidth = 20;
    private const double Top = 20;
    private const double Left = 18;
    private const double Radius = 6;

    private static readonly Color[] LaneColors =
        new[] { "#58A6FF", "#3FB950", "#D29922", "#BC8CFF", "#FF7B72", "#39C5CF" }.Select(Color.Parse).ToArray();

    private static readonly Typeface Sans = new(FontFamily.Default);
    private static readonly Typeface SansBold = new(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);
    private static readonly Typeface Mono = new("Cascadia Mono, Consolas, Menlo, DejaVu Sans Mono, monospace");

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GraphProperty || change.Property == ForegroundProperty) InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Graph is null) return new Size(0, 0);
        return new Size(TextStart(Graph) + 640, Top + Graph.Rows.Count * RowHeight);
    }

    private static double X(int lane) => Left + lane * LaneWidth;
    private static double Y(int row) => Top + row * RowHeight;
    private static double TextStart(CommitGraph graph) => Left + graph.LaneCount * LaneWidth + 6;
    private static Color LaneColor(int lane) => LaneColors[lane % LaneColors.Length];

    public override void Render(DrawingContext context)
    {
        if (Graph is not { } graph) return;
        var foreground = Foreground as ISolidColorBrush ?? Brushes.White;
        var muted = new SolidColorBrush(foreground.Color, 0.6);

        foreach (var edge in graph.Edges)
        {
            var pen = new Pen(new SolidColorBrush(LaneColor(edge.ViaLane == edge.FromLane ? edge.FromLane : edge.ViaLane)), 2);
            context.DrawGeometry(null, pen, EdgePath(edge));
        }
        var textStart = TextStart(graph);
        foreach (var row in graph.Rows)
        {
            var center = new Point(X(row.Lane), Y(row.Row));
            var color = LaneColor(row.Lane);
            if (row.IsHead)
                context.DrawEllipse(null, new Pen(new SolidColorBrush(Color.Parse("#F2CC60")), 2.5), center, Radius + 3.5, Radius + 3.5);
            context.DrawEllipse(new SolidColorBrush(color), new Pen(new SolidColorBrush(foreground.Color, 0.9), 1.2), center, Radius, Radius);

            var x = textStart;
            foreach (var label in row.Labels) x = DrawLabel(context, label, x, center.Y) + 5;
            var id = Text(row.Id.Short, Mono, 12.5, muted);
            context.DrawText(id, new Point(x + 2, center.Y - id.Height / 2));
            x += id.Width + 12;
            var subject = Text(row.Commit.Subject, row.IsHead ? SansBold : Sans, 13, foreground);
            context.DrawText(subject, new Point(x, center.Y - subject.Height / 2));
            x += subject.Width + 12;
            var who = Text($"{row.Commit.Author.Name} · {row.Commit.Author.When:dd/MM HH:mm}", Sans, 11.5, muted);
            context.DrawText(who, new Point(x, center.Y - who.Height / 2));
        }
    }

    /// <summary>Le lien d'un commit vers son parent : courbe vers la colonne réservée, ligne droite, courbe vers le parent.</summary>
    private static StreamGeometry EdgePath(GraphEdge edge)
    {
        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        double x1 = X(edge.FromLane), y1 = Y(edge.FromRow);
        double xv = X(edge.ViaLane), x2 = X(edge.ToLane), y2 = Y(edge.ToRow);
        g.BeginFigure(new Point(x1, y1), false);
        var y = y1;
        if (edge.ViaLane != edge.FromLane)
        {
            y = y1 + RowHeight;
            g.CubicBezierTo(new Point(x1, y1 + RowHeight * 0.6), new Point(xv, y1 + RowHeight * 0.4), new Point(xv, y));
        }
        if (edge.ToLane != edge.ViaLane)
        {
            var bend = Math.Max(y, y2 - RowHeight);
            g.LineTo(new Point(xv, bend));
            g.CubicBezierTo(new Point(xv, bend + RowHeight * 0.6), new Point(x2, y2 - RowHeight * 0.4), new Point(x2, y2));
        }
        else g.LineTo(new Point(x2, y2));
        g.EndFigure(false);
        return geometry;
    }

    /// <summary>Une pastille : HEAD en ambre, branche locale en vert (la courante cerclée), distante en rouge, tag en jaune.</summary>
    private static double DrawLabel(DrawingContext context, GraphLabel label, double x, double centerY)
    {
        var color = label.Kind switch
        {
            LabelKind.Head => Color.Parse("#F2CC60"),
            LabelKind.LocalBranch => Color.Parse("#3FB950"),
            LabelKind.RemoteBranch => Color.Parse("#FF7B72"),
            _ => Color.Parse("#D29922")
        };
        var text = Text(label.Kind == LabelKind.Tag ? "tag: " + label.Text : label.Text, SansBold, 11.5, new SolidColorBrush(color));
        var rect = new Rect(x, centerY - 10, text.Width + 14, 20);
        context.DrawRectangle(new SolidColorBrush(color, 0.16), new Pen(new SolidColorBrush(color), label.IsCurrent ? 2 : 1), rect, 6, 6);
        context.DrawText(text, new Point(x + 7, centerY - text.Height / 2));
        return rect.Right;
    }

    private static FormattedText Text(string text, Typeface typeface, double size, IBrush brush) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, size, brush);
}
