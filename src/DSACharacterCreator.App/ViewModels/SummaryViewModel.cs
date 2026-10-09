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
    public partial IReadOnlyList<IssueViewModel> Issues { get; private set; } = [];

    [ObservableProperty]
    public partial bool HasIssues { get; private set; }

    public void Update(CharacterState state)
    {
        var ap = state.Ap;
        ApText = $"{ap.Spent} / {ap.Total} AP";
        ApDetailText = $"Eigenschaften {ap.Attributes} · Talente {ap.Skills} · Kampftechniken {ap.CombatTechniques}";
        IsOverspent = ap.Remaining < 0;
        ExperienceLevelText = state.ExperienceLevel.Name;
        Attributes = state.Attributes.Select(a => new AttributeValueViewModel(a.Definition.Id, a.Value)).ToList();
        DerivedText = $"AW {state.Derived.Dodge} · INI {state.Derived.Initiative} · SchiP {state.Derived.FatePoints}";
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
