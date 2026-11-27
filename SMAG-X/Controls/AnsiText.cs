using System.Text.RegularExpressions;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace SMAGX.Controls;

/// <summary>Le texte d'un terminal en morceaux colorés : les codes ANSI de Git (ESC[31m…ESC[m) deviennent des Run.</summary>
public static partial class AnsiText
{
    public static readonly IBrush Normal = new SolidColorBrush(Color.Parse("#D6DEE6"));
    public static readonly IBrush Red = new SolidColorBrush(Color.Parse("#FF7B72"));
    public static readonly IBrush Green = new SolidColorBrush(Color.Parse("#3FB950"));
    public static readonly IBrush Yellow = new SolidColorBrush(Color.Parse("#D29922"));
    public static readonly IBrush Blue = new SolidColorBrush(Color.Parse("#58A6FF"));
    public static readonly IBrush Cyan = new SolidColorBrush(Color.Parse("#39C5CF"));
    public static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#8B949E"));

    [GeneratedRegex("\u001b\\[([0-9;]*)m")]
    private static partial Regex Sequence();

    /// <summary>Ajoute le texte aux Inlines, couleurs comprises.</summary>
    public static void Append(InlineCollection inlines, string text)
    {
        IBrush brush = Normal;
        var bold = false;
        var at = 0;
        foreach (Match m in Sequence().Matches(text))
        {
            Add(inlines, text[at..m.Index], brush, bold);
            at = m.Index + m.Length;
            foreach (var code in m.Groups[1].Value.Split(';'))
            {
                switch (code)
                {
                    case "" or "0": brush = Normal; bold = false; break;
                    case "1": bold = true; break;
                    case "31": brush = Red; break;
                    case "32": brush = Green; break;
                    case "33": brush = Yellow; break;
                    case "34": brush = Blue; break;
                    case "36": brush = Cyan; break;
                }
            }
        }
        Add(inlines, text[at..], brush, bold);
    }

    /// <summary>L'invite façon bash : « alice@poste » en vert, le dossier en bleu, la branche en jaune.</summary>
    public static void AppendPrompt(InlineCollection inlines, string prompt)
    {
        var m = Regex.Match(prompt, @"^([^:]+):(\S+)( \([^)]*\))?\$ $");
        if (!m.Success)
        {
            Add(inlines, prompt, Normal, false);
            return;
        }
        Add(inlines, m.Groups[1].Value, Green, true);
        Add(inlines, ":", Normal, false);
        Add(inlines, m.Groups[2].Value, Blue, true);
        if (m.Groups[3].Success) Add(inlines, m.Groups[3].Value, Yellow, false);
        Add(inlines, "$ ", Normal, false);
    }

    private static void Add(InlineCollection inlines, string text, IBrush brush, bool bold)
    {
        if (text.Length == 0) return;
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0) inlines.Add(new LineBreak());
            if (lines[i].Length > 0)
                inlines.Add(new Run(lines[i].Replace("\t", "        ")) { Foreground = brush, FontWeight = bold ? FontWeight.Bold : FontWeight.Normal });
        }
    }
}
