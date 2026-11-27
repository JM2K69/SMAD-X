using CommunityToolkit.Mvvm.ComponentModel;
using SMADX.Models;
using SMAGX.Labs;

namespace SMAGX.ViewModels;

/// <summary>Un scénario du parcours : son numéro, son titre, son niveau, seul ou à deux.</summary>
public sealed class ScenarioCard(SmagxLab lab)
{
    private static SmagxText Text => SmagxText.Instance;

    public SmagxLab Lab { get; } = lab;
    public int Step => Lab.Step;
    public string Title => Lab.Title.Resolve(Text.Culture);

    public string Details => (Lab.Level switch
    {
        LabLevel.Beginner => Text.T("Débutant", "Beginner"),
        LabLevel.Intermediate => Text.T("Intermédiaire", "Intermediate"),
        _ => Text.T("Avancé", "Advanced")
    }) + " · " + (Lab.IsPair ? Text.T("à deux", "by two") : Text.T("seul", "alone")) + $" · {Lab.DurationMinutes} min";
}

/// <summary>
/// La fenêtre « Scénarios » : le parcours à difficulté croissante, l'énoncé du scénario choisi et, pour un scénario à
/// deux, où le jouer : sur ce SMAG-X (deux postes) ou sur deux SMAG-X (servir la forge, ou la rejoindre).
/// </summary>
public sealed partial class ScenarioPickerViewModel : ObservableObject
{
    private static SmagxText Text => SmagxText.Instance;

    public ScenarioPickerViewModel(IReadOnlyList<SmagxLab> labs, bool joined)
    {
        Joined = joined;
        Solo = labs.Where(l => !l.IsPair).OrderBy(l => l.Step).Select(l => new ScenarioCard(l)).ToList();
        Pair = labs.Where(l => l.IsPair).OrderBy(l => l.Step).Select(l => new ScenarioCard(l)).ToList();
        _selected = Solo.FirstOrDefault() ?? Pair.FirstOrDefault();
    }

    public IReadOnlyList<ScenarioCard> Solo { get; }
    public IReadOnlyList<ScenarioCard> Pair { get; }

    /// <summary>Ce SMAG-X a rejoint la forge d'un camarade : il peut jouer le rôle qui la rejoint.</summary>
    public bool Joined { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Statement), nameof(IsPair), nameof(IsSolo), nameof(ServeLabel), nameof(JoinLabel), nameof(HereLabel), nameof(JoinNote))]
    private ScenarioCard? _selected;

    public string Statement => Selected?.Lab.Statement.Resolve(Text.Culture) ?? "";
    public bool IsPair => Selected?.Lab.IsPair == true;
    public bool IsSolo => Selected is { Lab.IsPair: false };

    private SmagxRole? Server => Selected?.Lab.Roles.FirstOrDefault(r => r.Serves);
    private SmagxRole? Joiner => Selected?.Lab.Roles.FirstOrDefault(r => !r.Serves);

    public string HereLabel => Text.T($"Les deux postes sur ce SMAG-X ({Server?.Login} et {Joiner?.Login})", $"Both workstations on this SMAG-X ({Server?.Login} and {Joiner?.Login})");
    public string ServeLabel => Text.T($"Je joue {Server?.Name} et je sers la forge", $"I play {Server?.Name} and serve the forge");
    public string JoinLabel => Text.T($"Je joue {Joiner?.Name} et je rejoins la forge", $"I play {Joiner?.Name} and join the forge");

    public string JoinNote => Joined
        ? Text.T("Vous avez rejoint la forge de votre camarade : il joue l'autre rôle.", "You joined your classmate's forge: they play the other role.")
        : Text.T($"Pour jouer {Joiner?.Name}, rejoignez d'abord la forge de votre camarade (Travail à deux…), une fois qu'il a lancé le scénario.",
            $"To play {Joiner?.Name}, first join your classmate's forge (Pair work…), once they have started the scenario.");
}
