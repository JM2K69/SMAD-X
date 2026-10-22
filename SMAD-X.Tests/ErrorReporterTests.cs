using Avalonia.Logging;
using SMADX.Helpers;
using SMADX.Services;

namespace SMADX.Tests;

public sealed class ErrorReporterTests : IDisposable
{
    private readonly string _previousDirectory = ErrorReporter.LogDirectory;
    private readonly List<ReportedError> _raised = new();

    public ErrorReporterTests()
    {
        ErrorReporter.LogDirectory = Path.Combine(Path.GetTempPath(), "smadx-tests", "logs-" + Guid.NewGuid().ToString("N"));
        ErrorReporter.ErrorRaised += _raised.Add;
    }

    public void Dispose()
    {
        ErrorReporter.ErrorRaised -= _raised.Add;
        ErrorReporter.LogDirectory = _previousDirectory;
    }

    private static string LogContent() => File.ReadAllText(ErrorReporter.CurrentLogFile);

    [Fact]
    public void Report_WritesFullExceptionAndRaisesEvent()
    {
        var exception = new InvalidOperationException("La fenêtre n'a pas pu s'ouvrir", new FormatException("cause"));

        ErrorReporter.Report("UI", exception);

        var error = Assert.Single(_raised);
        Assert.Equal("UI", error.Source);
        Assert.Equal("cause", error.Message);           // l'exception d'origine, la plus parlante
        Assert.Equal(ErrorReporter.CurrentLogFile, error.LogFile);
        Assert.Contains("ERREUR NON GÉRÉE", LogContent());
        Assert.Contains("La fenêtre n'a pas pu s'ouvrir", LogContent());
        Assert.Contains("FormatException", LogContent());
    }

    [Fact]
    public void Log_WritesWithoutRaisingEvent()
    {
        ErrorReporter.Log("Export", "PDF created: rapport.pdf");
        ErrorReporter.Log("Sauvegarde", new IOException("disque plein"));

        Assert.Empty(_raised);
        Assert.Contains("[Export] PDF created: rapport.pdf", LogContent());
        Assert.Contains("[Sauvegarde] System.IO.IOException: disque plein", LogContent());
    }

    [Fact]
    public void Log_NeverThrows_WhenDirectoryIsUnusable()
    {
        // Un fichier à la place du dossier : la création du journal échoue, l'application ne doit pas planter
        var blocker = Path.Combine(Path.GetTempPath(), "smadx-tests", "blocker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.GetDirectoryName(blocker)!);
        File.WriteAllText(blocker, "pas un dossier");
        ErrorReporter.LogDirectory = Path.Combine(blocker, "logs");

        ErrorReporter.Report("UI", new InvalidOperationException("x"));

        Assert.Single(_raised); // l'erreur est quand même signalée à l'interface
    }

    [Fact]
    public void AvaloniaSink_ReportsErrorsCarryingAnException()
    {
        var sink = new AvaloniaErrorLogSink();

        sink.Log(LogEventLevel.Error, "Win32", null, "Failed to process {Message}: {Exception}", "WM_KEYDOWN", new InvalidOperationException("boom"));
        sink.Log(LogEventLevel.Warning, "Binding", null, "Could not find property {Name}", "Foo");

        var error = Assert.Single(_raised);
        Assert.Equal("Avalonia/Win32", error.Source);
        Assert.Equal("boom", error.Message);
        Assert.True(sink.IsEnabled(LogEventLevel.Warning, "Binding"));
        Assert.False(sink.IsEnabled(LogEventLevel.Information, "Binding"));
    }

    [Fact]
    public void AvaloniaSink_LogsErrorsWithoutException()
    {
        new AvaloniaErrorLogSink().Log(LogEventLevel.Error, "Layout", null, "Layout cycle detected");

        Assert.Empty(_raised);
        Assert.Contains("[Avalonia/Layout] Layout cycle detected", LogContent());
    }
}
