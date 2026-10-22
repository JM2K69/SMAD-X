using System.Text.RegularExpressions;
using SMADX.Helpers;
using SMADX.Services;

namespace SMADX.Tests;

public class AppInfoTests
{
    [Fact]
    public void Version_ComesFromTheProjectFile()
    {
        // <Version> de SMAD-X.csproj, sans le hash de commit que le SDK ajoute par défaut
        Assert.Matches(new Regex(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$"), AppInfo.Version);
        Assert.Equal(AppInfo.Version, typeof(AppInfo).Assembly.GetName().Version!.ToString(3));
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("en-US")]
    public void AboutText_ShowsTheAssemblyVersion(string culture)
    {
        var loc = LocalizationService.Instance;
        var previous = loc.CurrentCulture.Name;
        try
        {
            loc.SetLanguage(culture);
            Assert.EndsWith(" " + AppInfo.Version, new LocalizationProxy().AboutVersion);
        }
        finally
        {
            loc.SetLanguage(previous);
        }
    }
}
