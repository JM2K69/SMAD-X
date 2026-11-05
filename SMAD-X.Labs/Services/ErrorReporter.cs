using System;
using System.Diagnostics;
using System.IO;

namespace SMADX.Services
{
    /// <summary>Erreur signalée à l'interface : message court et détail technique complet.</summary>
    public sealed record ReportedError(string Source, Exception Exception, string LogFile)
    {
        public string Message => Exception.GetBaseException().Message;
    }

    /// <summary>
    /// Journal unique de l'application, dans %LOCALAPPDATA%\SMAD-X\logs (un fichier par jour) :
    /// accessible en écriture même quand l'exécutable est installé dans Program Files.
    /// Les erreurs inattendues sont aussi signalées à l'interface par <see cref="ErrorRaised"/>.
    /// </summary>
    public static class ErrorReporter
    {
        private static readonly object Sync = new();

        /// <summary>Dossier du journal (modifiable par les tests).</summary>
        public static string LogDirectory { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SMAD-X", "logs");

        public static string CurrentLogFile => Path.Combine(LogDirectory, $"smadx-{DateTime.Now:yyyyMMdd}.log");

        /// <summary>Levé pour chaque erreur inattendue, pour qu'une fenêtre puisse l'afficher.</summary>
        public static event Action<ReportedError>? ErrorRaised;

        /// <summary>Écrit une ligne d'information ou d'erreur gérée (export échoué, fichier illisible…).</summary>
        public static void Log(string source, string message) => Write($"[{source}] {message}");

        /// <summary>Journalise une erreur gérée par l'appelant, sans l'afficher.</summary>
        public static void Log(string source, Exception exception) => Write($"[{source}] {exception}");

        /// <summary>Journalise une erreur inattendue et la signale à l'interface.</summary>
        public static void Report(string source, Exception exception)
        {
            Write($"[{source}] ERREUR NON GÉRÉE {exception}");
            ErrorRaised?.Invoke(new ReportedError(source, exception, CurrentLogFile));
        }

        private static void Write(string line)
        {
            Debug.WriteLine($"[SMAD-X] {line}");
            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(LogDirectory);
                    File.AppendAllText(CurrentLogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {line}{Environment.NewLine}");
                }
            }
            catch (Exception)
            {
                // Le journal ne doit jamais faire planter l'application
            }
        }
    }
}
