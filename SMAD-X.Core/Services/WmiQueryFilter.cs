using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SMADX.Models;

namespace SMADX.Services
{
    /// <summary>
    /// Ordinateurs que vise un filtre WMI, d'après ses requêtes WQL et le type de système qu'elles testent
    /// (Win32_OperatingSystem.ProductType : 1 poste de travail, 2 contrôleur de domaine, 3 serveur). SMAD-X ne connaît
    /// pas les autres conditions (version, architecture…) : elles peuvent être vraies ou fausses, et le filtre vise un type
    /// de système dès que ses requêtes peuvent être vraies pour lui. Quand un filtre a plusieurs requêtes, toutes doivent
    /// être vraies.
    /// </summary>
    public static class WmiQueryFilter
    {
        /// <summary>Serveurs, postes, ou null : tous les ordinateurs, ou ce que les requêtes ne permettent pas de dire.</summary>
        /// <param name="queries">Requêtes du filtre : msWMI-Parm2 tel qu'AD le garde, ou les requêtes d'un fichier MOF.</param>
        public static GpoComputerFilter? Classify(string? queries)
        {
            if (string.IsNullOrWhiteSpace(queries)) return null;
            var statements = Regex.Matches(queries, @"\bSELECT\b[^;]*", RegexOptions.IgnoreCase).Select(m => m.Value).ToList();
            if (statements.Count == 0) return null;
            var types = new[] { 1, 2, 3 }.Where(t => statements.All(s => Possible(s, t))).ToHashSet();
            if (types.Count == 1 && types.Contains(1)) return GpoComputerFilter.Workstations;
            if (types.Count > 0 && !types.Contains(1)) return GpoComputerFilter.Servers;
            return null;
        }

        /// <summary>La requête peut-elle être vraie pour ce type de système ?</summary>
        private static bool Possible(string statement, int productType)
        {
            var where = Regex.Match(statement, @"\bWHERE\b(.*)$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!where.Success) return true;
            var tokens = Regex.Matches(where.Groups[1].Value, @"""[^""]*""|'[^']*'|\(|\)|[^\s()""']+").Select(m => m.Value).ToList();
            var position = 0;
            return new Parser(tokens, productType).Or(ref position) != false;
        }

        /// <summary>Logique à trois valeurs : vrai, faux, ou null pour une condition que SMAD-X ne connaît pas.</summary>
        private sealed class Parser(IReadOnlyList<string> tokens, int productType)
        {
            private bool Is(int position, string keyword) =>
                position < tokens.Count && string.Equals(tokens[position], keyword, StringComparison.OrdinalIgnoreCase);

            public bool? Or(ref int position)
            {
                var value = And(ref position);
                while (Is(position, "OR"))
                {
                    position++;
                    var right = And(ref position);
                    value = value == true || right == true ? true : value == false && right == false ? false : null;
                }
                return value;
            }

            private bool? And(ref int position)
            {
                var value = Not(ref position);
                while (Is(position, "AND"))
                {
                    position++;
                    var right = Not(ref position);
                    value = value == false || right == false ? false : value == true && right == true ? true : null;
                }
                return value;
            }

            private bool? Not(ref int position)
            {
                if (!Is(position, "NOT")) return Primary(ref position);
                position++;
                return Not(ref position) is { } value ? !value : null;
            }

            private bool? Primary(ref int position)
            {
                if (Is(position, "("))
                {
                    position++;
                    var value = Or(ref position);
                    if (Is(position, ")")) position++;
                    return value;
                }
                // Une condition : les mots jusqu'au prochain opérateur ou parenthèse
                var start = position;
                while (position < tokens.Count && !Is(position, "AND") && !Is(position, "OR") && !Is(position, "(") && !Is(position, ")"))
                    position++;
                var condition = string.Join(" ", tokens.Skip(start).Take(position - start));
                var test = Regex.Match(condition, @"^ProductType\s*(=|<>|!=)\s*[""']?(\d+)[""']?$", RegexOptions.IgnoreCase);
                if (!test.Success) return null;
                var equals = int.Parse(test.Groups[2].Value) == productType;
                return test.Groups[1].Value == "=" ? equals : !equals;
            }
        }
    }
}
