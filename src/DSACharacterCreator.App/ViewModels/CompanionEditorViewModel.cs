using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.ViewModels;

/// <summary>
/// Ein geöffnetes Tier: eigene Tabs (ersetzen die Navigation des Helden), eigene Übersicht. Änderungen gehen als Einträge
/// ins Protokoll des Tiers (über <see cref="EditorViewModel.Mutate"/>, also rückgängig machbar).
/// </summary>
public sealed partial class CompanionEditorViewModel : ObservableObject
{
    public CompanionEditorViewModel(EditorViewModel editor, Guid id)
    {
        Editor = editor;
        Id = id;
        Result = new CompanionResultPageViewModel(this);
        Basics = new CompanionBasicsPageViewModel(this);
        Values = new CompanionValuesPageViewModel(this);
        Skills = new CompanionSkillsPageViewModel(this);
        Traits = new CompanionAbilitiesPageViewModel(this, "Vor- und Nachteile",
            "Vor- und Nachteile der Tierart kosten nichts. Bei der Aufnahme höchstens 30 AP für Vorteile und 30 AP für Nachteile.",
            [new("Vorteile", "Vorteil", [AnimalAbilityKind.Advantage]), new("Nachteile", "Nachteil", [AnimalAbilityKind.Disadvantage])]);
        SpecialAbilities = new CompanionAbilitiesPageViewModel(this, "Sonderfertigkeiten",
            "Sonderfertigkeiten der Tierart sind kostenlos. Kampfsonderfertigkeiten lernen Tiere nur nach Meisterentscheid; das Wiki nennt dafür keinen AP-Wert.",
            [new("Tiersonderfertigkeiten", "Sonderfertigkeit", [AnimalAbilityKind.SpecialAbility]),
             new("Kampfsonderfertigkeiten", "Kampf-SF", [AnimalAbilityKind.CombatSpecialAbility])]);
        FamiliarTricks = new CompanionAbilitiesPageViewModel(this, "Vertrautentricks",
            "Keine Probe, 1 Aktion; für Antimagie gelten sie als Zauber mit QS 1. Zwiegespräch und Hexensinn hat jeder Vertraute.",
            [new("Vertrautentricks", "Vertrautentrick", [AnimalAbilityKind.FamiliarTrick])]);
        Training = new CompanionAbilitiesPageViewModel(this, "Ausbildung & Tricks",
            "Ein Ausbildungsaufsatz je Tier (Reittier lässt sich mit einem zweiten kombinieren, Vertraute dürfen zwei haben); " +
            "zusätzlich bis zu KL/2 Tricks.",
            [new("Ausbildungsaufsätze", "Ausbildungsaufsatz", [AnimalAbilityKind.Training]), new("Tricks", "Trick", [AnimalAbilityKind.Trick])]);
        Log = new CompanionLogPageViewModel(this);
        Summary = new CompanionSummaryViewModel();
        BuildSections();
    }

    public EditorViewModel Editor { get; }

    public Guid Id { get; }

    public RuleData Rules => Editor.Rules;

    /// <summary>Das Tier im aktuellen Helden (nach Rückgängig ist es ein neues Objekt mit derselben ID).</summary>
    public Companion? Companion => Editor.Character.Companions.FirstOrDefault(c => c.Id == Id);

    [ObservableProperty]
    public partial CompanionState? State { get; private set; }

    public CompanionResultPageViewModel Result { get; }

    public CompanionBasicsPageViewModel Basics { get; }

    public CompanionValuesPageViewModel Values { get; }

    public CompanionSkillsPageViewModel Skills { get; }

    public CompanionAbilitiesPageViewModel Traits { get; }

    public CompanionAbilitiesPageViewModel SpecialAbilities { get; }

    public CompanionAbilitiesPageViewModel FamiliarTricks { get; }

    public CompanionAbilitiesPageViewModel Training { get; }

    public CompanionLogPageViewModel Log { get; }

    public CompanionSummaryViewModel Summary { get; }

    [ObservableProperty]
    public partial IReadOnlyList<SectionViewModel> Sections { get; private set; } = [];

    [ObservableProperty]
    public partial SectionViewModel? SelectedSection { get; set; }

    partial void OnSelectedSectionChanged(SectionViewModel? oldValue, SectionViewModel? newValue)
    {
        if (oldValue is not null) oldValue.IsSelected = false;
        if (newValue is not null) newValue.IsSelected = true;
    }

    [ObservableProperty]
    public partial string Title { get; private set; } = "";

    [ObservableProperty]
    public partial string Subtitle { get; private set; } = "";

    [ObservableProperty]
    public partial bool IsFamiliar { get; private set; }

    /// <summary>„38 / 100 AP“ für die Kopfzeile am Handy.</summary>
    [ObservableProperty]
    public partial string ApText { get; private set; } = "";

    /// <summary>Vertrautentricks gibt es nur bei Vertrauten: Tabs neu aufbauen, wenn sich die Art ändert.</summary>
    private void BuildSections()
    {
        var familiar = Companion?.Kind == CompanionKind.Familiar;
        var selected = SelectedSection?.Page;
        var list = new List<SectionViewModel>
        {
            new("Ergebnis", "Ergebnis", Result, SectionIcons.Sheet),
            new("Grundlagen", "Basis", Basics, SectionIcons.Basics),
            new("Eigenschaften", "Eigen.", Values, SectionIcons.Attributes),
            new("Vor-/Nachteile", "Vor/N", Traits, SectionIcons.Traits),
            new("Sonderfertigkeiten", "SF", SpecialAbilities, SectionIcons.SpecialAbilities),
            new("Talente", "Talente", Skills, SectionIcons.Skills),
        };
        if (familiar) list.Add(new("Vertrautentricks", "V-Tricks", FamiliarTricks, SectionIcons.Magic));
        list.Add(new("Ausbildung & Tricks", "Ausb.", Training, SectionIcons.Training));
        list.Add(new("Protokoll", "Protok.", Log, SectionIcons.Log));
        Sections = list;
        IsFamiliar = familiar;
        SelectedSection = list.FirstOrDefault(s => s.Page == selected) ?? list[0];
    }

    public void Update(CompanionState state)
    {
        State = state;
        if ((state.Companion.Kind == CompanionKind.Familiar) != IsFamiliar) BuildSections();
        Title = CompanionTexts.Name(state);
        Subtitle = CompanionTexts.Subtitle(state, Editor.Character.Name);
        ApText = $"{state.Ap.Spent} / {state.Ap.Total} AP";
        Result.Update(state);
        Basics.Update(state);
        Values.Update(state);
        Skills.Update(state);
        Traits.Update(state);
        SpecialAbilities.Update(state);
        FamiliarTricks.Update(state);
        Training.Update(state);
        Log.Update(state);
        Summary.Update(state);
        Values.PageApText = ApTextFor(state, RaiseTarget.Attribute, RaiseTarget.Energy, RaiseTarget.Attack, RaiseTarget.Defense);
        Skills.PageApText = ApTextFor(state, RaiseTarget.Skill);
    }

    private static string ApTextFor(CompanionState state, params RaiseTarget[] kinds) =>
        $"{state.Entries.Where(e => e.Entry is RaiseEntry r && kinds.Contains(r.Kind)).Sum(e => e.Ap)} AP";

    /// <summary>Ändert das Tier (Sicherung für Rückgängig, speichern, neu berechnen).</summary>
    public void Change(Action<Companion> change)
    {
        if (Companion is { } c) Editor.Mutate(() => change(c));
    }

    /// <summary>Steigert bzw. senkt einen Wert um einen Schritt. Folgt die Steigerung direkt auf eine des gleichen Werts,
    /// wird sie zusammengefasst; Senken nimmt die letzte eigene Steigerung zurück.</summary>
    public void Step(RaiseTarget kind, string target, int delta) => Change(c =>
    {
        bool Same(LogEntry e) => e is RaiseEntry r && r.Kind == kind && r.Target == target;
        if (delta > 0)
        {
            if (c.Log.Count > 0 && Same(c.Log[^1]) && c.Log[^1] is RaiseEntry last && last.To > last.From)
                last.To += delta;
            else
                c.Log.Add(new RaiseEntry { Kind = kind, Target = target, From = 0, To = delta });
            return;
        }
        var index = c.Log.FindLastIndex(e => Same(e) && e is RaiseEntry { To: var to, From: var from } && to > from);
        if (index < 0) return;
        var raise = (RaiseEntry)c.Log[index];
        raise.To += delta;
        if (raise.To <= raise.From) c.Log.RemoveAt(index);
    });

    /// <summary>Summe der eigenen Steigerungen eines Werts (Untergrenze beim Senken).</summary>
    public int Raised(RaiseTarget kind, string target) =>
        Companion?.Log.OfType<RaiseEntry>().Where(r => r.Kind == kind && r.Target == target).Sum(r => r.To - r.From) ?? 0;

    [RelayCommand]
    private void Close() => Editor.CloseCompanion();
}
