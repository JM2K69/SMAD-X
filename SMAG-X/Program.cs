using Avalonia;
using SMADX.Services;
using SMAGX.Fidelity;
using SMAGX.Machine;

namespace SMAGX;

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
            // Sous Linux, pas de menu global : les menus sont dans la fenêtre (voir SMAD-X, issue #8)
            .With(new X11PlatformOptions { UseDBusMenu = false })
            .WithInterFont()
            .WithEmojiFont()
            .LogToTrace();

    /// <summary>
    /// Vérification de l'exécutable publié, lancée par la CI de release (<c>SMAG-X --self-test</c>), sans fenêtre : un
    /// premier commit donne l'identifiant que donne le vrai Git, et les textes existent dans les deux langues.
    /// </summary>
    /// <returns>0 si tout est conforme, 1 sinon.</returns>
    internal static int SelfTest(TextWriter output)
    {
        try
        {
            var steps = Transcript.Parse("""
                $ git config --global user.name "Alice Martin"
                $ git config --global user.email alice@lycee.example
                $ git config --global init.defaultBranch main
                $ git init
                write index.html <h1>Agence Méridien</h1>\n
                write style.css body { margin: 0; }\n
                $ git add index.html
                $ git commit -m "Page d'accueil"
                """);
            var results = EngineReplay.Run(steps);
            // Le même commit que le vrai Git 2.55 (SMAG-X.Tests/Fidelity/01-premiers-pas.git.txt)
            var engine = results[^1].Out.StartsWith("[main (root-commit) ");
            var fr = SmagxText.Instance.Translate("Zones.Index", "fr-FR");
            var en = SmagxText.Instance.Translate("Zones.Index", "en-US");
            var texts = !fr.StartsWith('[') && !en.StartsWith('[') && fr != en;
            output.WriteLine($"SMAG-X {typeof(Program).Assembly.GetName().Version}: engine {(engine ? "ok" : "unexpected")}, texts {(texts ? "ok" : "missing")}");
            return engine && texts ? 0 : 1;
        }
        catch (Exception ex)
        {
            output.WriteLine($"SMAG-X self-test failed: {ex}");
            return 1;
        }
    }
}
