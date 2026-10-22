using System.Text.RegularExpressions;
using SMADX.Converters;

namespace SMADX.Tests;

public class MarkdownConverterTests
{
    private static Match[] Matches(string text) => MarkdownConverter.InlinePattern.Matches(text).ToArray();

    [Theory]
    [InlineData("L'option DONT_REQ_PREAUTH est cochée.")]
    [InlineData("(TRUSTED_FOR_DELEGATION)")]
    [InlineData("The PASSWD_NOTREQD flag")]
    [InlineData("svc_sql et svc_web")]
    [InlineData("vuln_kerberos_properties_preauth_priv")]
    [InlineData("mon__nom__composé et a_b_c")]
    public void UnderscoresInsideWords_AreLiteral(string text)
    {
        Assert.DoesNotContain(Matches(text), m => m.Groups[7].Success || m.Groups[9].Success);
    }

    [Theory]
    [InlineData("un mot _en italique_ ici", 9, "en italique")]
    [InlineData("_italique_ en début de ligne", 9, "italique")]
    [InlineData("(_entre parenthèses_)", 9, "entre parenthèses")]
    [InlineData("un mot __en gras__ ici", 7, "en gras")]
    [InlineData("un mot *en italique* ici", 5, "en italique")]
    [InlineData("le flag `DONT_REQ_PREAUTH` en code", 11, "DONT_REQ_PREAUTH")]
    public void Emphasis_StillWorksAtWordBoundaries(string text, int group, string content)
    {
        var match = Assert.Single(Matches(text));
        Assert.True(match.Groups[group].Success);
        Assert.Equal(content, match.Groups[group + 1].Value);
    }

    [Fact]
    public void SpacedUnderscores_AreNotEmphasis()
    {
        Assert.Empty(Matches("a _ b _ c"));
    }
}
