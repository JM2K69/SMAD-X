using SMADX.Models;
using SMADX.Services;

namespace SMADX.Tests;

/// <summary>Requêtes des filtres WMI de HardenAD (Inputs/GroupPolicies/WmiFilters) et ce qu'elles visent.</summary>
public class WmiQueryFilterTests
{
    [Theory]
    [InlineData("SELECT ProductType FROM Win32_OperatingSystem WHERE NOT ProductType = \"1\"", GpoComputerFilter.Servers)]           // Windows-OS-Servers
    [InlineData("SELECT ProductType FROM Win32_OperatingSystem WHERE ProductType = \"3\"", GpoComputerFilter.Servers)]               // Windows-OS-Servers-NoDC
    [InlineData("Select ProductType From Win32_OperatingSystem Where ProductType = \"1\"", GpoComputerFilter.Workstations)]          // Windows-OS-Clients
    [InlineData("SELECT * FROM Win32_OperatingSystem WHERE (Version like \"10.0%\") AND NOT ProductType = \"1\"", GpoComputerFilter.Servers)]
    [InlineData("SELECT * FROM Win32_OperatingSystem WHERE Version like \"10.0.2%\" AND ProductType = \"1\"", GpoComputerFilter.Workstations)]
    [InlineData("SELECT * FROM Win32_OperatingSystem WHERE (Version like \"5%\" or Version like \"6.0%\") AND ProductType = \"3\"", GpoComputerFilter.Servers)]
    [InlineData("SELECT * FROM Win32_OperatingSystem WHERE Version like \"10.0%\" AND ProductType=\"3\"", GpoComputerFilter.Servers)]
    // Postes et serveurs : le filtre n'en exclut que les contrôleurs de domaine, ou ne teste pas le type
    [InlineData("SELECT ProductType FROM Win32_OperatingSystem WHERE (ProductType = \"1\" or ProductType = \"3\")", null)]          // Windows-NoDC
    [InlineData("SELECT * FROM Win32_operatingsystem WHERE OSArchitecture Like \"%64%\" AND NOT ProductType=\"2\"", null)]           // Windows-x64-NoDC
    [InlineData("SELECT * FROM Win32_OperatingSystem WHERE ((Version like '10.0.2%' or Version like '10.0.19045%') and ProductType = '1') or ((Version like '10.0.2%' or Version like '10.0.17%') and ProductType = '3')", null)]
    [InlineData("SELECT * FROM Win32_OperatingSystem WHERE (Version like \"6%\" or Version like \"10%\")", null)]
    [InlineData("SELECT * FROM Win32_ComputerSystem WHERE DomainRole = 5", null)]
    [InlineData("", null)]
    public void Classify_ReadsTheProductTypeOfTheQuery(string query, GpoComputerFilter? expected) =>
        Assert.Equal(expected, WmiQueryFilter.Classify(query));

    [Fact]
    public void Classify_ReadsTheQueriesAsAdKeepsThem_AllMustBeTrue()
    {
        // msWMI-Parm2 : nombre de requêtes, puis chaque requête précédée de sa langue et de son espace de noms
        Assert.Equal(GpoComputerFilter.Servers, WmiQueryFilter.Classify("1;3;10;64;WQL;root\\CIMv2;SELECT ProductType FROM Win32_OperatingSystem WHERE ProductType = \"3\";"));
        Assert.Equal(GpoComputerFilter.Servers, WmiQueryFilter.Classify(
            "2;3;10;60;WQL;root\\CIMv2;SELECT * FROM Win32_OperatingSystem WHERE Version like \"10%\";3;10;70;WQL;root\\CIMv2;SELECT * FROM Win32_OperatingSystem WHERE NOT ProductType = \"1\";"));
    }
}
