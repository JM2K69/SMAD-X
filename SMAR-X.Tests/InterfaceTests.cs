namespace SMARX.Tests;

/// <summary>
/// Tests de l'interface : ils partagent la langue de l'application (SmarxText, unique) et ne tournent donc pas en
/// parallèle les uns des autres.
/// </summary>
[CollectionDefinition(Name)]
public sealed class InterfaceTests
{
    public const string Name = "Interface";
}
