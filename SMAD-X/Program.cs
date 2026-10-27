using Avalonia;
using System;
using System.IO;
using System.Linq;
using SMADX.Services;
using SMADX.Services.Labs;

namespace SMADX
{
    internal sealed class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static int Main(string[] args) => args is ["--self-test"]
            ? SelfTest(Console.Out)
            : BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
#if DEBUG
                .WithDeveloperTools()
#endif
                .WithInterFont()
                .LogToTrace();

        /// <summary>
        /// Vérification de l'exécutable publié, lancée par la CI de release (<c>SMAD-X --self-test</c>) : le
        /// contenu embarqué (exercices, fiches, polices du PDF) se lit dans l'exe autonome, sans fenêtre.
        /// </summary>
        /// <returns>0 si tout est lisible, 1 sinon.</returns>
        internal static int SelfTest(TextWriter output)
        {
            try
            {
                var labs = new LabService().BuiltInLabs();
                var loc = LocalizationService.Instance;
                var sheets = new[] { "fr-FR", "en-US" }.All(c => !loc.Translate("Desc.Users.Krbtgt", c).StartsWith('['));
                var fonts = typeof(LabService).Assembly.GetManifestResourceNames().Count(r => r.StartsWith("Fonts/", StringComparison.Ordinal));
                output.WriteLine($"SMAD-X {EngineInfo.Version}: {labs.Count} labs, sheets {(sheets ? "ok" : "missing")}, {fonts} fonts");
                return labs.Count > 0 && sheets && fonts > 0 ? 0 : 1;
            }
            catch (Exception ex)
            {
                output.WriteLine($"SMAD-X self-test failed: {ex}");
                return 1;
            }
        }
    }
}
