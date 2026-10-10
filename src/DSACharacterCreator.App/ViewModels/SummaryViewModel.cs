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
                       (ap.Unmodeled > 0 ? $" · Rest der Vorlage (vorläufig) {ap.Unmodeled}" : "") +
                       (ap.Spells > 0 ? $" · Zauber {ap.Spells}" : "") +
                       (ap.Liturgies > 0 ? $" · Liturgien {ap.Liturgies}" : "") +
                       (ap.Energies > 0 ? $" · Energien {ap.Energies}" : "") +
                       (state.Advancement is { } adv && (adv.AwardedAp != 0 || adv.ForfeitedAp != 0)
                           ? $" · Gutschriften {adv.AwardedAp:+0;−0}" + (adv.ForfeitedAp > 0 ? $" · verfallen {adv.ForfeitedAp}" : "")
                           : "");
        IsOverspent = ap.Remaining < 0;
        ExperienceLevelText = state.ExperienceLevel.Name;
        OriginText = string.Join(" · ", new[] { state.Origin.Species?.Name, state.Origin.Culture?.Name,
            state.Origin.Variant is { } v ? $"{state.Origin.Profession!.Name} ({v.Name})" : state.Origin.Profession?.Name }
            .Where(t => t is not null));
        Attributes = state.Attributes.Select(a => new AttributeValueViewModel(a.Definition.Id, a.Value)).ToList();
        var d = state.Derived;
        // Werte, die Belastung und Rüstung verändern: Basiswert und in Klammern der Wert mit Ausrüstung
        var eq = state.Equipment;
        static string With(int basis, int? current) => current is { } c && c != basis ? $"{basis} ({c})" : $"{basis}";
        DerivedText = (d.LifePoints is null ? "" : $"LeP {d.LifePoints} · SK {d.Spirit} · ZK {d.Toughness} · GS {With(d.Speed!.Value, eq.Speed)} · ") +
                      (d.Astral is { } asp ? $"AsP {asp} · " : "") +
                      (d.Karma is { } kap ? $"KaP {kap} · " : "") +
                      $"AW {With(d.Dodge, eq.Dodge)} · INI {With(d.Initiative, eq.Initiative)} · SchiP {d.FatePoints}" +
                      (eq.WornArmor is not null || eq.Encumbrance > 0
                          ? $" · RS {eq.Protection} · BE {eq.Encumbrance}" + (eq.Encumbrance != eq.ArmorEncumbrance ? $" (Rüstung {eq.ArmorEncumbrance})" : "")
                          : "");
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
