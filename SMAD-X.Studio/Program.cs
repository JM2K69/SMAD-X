using System;
using System.Runtime.InteropServices;
using Avalonia;

namespace SMADX.Studio
{
    internal static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            if (Cli.IsCommand(args))
            {
                // Exe fenêtré : sous Windows, on se rattache à la console qui l'a lancé pour y écrire
                if (OperatingSystem.IsWindows() && !Console.IsOutputRedirected) AttachConsole(-1);
                return Cli.Run(args, Console.Out, Console.Error);
            }
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();

        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int processId);
    }
}
