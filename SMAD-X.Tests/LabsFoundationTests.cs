using SMADX.Services;

namespace SMADX.Tests;

/// <summary>
/// Socle commun SMAD-X.Labs : partagé avec les autres simulateurs, il ne dépend ni d'Active Directory ni de
/// l'interface, et SMAD-X garde ses textes.
/// </summary>
public class LabsFoundationTests
{
    [Fact]
    public void Foundation_DependsNeitherOnActiveDirectoryNorOnTheUserInterface()
    {
        var references = typeof(Localizer).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        Assert.DoesNotContain(references, r => r.StartsWith("SMAD-X", StringComparison.Ordinal));
        Assert.DoesNotContain(references, r => r.StartsWith("Avalonia", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("fr-FR", "Bonne réponse.")]
    [InlineData("en-US", "Right answer.")]
    public void SmadX_KeepsTheFoundationTextsWithItsOwn(string culture, string rightAnswer)
    {
        var loc = LocalizationService.Instance;

        Assert.Equal(rightAnswer, loc.Translate("Lab.Feedback.RightAnswer", culture));
        Assert.NotEqual("[Menu.File]", loc.Translate("Menu.File", culture));
        Assert.Same(loc, Localizer.Current);
    }
}
