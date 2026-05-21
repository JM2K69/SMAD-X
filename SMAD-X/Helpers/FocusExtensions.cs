using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace SMADX.Helpers
{
    /// <summary>
    /// Attached property helper to focus a control (TextBox) when a bound boolean becomes true.
    /// Usage in XAML: helpers:FocusExtensions.IsFocused="{Binding SelectedNode.IsEditing}"
    /// </summary>
    public static class FocusExtensions
    {
        public static readonly AttachedProperty<bool> IsFocusedProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>("IsFocused", typeof(FocusExtensions));

        public static bool GetIsFocused(AvaloniaObject element) => element.GetValue(IsFocusedProperty);
        public static void SetIsFocused(AvaloniaObject element, bool value) => element.SetValue(IsFocusedProperty, value);

        static FocusExtensions()
        {
            IsFocusedProperty.Changed.AddClassHandler<Control>((control, args) =>
            {
                if (args.NewValue is bool wantFocus && wantFocus)
                {
                    TryFocus(control, retriesLeft: 5);
                }
            });
        }

        private static void TryFocus(Control control, int retriesLeft)
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    if (!control.IsVisible || !control.IsLoaded)
                    {
                        if (retriesLeft > 0)
                            TryFocus(control, retriesLeft - 1);
                        return;
                    }
                    control.Focus();
                    if (control is TextBox tb)
                    {
                        tb.SelectionStart = 0;
                        tb.SelectionEnd = tb.Text?.Length ?? 0;
                    }
                }
                catch
                {
                    // Ignore focus errors; this is best-effort helper
                }
            }, DispatcherPriority.Background);
        }
    }
}
