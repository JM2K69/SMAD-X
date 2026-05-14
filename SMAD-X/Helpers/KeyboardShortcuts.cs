using Avalonia.Input;
using System.Collections.Generic;

namespace SMADX.Helpers
{
    /// <summary>
    /// Gestionnaire de raccourcis clavier pour l'application
    /// </summary>
    public static class KeyboardShortcuts
    {
        public static Dictionary<string, KeyGesture> Shortcuts = new()
        {
            { "Copy", new KeyGesture(Key.C, KeyModifiers.Control) },
            { "Paste", new KeyGesture(Key.V, KeyModifiers.Control) },
            { "Delete", new KeyGesture(Key.Delete) },
            { "Rename", new KeyGesture(Key.F2) },
            { "Save", new KeyGesture(Key.S, KeyModifiers.Control) },
            { "Open", new KeyGesture(Key.O, KeyModifiers.Control) },
            { "New", new KeyGesture(Key.N, KeyModifiers.Control) }
        };
    }
}
