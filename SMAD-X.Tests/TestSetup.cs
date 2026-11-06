using System.Runtime.CompilerServices;

namespace SMADX.Tests;

/// <summary>Réglages communs à tous les tests, posés avant le premier.</summary>
internal static class TestSetup
{
    /// <summary>
    /// Les tests de Studio n'écrivent jamais dans les réglages du formateur (exercices récents, dernier dossier) : ils ont
    /// les leurs, dans un dossier temporaire propre à cette exécution.
    /// </summary>
    [ModuleInitializer]
    internal static void IsolateStudioSettings() =>
        SMADX.Studio.StudioSettings.DefaultPath = Path.Combine(Path.GetTempPath(), "smadx-tests-" + Environment.ProcessId, "settings.json");
}
