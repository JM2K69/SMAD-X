using System;
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
            if (level >= LogEventLevel.Error)
                ErrorReporter.Log($"Avalonia/{area}", $"{messageTemplate} {values}");
        }
    }
}
