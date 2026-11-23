using System;
using System.Linq;
using SMADX.Services;

namespace SMADX.Helpers
{
    /// <summary>
    /// Exceptions de tâches que personne n'a attendues, relevées plus tard par le ramasse-miettes. Celles de
    /// l'intégration au bureau Linux (appels D-Bus d'Avalonia : menu global, méthode de saisie, portails) ne
    /// viennent pas d'une action de l'utilisateur et n'en font échouer aucune : elles vont au journal, sans
    /// fenêtre « Erreur inattendue » (issue #8, Ubuntu sans registre de menus globaux). Les autres sont
    /// signalées comme avant.
    /// </summary>
    public static class UnobservedTaskErrors
    {
        public static void Handle(AggregateException exception)
        {
            if (IsDesktopIntegration(exception))
                ErrorReporter.Log("D-Bus", exception);
            else
                ErrorReporter.Report("Tâche", exception);
        }

        /// <summary>Toutes les exceptions de la tâche sont des erreurs D-Bus (Tmds.DBus, utilisé par Avalonia sous Linux).</summary>
        public static bool IsDesktopIntegration(AggregateException exception) =>
            exception.Flatten().InnerExceptions is { Count: > 0 } inner
            && inner.All(e => e.GetType().Namespace?.StartsWith("Tmds.DBus", StringComparison.Ordinal) == true);
    }
}
