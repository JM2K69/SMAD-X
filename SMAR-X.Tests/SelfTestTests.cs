namespace SMARX.Tests;

/// <summary>
/// <c>SMAR-X --self-test</c>, que la CI de release lance sur l'exécutable publié : il doit réussir sur les sources,
/// sinon la release échouerait pour une raison étrangère à la publication.
/// </summary>
public class SelfTestTests
{
    [Fact]
    public void SelfTest_Succeeds_OnTheSampleServers()
    {
        var output = new StringWriter();

        var code = Program.SelfTest(output);

        Assert.True(code == 0, output.ToString());
        Assert.Contains("engines ok, texts ok", output.ToString());
    }
}
