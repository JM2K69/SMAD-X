using SMADX.Graph;
using SMADX.Services;

namespace SMADX.Tests;

/// <summary>
/// Les pictogrammes du graphe sont au trait (Segoe UI Symbol) ; le reste de la suite cherche les emoji dans la police
/// emoji du système, qu'une fenêtre ouverte avant ne peut plus remplacer.
/// </summary>
public class GraphSymbolsTests
{
    private static string[] Symbols(string text) =>
        GraphSymbols.Ranges(text).Select(r => text.Substring(r.Start, r.Length)).ToArray();

    [Theory]
    [InlineData("🔄 Recharger", new[] { "🔄" })]
    [InlineData("🔍+", new[] { "🔍" })]
    [InlineData("🔍−", new[] { "🔍" })]
    [InlineData("⬇ Héritage GPO", new[] { "⬇" })]
    [InlineData("✕ Fermer", new[] { "✕" })]
    [InlineData("👤 jdoe  |  Type : Utilisateur", new[] { "👤" })]
    public void Pictograms_AreDrawnWithTheSymbolFont(string text, string[] expected)
    {
        Assert.Equal(expected, Symbols(text));
    }

    [Theory]
    [InlineData("⊡ Ajuster")]
    [InlineData("Isolés")]
    [InlineData("Nœuds : 68  |  Arêtes : 156    [Molette : zoom  •  Clic : sélectionner]")]
    [InlineData("")]
    public void LettersPunctuationAndMathSigns_KeepTheTextFont(string text)
    {
        Assert.Empty(Symbols(text));
    }

    [Fact]
    public void VariationSelectorsAndJoiners_StayWithTheirPictogram()
    {
        Assert.Equal(new[] { "⚠️", "👨‍💻" }, Symbols("⚠️ alerte 👨‍💻 poste"));
    }

    [Theory]
    [InlineData(0x2713, true)]   // ✓
    [InlineData(0x2714, true)]   // ✔ réussi, en vert
    [InlineData(0x2715, true)]   // ✕
    [InlineData(0x2717, true)]   // ✗
    [InlineData(0x2718, true)]   // ✘ échoué, en rouge
    [InlineData(0x25D0, true)]   // ◐ en partie
    [InlineData(0x26A0, false)]  // ⚠ : Inter l'a déjà, au trait
    [InlineData(0x2705, false)]  // ✅
    [InlineData(0x1F464, false)] // 👤
    public void ResultMarks_TakeTheTextColor(int codepoint, bool mark)
    {
        Assert.Equal(mark, EmojiFont.Marks.IsInRange(codepoint));
    }

    [Fact]
    public void TheEmojiFont_IsTheSystemOne()
    {
        var expected = OperatingSystem.IsWindows() ? "Segoe UI Emoji"
            : OperatingSystem.IsMacOS() ? "Apple Color Emoji"
            : "Noto Color Emoji";
        Assert.Equal(expected, EmojiFont.Family);
    }
}
