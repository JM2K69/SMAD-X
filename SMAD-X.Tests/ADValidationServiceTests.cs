using SMADX.Models;
using SMADX.Services;

namespace SMADX.Tests;

public class ADValidationServiceTests
{
    private readonly ADValidationService _service = new();

    [Theory]
    [InlineData("jdoe")]
    [InlineData("Domain Admins")]
    [InlineData("Équipe-RH_2026")]
    public void ValidateName_AcceptsValidNames(string name)
    {
        Assert.True(_service.ValidateName(name, out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a/b")]
    [InlineData("a,b")]
    [InlineData("a=b")]
    [InlineData("a\"b")]
    [InlineData("a*b")]
    public void ValidateName_RejectsInvalidNames(string name)
    {
        Assert.False(_service.ValidateName(name, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateName_RejectsMoreThan64Characters()
    {
        Assert.True(_service.ValidateName(new string('a', 64), out _));
        Assert.False(_service.ValidateName(new string('a', 65), out _));
    }

    [Theory]
    [InlineData(ADObjectType.Domain, ADObjectType.OrganizationalUnit, true)]
    [InlineData(ADObjectType.Domain, ADObjectType.User, false)]
    [InlineData(ADObjectType.OrganizationalUnit, ADObjectType.OrganizationalUnit, true)]
    [InlineData(ADObjectType.OrganizationalUnit, ADObjectType.User, true)]
    [InlineData(ADObjectType.OrganizationalUnit, ADObjectType.Container, false)]
    [InlineData(ADObjectType.Container, ADObjectType.User, true)]
    [InlineData(ADObjectType.Container, ADObjectType.PasswordSettingsObject, true)]
    [InlineData(ADObjectType.Container, ADObjectType.OrganizationalUnit, false)]
    [InlineData(ADObjectType.User, ADObjectType.User, false)]
    [InlineData(ADObjectType.Group, ADObjectType.User, false)]
    public void ValidateContainerRule_FollowsAdRules(ADObjectType parent, ADObjectType child, bool expected)
    {
        Assert.Equal(expected, _service.ValidateContainerRule(parent, child, out _));
    }
}
