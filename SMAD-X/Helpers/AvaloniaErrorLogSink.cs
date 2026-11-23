using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia.Logging;
using SMADX.Services;

namespace SMADX.Helpers
{
    /// <summary>
    /// Collecteur des journaux Avalonia, qui partaient auparavant dans Trace (invisible hors débogueur).
    /// Les erreurs qui portent une exception sont signalées à l'utilisateur, les autres erreurs vont
    /// dans le journal de l'application, et les avertissements (liaisons XAML…) dans la sortie de débogage.
    /// </summary>
    public sealed class AvaloniaErrorLogSink : ILogSink
    {
        /// <summary>
        /// Zones où Avalonia répète une erreur sans conséquence pour l'utilisateur : sous Ubuntu, la méthode de saisie
        /// IBus (IME) refuse de détruire son contexte à chaque champ quitté, des centaines de fois par jour (issue #8).
        /// Seule la première de la session va au journal, avec la mention que les suivantes n'y vont pas.
        /// </summary>
        private static readonly HashSet<string> OncePerSessionAreas = new(StringComparer.Ordinal) { "IME" };

        private readonly HashSet<string> _loggedAreas = new(StringComparer.Ordinal);

        public bool IsEnabled(LogEventLevel level, string area) => level >= LogEventLevel.Warning;

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate) =>
            Log(level, area, source, messageTemplate, Array.Empty<object?>());

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
        {
            var exception = propertyValues.OfType<Exception>().FirstOrDefault();
            if (level >= LogEventLevel.Error && exception != null)
            {
                ErrorReporter.Report($"Avalonia/{area}", exception);
                return;
            }

            var values = string.Join(", ", propertyValues.Select(v => v?.ToString()));
            Debug.WriteLine($"[Avalonia {level} {area}] {messageTemplate} {values}");
            if (level < LogEventLevel.Error) return;
            if (OncePerSessionAreas.Contains(area))
            {
                lock (_loggedAreas)
                    if (!_loggedAreas.Add(area)) return;
                ErrorReporter.Log($"Avalonia/{area}", $"{messageTemplate} {values}");
                ErrorReporter.Log($"Avalonia/{area}", "Erreur sans conséquence pour l'utilisateur : les suivantes de cette session ne sont pas journalisées.");
                return;
            }
            ErrorReporter.Log($"Avalonia/{area}", $"{messageTemplate} {values}");
        }
    }
}
