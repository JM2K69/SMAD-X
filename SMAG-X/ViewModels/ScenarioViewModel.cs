using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;
using SMADX.ViewModels;
using SMAGX.Labs;

namespace SMAGX.ViewModels;

/// <summary>Où se joue un scénario : tous les rôles sur ce SMAG-X, ou un rôle par SMAG-X (celui qui sert la forge, celui qui la rejoint).</summary>
public enum ScenarioMode { Here, Serve, Join }

/// <summary>Un objectif du scénario dans le panneau : texte, rôle, indice, statut après vérification.</summary>
public sealed partial class ScenarioObjectiveItem : ObservableObject
{
    public ScenarioObjectiveItem(SmagxObjective objective, string culture, bool canShowHint, bool showRole)
    {
        Objective = objective;
        Title = objective.Title.Resolve(culture);
        Hint = objective.Hint.Resolve(culture);
        CanShowHint = canShowHint && !string.IsNullOrWhiteSpace(Hint);
        Role = showRole ? objective.Check.Role ?? SmagxText.Instance.T("forge", "forge") : "";
    }

    public SmagxObjective Objective { get; }
    public string Title { get; }
    public string Hint { get; }
    public int Points => Objective.Points;
    public bool IsGuard => Objective.Guard;
    public bool IsScored => !Objective.Guard;
    public bool CanShowHint { get; }

    /// <summary>« alice », « bob », « forge » dans un scénario à deux ; vide sinon.</summary>
    public string Role { get; }

    public bool HasRole => Role.Length > 0;

    [ObservableProperty] private bool _isHintVisible;
    public bool HintSeen { get; set; }
    partial void OnIsHintVisibleChanged(bool value) { if (value) HintSeen = true; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusIcon), nameof(StatusColor))]
    private bool? _passed;

    [ObservableProperty] private string _feedback = string.Empty;

    public string StatusIcon => Status.Icon(Passed, false);
    public string StatusColor => Status.Color(Passed, false);

    [RelayCommand]
    private void ToggleHint() { if (CanShowHint) IsHintVisible = !IsHintVisible; }
}

/// <summary>
/// Le panneau du scénario en cours : énoncé, rôle joué, objectifs vérifiés sur « Vérifier » (comme dans SMAD-X ; en
/// examen, seulement si le formateur l'a permis), indices, score. Les commandes Recommencer et Quitter reviennent à la fenêtre principale.
/// </summary>
public sealed partial class ScenarioViewModel : ObservableObject
{
    private static SmagxText Text => SmagxText.Instance;

    public ScenarioViewModel(SmagxLab lab, ScenarioMode mode)
    {
        Lab = lab;
        Mode = mode;
        Build();
    }

    public SmagxLab Lab { get; }
    public ScenarioMode Mode { get; }
    public DateTime StartedOn { get; } = DateTime.Now;

    public bool IsExam => Lab.Exam;
    public bool ShowGrading => !Lab.Exam;
    public bool CanCheck => !Lab.Exam || Lab.ExamCheck;

    public string Title => Lab.Title.Resolve(Text.Culture);
    /// <summary>L'énoncé, sans son titre « # … » (le panneau l'affiche déjà au-dessus).</summary>
    public string Statement
    {
        get
        {
            var text = Lab.Statement.Resolve(Text.Culture).TrimStart();
            return text.StartsWith("# ") && text.IndexOf('\n') is var end and > 0 ? text[(end + 1)..].TrimStart() : text;
        }
    }
    public string StepLabel => Text.T($"Scénario {Lab.Step}", $"Scenario {Lab.Step}");

    public string LevelLabel => Lab.Level switch
    {
        LabLevel.Beginner => Text.T("Débutant", "Beginner"),
        LabLevel.Intermediate => Text.T("Intermédiaire", "Intermediate"),
        _ => Text.T("Avancé", "Advanced")
    };

    public string ModeLabel => (Lab.IsPair, Mode) switch
    {
        (false, _) => Text.T("Seul", "Alone"),
        (true, ScenarioMode.Here) => Text.T("À deux sur ce SMAG-X", "By two on this SMAG-X"),
        (true, ScenarioMode.Serve) => Text.T("Deux SMAG-X : vous servez la forge", "Two SMAG-X: you serve the forge"),
        _ => Text.T("Deux SMAG-X : vous rejoignez la forge", "Two SMAG-X: you join the forge")
    };

    public string DurationLabel => Text.T($"{Lab.DurationMinutes} min", $"{Lab.DurationMinutes} min");

    public IReadOnlyList<ScenarioObjectiveItem> Objectives { get; private set; } = [];

    [ObservableProperty] private string _scoreLabel = string.Empty;
    [ObservableProperty] private bool _isCompleted;

    /// <summary>Le rappel du rôle joué sur le poste affiché (« Vous jouez Alice : … »), dans un scénario à deux.</summary>
    [ObservableProperty] private string _roleBrief = string.Empty;

    public bool HasRoleBrief => RoleBrief.Length > 0;
    partial void OnRoleBriefChanged(string value) => OnPropertyChanged(nameof(HasRoleBrief));

    public List<string> HintsUsed => Objectives.Where(o => o.HintSeen).Select(o => o.Objective.Id).ToList();

    private void Build()
    {
        var seen = Objectives.Where(o => o.HintSeen).Select(o => o.Objective.Id).ToHashSet();
        Objectives = Lab.Objectives.Select(o => new ScenarioObjectiveItem(o, Text.Culture, !Lab.Exam, Lab.IsPair) { HintSeen = seen.Contains(o.Id) }).ToList();
        ScoreLabel = $"0 / {Lab.MaxPoints} points";
        OnPropertyChanged(nameof(Objectives));
    }

    /// <summary>La langue a changé : les textes suivent, la vérification est à refaire.</summary>
    public void Relabel()
    {
        Build();
        IsCompleted = false;
        foreach (var name in new[] { nameof(Title), nameof(Statement), nameof(StepLabel), nameof(LevelLabel), nameof(ModeLabel), nameof(DurationLabel) })
            OnPropertyChanged(name);
    }

    /// <summary>Le rôle du poste affiché, rappelé en haut du panneau.</summary>
    public void ShowRole(string? roleId)
    {
        var role = Lab.Roles.FirstOrDefault(r => r.Id == roleId);
        RoleBrief = Lab.IsPair && role is not null
            ? Text.T($"Vous jouez {role.Name} : ", $"You play {role.Name}: ") + role.Brief.Resolve(Text.Culture)
            : "";
    }

    /// <summary>Le résultat du correcteur : chaque objectif prend son statut ; en examen, seulement atteint ou non.</summary>
    public void Apply(SmagxLabResult result)
    {
        foreach (var item in Objectives)
        {
            var r = result.Objectives.Single(o => o.Objective == item.Objective);
            item.Passed = r.Passed;
            item.Feedback = Lab.Exam || r.Passed == true ? "" : r.Feedback;
        }
        ScoreLabel = $"{result.Score:0.##} / {result.MaxPoints} points";
        IsCompleted = !Lab.Exam && result.Completed;
    }
}
