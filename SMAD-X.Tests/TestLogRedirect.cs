using System.Runtime.CompilerServices;
using SMADX.Services;

namespace SMADX.Tests;

internal static class TestLogRedirect
{
    /// <summary>
    /// Avant tout test : le journal de l'application va dans un dossier temporaire, pas dans
    /// %LOCALAPPDATA%\SMAD-X\logs de la personne qui lance les tests.
    /// </summary>
    [ModuleInitializer]
    internal static void RedirectApplicationLog() =>
        ErrorReporter.LogDirectory = Path.Combine(Path.GetTempPath(), "smadx-tests", "logs");
}
