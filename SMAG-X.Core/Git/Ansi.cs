namespace SMAGX.Git;

/// <summary>
/// Les couleurs que Git écrit dans un terminal (color.ui=auto) : SMAG-X se comporte toujours comme un terminal,
/// ses sorties portent donc les mêmes codes que celles du vrai Git, que la console de l'application colore.
/// </summary>
public static class Ansi
{
    public const string Reset = "\u001b[m";
    public const string Red = "\u001b[31m";
    public const string Green = "\u001b[32m";
    public const string Yellow = "\u001b[33m";
    public const string Cyan = "\u001b[36m";
    public const string Bold = "\u001b[1m";
    public const string BoldRed = "\u001b[1;31m";
    public const string BoldGreen = "\u001b[1;32m";
    public const string BoldYellow = "\u001b[1;33m";
    public const string BoldCyan = "\u001b[1;36m";

    public static string Paint(string color, string text) => color + text + Reset;
}
