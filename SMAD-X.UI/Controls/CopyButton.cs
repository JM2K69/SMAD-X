using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Threading;

namespace SMADX.Controls
{
    /// <summary>
    /// Un petit bouton qui copie une commande, ou tout un bloc de code, dans le presse-papiers : l'icône devient une coche
    /// un instant pour dire que c'est fait. Commun à SMAD-X, SMAR-X et Studio ; son style (Button.copy) est dans Suite.axaml.
    /// </summary>
    public class CopyButton : Button
    {
        public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<CopyButton, string?>(nameof(Text));

        /// <summary>La langue des infobulles : chaque application donne la sienne au démarrage.</summary>
        public static Func<bool> French { get; set; } = () => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr";

        // Deux feuilles l'une sur l'autre ; une coche une fois copié
        private static readonly Geometry CopyIcon = Geometry.Parse(
            "F0 M9,2 L20,2 C21.1,2 22,2.9 22,4 L22,15 C22,16.1 21.1,17 20,17 L9,17 C7.9,17 7,16.1 7,15 L7,4 C7,2.9 7.9,2 9,2 Z M9,4 L9,15 L20,15 L20,4 Z " +
            "M2,9 C2,7.9 2.9,7 4,7 L5,7 L5,9 L4,9 L4,20 L15,20 L15,19 L17,19 L17,20 C17,21.1 16.1,22 15,22 L4,22 C2.9,22 2,21.1 2,20 Z");
        private static readonly Geometry DoneIcon = Geometry.Parse("M9,16.2 L4.8,12 L3.4,13.4 L9,19 L21,7 L19.6,5.6 Z");

        private readonly PathIcon _icon = new() { Width = 13, Height = 13, Data = CopyIcon };

        public CopyButton()
        {
            Content = _icon;
            Classes.Add("copy");
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            Cursor = new Cursor(StandardCursorType.Hand);
            PointerEntered += (_, _) => ToolTip.SetTip(this, French() ? "Copier dans le presse-papiers" : "Copy to clipboard");
        }

        protected override Type StyleKeyOverride => typeof(Button);

        /// <summary>Le texte copié : la commande, ou le bloc entier.</summary>
        public string? Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        protected override async void OnClick()
        {
            base.OnClick();
            if (string.IsNullOrEmpty(Text) || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;
            await clipboard.SetTextAsync(Text);
            _icon.Data = DoneIcon;
            ToolTip.SetTip(this, French() ? "Copié" : "Copied");
            DispatcherTimer.RunOnce(() => _icon.Data = CopyIcon, TimeSpan.FromSeconds(1.5));
        }
    }
}
