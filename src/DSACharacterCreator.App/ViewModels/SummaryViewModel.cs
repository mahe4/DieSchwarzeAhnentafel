using CommunityToolkit.Mvvm.ComponentModel;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.ViewModels;

/// <summary>Dauerhafte Übersicht: AP-Konto, Eigenschaften, Basiswerte, Hinweise.</summary>
public sealed partial class SummaryViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string ApText { get; private set; } = "";

    [ObservableProperty]
    public partial string ApDetailText { get; private set; } = "";

    [ObservableProperty]
    public partial bool IsOverspent { get; private set; }

    [ObservableProperty]
    public partial string ExperienceLevelText { get; private set; } = "";

    [ObservableProperty]
    public partial IReadOnlyList<AttributeValueViewModel> Attributes { get; private set; } = [];

    [ObservableProperty]
    public partial string DerivedText { get; private set; } = "";

    [ObservableProperty]
    public partial string OriginText { get; private set; } = "";

    [ObservableProperty]
    public partial bool HasSpecies { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<IssueViewModel> Issues { get; private set; } = [];

    [ObservableProperty]
    public partial bool HasIssues { get; private set; }

    public void Update(CharacterState state)
    {
        var ap = state.Ap;
        ApText = $"{ap.Spent} / {ap.Total} AP";
        ApDetailText = $"Spezies {ap.Species} · Eigenschaften {ap.Attributes} · Vorteile {ap.Advantages} · Nachteile {ap.Disadvantages} · SF {ap.SpecialAbilities} · " +
                       $"Talente {ap.Skills} · Kampftechniken {ap.CombatTechniques}" +
                       (ap.Unmodeled > 0 ? $" · SF/Sprachen/Zauber (vorläufig) {ap.Unmodeled}" : "");
        IsOverspent = ap.Remaining < 0;
        ExperienceLevelText = state.ExperienceLevel.Name;
        OriginText = string.Join(" · ", new[] { state.Origin.Species?.Name, state.Origin.Culture?.Name,
            state.Origin.Variant is { } v ? $"{state.Origin.Profession!.Name} ({v.Name})" : state.Origin.Profession?.Name }
            .Where(t => t is not null));
        Attributes = state.Attributes.Select(a => new AttributeValueViewModel(a.Definition.Id, a.Value)).ToList();
        var d = state.Derived;
        DerivedText = (d.LifePoints is null ? "" : $"LeP {d.LifePoints} · SK {d.Spirit} · ZK {d.Toughness} · GS {d.Speed} · ") +
                      (d.Astral is { } asp ? $"AsP {asp} · " : "") +
                      (d.Karma is { } kap ? $"KaP {kap} · " : "") +
                      $"AW {d.Dodge} · INI {d.Initiative} · SchiP {d.FatePoints}";
        HasSpecies = d.LifePoints is not null;
        Issues = state.Issues.OrderByDescending(i => i.Severity).Select(i => new IssueViewModel(i)).ToList();
        HasIssues = Issues.Count > 0;
    }
}

public sealed record AttributeValueViewModel(string Short, int Value);

public sealed class IssueViewModel(Issue issue)
{
    public string Message => issue.Message;

    public string Symbol => issue.Severity switch
    {
        IssueSeverity.Error => "⛔",
        IssueSeverity.Warning => "⚠",
        _ => "ℹ",
    };

    public bool IsError => issue.Severity == IssueSeverity.Error;
}
