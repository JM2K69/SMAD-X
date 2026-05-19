using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Layout;
using SMADX.Services;

namespace SMADX.Converters
{
    /// <summary>
    /// Convertisseur avancé pour afficher du Markdown dans Avalonia avec styles riches
    /// </summary>
    public class MarkdownConverter : IValueConverter
    {
        private static bool IsDark =>
            ThemeService.Instance.CurrentTheme == Services.AppTheme.Dark;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string markdown || string.IsNullOrWhiteSpace(markdown))
                return new StackPanel();

            var stackPanel = new StackPanel { Spacing = 8 };
            var lines = markdown.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // Séparateur horizontal (--- ou ***)
                if (line.Trim() == "---" || line.Trim() == "***" || line.Trim() == "___")
                {
                    stackPanel.Children.Add(new Border
                    {
                        Height = 1,
                        Background = new SolidColorBrush(Color.Parse("#888888")),
                        Margin = new Avalonia.Thickness(0, 12, 0, 12)
                    });
                    continue;
                }

                // Titres (# ## ###)
                if (line.StartsWith("### "))
                {
                    stackPanel.Children.Add(new TextBlock
                    {
                        Text = line.Substring(4),
                        FontSize = 16,
                        FontWeight = FontWeight.SemiBold,
                        Margin = new Avalonia.Thickness(0, 12, 0, 4),
                        Foreground = new SolidColorBrush(Color.Parse(IsDark ? "#7DD3FC" : "#4A9EFF"))
                    });
                }
                else if (line.StartsWith("## "))
                {
                    stackPanel.Children.Add(new TextBlock
                    {
                        Text = line.Substring(3),
                        FontSize = 18,
                        FontWeight = FontWeight.SemiBold,
                        Margin = new Avalonia.Thickness(0, 14, 0, 4),
                        Foreground = new SolidColorBrush(Color.Parse(IsDark ? "#93C5FD" : "#3A8EEF"))
                    });
                }
                else if (line.StartsWith("# "))
                {
                    stackPanel.Children.Add(new TextBlock
                    {
                        Text = line.Substring(2),
                        FontSize = 22,
                        FontWeight = FontWeight.Bold,
                        Margin = new Avalonia.Thickness(0, 16, 0, 6),
                        Foreground = new SolidColorBrush(Color.Parse(IsDark ? "#BAE6FD" : "#2A7EDF"))
                    });
                }
                // Citation (>)
                else if (line.TrimStart().StartsWith("> "))
                {
                    var text = line.TrimStart().Substring(2);
                    stackPanel.Children.Add(new Border
                    {
                        BorderBrush = new SolidColorBrush(Color.Parse(IsDark ? "#4A9EFF" : "#4A9EFF")),
                        BorderThickness = new Avalonia.Thickness(4, 0, 0, 0),
                        Background = new SolidColorBrush(Color.Parse(IsDark ? "#1A2D3F" : "#F0F8FF")),
                        Padding = new Avalonia.Thickness(12, 8, 12, 8),
                        Margin = new Avalonia.Thickness(0, 4, 0, 4),
                        Child = CreateInlineTextBlock(text, true)
                    });
                }
                // Listes à puces (- ou *)
                else if (line.TrimStart().StartsWith("- ") || line.TrimStart().StartsWith("* "))
                {
                    var indent = line.Length - line.TrimStart().Length;
                    var text = line.TrimStart().Substring(2);

                    var panel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Avalonia.Thickness(indent * 10 + 10, 2, 0, 2)
                    };

                    panel.Children.Add(new TextBlock
                    {
                        Text = "• ",
                        Foreground = new SolidColorBrush(Color.Parse(IsDark ? "#7DD3FC" : "#4A9EFF")),
                        FontWeight = FontWeight.Bold,
                        VerticalAlignment = VerticalAlignment.Top
                    });

                    panel.Children.Add(CreateInlineTextBlock(text, false));

                    stackPanel.Children.Add(panel);
                }
                // Listes numérotées
                else if (Regex.IsMatch(line.TrimStart(), @"^\d+\.\s"))
                {
                    var indent = line.Length - line.TrimStart().Length;
                    var match = Regex.Match(line.TrimStart(), @"^(\d+)\.\s(.*)$");
                    if (match.Success)
                    {
                        var panel = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Margin = new Avalonia.Thickness(indent * 10 + 10, 2, 0, 2)
                        };

                        panel.Children.Add(new TextBlock
                        {
                            Text = match.Groups[1].Value + ". ",
                            Foreground = new SolidColorBrush(Color.Parse(IsDark ? "#7DD3FC" : "#4A9EFF")),
                            FontWeight = FontWeight.SemiBold,
                            VerticalAlignment = VerticalAlignment.Top
                        });

                        panel.Children.Add(CreateInlineTextBlock(match.Groups[2].Value, false));

                        stackPanel.Children.Add(panel);
                    }
                }
                // Code block (```)
                else if (line.Trim().StartsWith("```"))
                {
                    var codeLines = new System.Collections.Generic.List<string>();
                    var language = line.Trim().Substring(3).Trim(); // Récupérer le langage optionnel
                    i++; // Skip opening ```
                    while (i < lines.Length && !lines[i].Trim().StartsWith("```"))
                    {
                        codeLines.Add(lines[i]);
                        i++;
                    }

                    var codePanel = new StackPanel();

                    // Si un langage est spécifié, l'afficher
                    if (!string.IsNullOrWhiteSpace(language))
                    {
                        codePanel.Children.Add(new TextBlock
                        {
                            Text = language.ToUpper(),
                            FontSize = 10,
                            FontWeight = FontWeight.SemiBold,
                            Foreground = new SolidColorBrush(Color.Parse(IsDark ? "#A0A0A0" : "#888888")),
                            Margin = new Avalonia.Thickness(0, 0, 0, 4)
                        });
                    }

                    codePanel.Children.Add(new Border
                    {
                        Background = new SolidColorBrush(Color.Parse(IsDark ? "#1A1A1A" : "#F8F8F8")),
                        BorderBrush = new SolidColorBrush(Color.Parse(IsDark ? "#3F3F46" : "#CCCCCC")),
                        BorderThickness = new Avalonia.Thickness(1),
                        CornerRadius = new Avalonia.CornerRadius(4),
                        Padding = new Avalonia.Thickness(12),
                        Child = new ScrollViewer
                        {
                            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                            Content = new TextBlock
                            {
                                Text = string.Join(Environment.NewLine, codeLines),
                                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                                FontSize = 13,
                                TextWrapping = TextWrapping.NoWrap,
                                Foreground = new SolidColorBrush(Color.Parse(IsDark ? "#D4D4D4" : "#1E1E1E"))
                            }
                        }
                    });

                    stackPanel.Children.Add(new Border
                    {
                        Margin = new Avalonia.Thickness(0, 4, 0, 4),
                        Child = codePanel
                    });
                }
                // Ligne vide
                else if (string.IsNullOrWhiteSpace(line))
                {
                    stackPanel.Children.Add(new TextBlock
                    {
                        Text = " ",
                        FontSize = 6
                    });
                }
                // Tableau Markdown (ligne commençant par |)
                else if (line.TrimStart().StartsWith("|"))
                {
                    // Collecter toutes les lignes du tableau
                    var tableLines = new System.Collections.Generic.List<string>();
                    while (i < lines.Length && lines[i].TrimStart().StartsWith("|"))
                    {
                        tableLines.Add(lines[i]);
                        i++;
                    }
                    i--; // on reculera d'un cran car la boucle fait i++

                    stackPanel.Children.Add(BuildTable(tableLines));
                }
                // Texte normal
                else
                {
                    stackPanel.Children.Add(CreateInlineTextBlock(line, false));
                }
            }

            return stackPanel;
        }

        /// <summary>
        /// Construit un Grid Avalonia à partir des lignes d'un tableau Markdown.
        /// </summary>
        private Border BuildTable(System.Collections.Generic.List<string> tableLines)
        {
            // Parser les cellules d'une ligne
            static string[] ParseRow(string line)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("|")) trimmed = trimmed.Substring(1);
                if (trimmed.EndsWith("|")) trimmed = trimmed.Substring(0, trimmed.Length - 1);
                var cells = trimmed.Split('|');
                for (int k = 0; k < cells.Length; k++) cells[k] = cells[k].Trim();
                return cells;
            }

            // Filtrer la ligne de séparation (|---|---|)
            var dataLines = new System.Collections.Generic.List<string[]>();
            bool isFirstDataRow = true;
            string[]? headers = null;

            foreach (var tl in tableLines)
            {
                if (Regex.IsMatch(tl, @"^\s*\|?\s*[-:]+[-| :]*\s*$"))
                    continue; // ligne de séparation
                var cells = ParseRow(tl);
                if (isFirstDataRow) { headers = cells; isFirstDataRow = false; }
                else dataLines.Add(cells);
            }

            if (headers == null) headers = Array.Empty<string>();
            int colCount = headers.Length;

            var grid = new Grid { Margin = new Avalonia.Thickness(0, 4, 0, 4) };
            for (int c = 0; c < colCount; c++)
                grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            int row = 0;
            // En-tête
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (int c = 0; c < colCount; c++)
            {
                var cell = new Border
                {
                    Background = new SolidColorBrush(Color.Parse(IsDark ? "#1A3A52" : "#D0E4F7")),
                    BorderBrush = new SolidColorBrush(Color.Parse(IsDark ? "#3F3F46" : "#AAAAAA")),
                    BorderThickness = new Avalonia.Thickness(1),
                    Padding = new Avalonia.Thickness(8, 4, 8, 4),
                    Child = new TextBlock
                    {
                        Text = c < headers.Length ? headers[c] : "",
                        FontWeight = FontWeight.Bold,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(Color.Parse(IsDark ? "#E8E8E8" : "#1A1A1A"))
                    }
                };
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
            row++;

            // Lignes de données
            foreach (var dataRow in dataLines)
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                bool isEven = (row % 2 == 0);
                for (int c = 0; c < colCount; c++)
                {
                    var cell = new Border
                    {
                        Background = new SolidColorBrush(IsDark
                            ? Color.Parse(isEven ? "#252526" : "#2D2D30")
                            : Color.Parse(isEven ? "#F5F9FF" : "#FFFFFF")),
                        BorderBrush = new SolidColorBrush(Color.Parse(IsDark ? "#3F3F46" : "#CCCCCC")),
                        BorderThickness = new Avalonia.Thickness(1),
                        Padding = new Avalonia.Thickness(8, 4, 8, 4),
                        Child = CreateInlineTextBlock(c < dataRow.Length ? dataRow[c] : "", false)
                    };
                    Grid.SetRow(cell, row);
                    Grid.SetColumn(cell, c);
                    grid.Children.Add(cell);
                }
                row++;
            }

            return new Border
            {
                BorderBrush = new SolidColorBrush(Color.Parse(IsDark ? "#3F3F46" : "#AAAAAA")),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(4),
                ClipToBounds = true,
                Margin = new Avalonia.Thickness(0, 6, 0, 6),
                Child = grid
            };
        }

        /// <summary>
        /// Crée un TextBlock avec support du formatage inline (gras, italique, code)
        /// </summary>
        private TextBlock CreateInlineTextBlock(string text, bool isQuote)
        {
            var textBlock = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(0, 2, 0, 2),
                Foreground = new SolidColorBrush(Color.Parse(IsDark ? "#E0E0E0" : "#1A1A1A"))
            };

            if (isQuote)
            {
                textBlock.FontStyle = FontStyle.Italic;
                textBlock.Foreground = new SolidColorBrush(Color.Parse(IsDark ? "#A8C7E8" : "#555555"));
            }

            // Utiliser Inlines pour supporter le formatage riche
            var inlines = new Avalonia.Controls.Documents.InlineCollection();

            // Parser le texte avec regex pour supporter gras, italique, code inline
            var pattern = @"(\*\*\*(.+?)\*\*\*)|(\*\*(.+?)\*\*)|(\*(.+?)\*)|(__(.+?)__)|(_(.+?)_)|(`(.+?)`)|\[([^\]]+)\]\(([^\)]+)\)";
            int lastIndex = 0;

            foreach (Match match in Regex.Matches(text, pattern))
            {
                // Ajouter le texte avant le match
                if (match.Index > lastIndex)
                {
                    var normalText = text.Substring(lastIndex, match.Index - lastIndex);
                    inlines.Add(new Avalonia.Controls.Documents.Run(normalText)
                    {
                        Foreground = new SolidColorBrush(Color.Parse(IsDark ? "#E0E0E0" : "#1A1A1A"))
                    });
                }

                // ***gras et italique***
                if (match.Groups[1].Success)
                {
                    inlines.Add(new Avalonia.Controls.Documents.Run(match.Groups[2].Value)
                    {
                        FontWeight = FontWeight.Bold,
                        FontStyle = FontStyle.Italic,
                        Foreground = new SolidColorBrush(Color.Parse(IsDark ? "#F0F0F0" : "#1A1A1A"))
                    });
                }
                // **gras**
                else if (match.Groups[3].Success)
                {
                    inlines.Add(new Avalonia.Controls.Documents.Run(match.Groups[4].Value)
                    {
                        FontWeight = FontWeight.Bold,
                        Foreground = new SolidColorBrush(Color.Parse(IsDark ? "#F0F0F0" : "#1A1A1A"))
                    });
                }
                // *italique*
                else if (match.Groups[5].Success)
                {
                    inlines.Add(new Avalonia.Controls.Documents.Run(match.Groups[6].Value)
                    {
                        FontStyle = FontStyle.Italic,
                        Foreground = new SolidColorBrush(Color.Parse(IsDark ? "#E0E0E0" : "#1A1A1A"))
                    });
                }
                // __gras__
                else if (match.Groups[7].Success)
                {
                    inlines.Add(new Avalonia.Controls.Documents.Run(match.Groups[8].Value)
                    {
                        FontWeight = FontWeight.Bold,
                        Foreground = new SolidColorBrush(Color.Parse(IsDark ? "#F0F0F0" : "#1A1A1A"))
                    });
                }
                // _italique_
                else if (match.Groups[9].Success)
                {
                    inlines.Add(new Avalonia.Controls.Documents.Run(match.Groups[10].Value)
                    {
                        FontStyle = FontStyle.Italic,
                        Foreground = new SolidColorBrush(Color.Parse(IsDark ? "#E0E0E0" : "#1A1A1A"))
                    });
                }
                // `code inline`
                else if (match.Groups[11].Success)
                {
                    var codeRun = new Avalonia.Controls.Documents.Run(match.Groups[12].Value)
                    {
                        FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                        FontSize = 13,
                        Background = new SolidColorBrush(Color.Parse(IsDark ? "#2D2D30" : "#F5F5F5")),
                        Foreground = new SolidColorBrush(Color.Parse(IsDark ? "#FF7EB3" : "#E01E5A"))
                    };
                    inlines.Add(codeRun);
                }
                // [lien](url)
                else if (match.Groups[13].Success)
                {
                    inlines.Add(new Avalonia.Controls.Documents.Run(match.Groups[13].Value)
                    {
                        Foreground = new SolidColorBrush(Color.Parse(IsDark ? "#5BB8FF" : "#0066CC")),
                        TextDecorations = Avalonia.Media.TextDecorations.Underline
                    });
                }

                lastIndex = match.Index + match.Length;
            }

            // Ajouter le texte restant
            if (lastIndex < text.Length)
            {
                inlines.Add(new Avalonia.Controls.Documents.Run(text.Substring(lastIndex)));
            }

            // Si aucun formatage, utiliser le texte brut
            if (inlines.Count == 0)
            {
                textBlock.Text = text;
            }
            else
            {
                textBlock.Inlines = inlines;
            }

            return textBlock;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
