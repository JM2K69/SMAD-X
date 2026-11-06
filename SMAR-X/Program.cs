using System;
using System.IO;
using System.Linq;
using Avalonia;
using SMARX.Samples;
using SMARX.Services.Linux;
using SMARX.Services.Windows;

namespace SMARX
{
    internal sealed class Program
    {
        [STAThread]
        public static int Main(string[] args) => args is ["--self-test"]
            ? SelfTest(Console.Out)
            : BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        // Configuration d'Avalonia, aussi utilisée par le concepteur visuel
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();

        /// <summary>
        /// Vérification de l'exécutable publié, lancée par la CI de release (<c>SMAR-X --self-test</c>), sans fenêtre :
        /// les deux serveurs d'exemple se construisent, les quatre moteurs (noyau, Samba, NTFS, SMB) rendent le verdict
        /// attendu sur le même accès en local puis par un partage, les textes existent dans les deux langues, et
        /// l'aide-mémoire en PDF a ses polices et le bouclier de SMAR-X.
        /// </summary>
        /// <returns>0 si tout est conforme, 1 sinon.</returns>
        internal static int SelfTest(TextWriter output)
        {
            try
            {
                var linux = LinuxSamples.FileServer();
                var carol = linux.Users.First(u => u.Name == "carol");
                var kernel = LinuxAccess.Check(linux, carol, "/srv/partage/doc.txt", LinuxOperation.Write).Allowed;
                var samba = SambaAccess.Check(linux, carol, linux.Shares.First(s => s.Name == "partage"), "/srv/partage/doc.txt", LinuxOperation.Write).Allowed;

                var windows = WindowsSamples.FileServer();
                var dave = windows.Users.First(u => u.Name == "dave");
                const string planning = @"D:\Partages\Projets\planning.xlsx";
                var ntfs = WindowsAccess.Check(windows, dave, planning, WindowsOperation.Write).Allowed;
                var smb = SmbAccess.Check(windows, dave, windows.Shares.First(s => s.Name == "Projets"), planning, WindowsOperation.Write).Allowed;

                // Une clé absente rend « [clé] », et l'anglais manquant retombe sur le français
                var fr = SmarxText.Instance.Translate("Tester.From", "fr-FR");
                var en = SmarxText.Instance.Translate("Tester.From", "en-US");
                var texts = !fr.StartsWith('[') && !en.StartsWith('[') && fr != en;
                // Le même accès : autorisé en local, refusé par le partage
                var engines = kernel && !samba && ntfs && !smb;
                // L'aide-mémoire en PDF : les polices de secours de la suite et le bouclier, embarqués dans l'exe
                var pdf = SMADX.Services.MarkdownPdf.EmbeddedFontCount == 3 && SMARX.Services.Glossary.Logo().Length > 0;
                output.WriteLine($"SMAR-X {typeof(Program).Assembly.GetName().Version}: engines {(engines ? "ok" : "unexpected")}, texts {(texts ? "ok" : "missing")}, pdf {(pdf ? "ok" : "missing")}");
                return engines && texts && pdf ? 0 : 1;
            }
            catch (Exception ex)
            {
                output.WriteLine($"SMAR-X self-test failed: {ex}");
                return 1;
            }
        }
    }
}
