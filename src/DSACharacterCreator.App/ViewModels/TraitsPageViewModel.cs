using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.ViewModels;

/// <summary>Gemeinsames für die Seiten mit Vor-/Nachteilen bzw. Sonderfertigkeiten: Zeilen abgleichen, Auswahlliste.</summary>
public abstract class TraitListPageViewModel(EditorViewModel editor) : ViewModelBase
{
    protected EditorViewModel Editor { get; } = editor;

    /// <summary>Zeilen bleiben erhalten, solange sich die Einträge nicht ändern (sonst verlöre ein Textfeld den Fokus).</summary>
    protected void Sync(ObservableCollection<TraitRowViewModel> rows, List<TraitState> traits, CharacterState state)
    {
        var same = rows.Count == traits.Count &&
                   rows.Zip(traits).All(p => p.First.Index == p.Second.Index && p.First.DefinitionId == p.Second.Definition.Id);
        if (!same)
        {
            rows.Clear();
            foreach (var t in traits) rows.Add(new TraitRowViewModel(Editor, t));
        }
        foreach (var (row, t) in rows.Zip(traits)) row.Update(t, state);
    }

    protected void Add(IReadOnlyList<TraitDefinition> list, string title, Func<TraitDefinition, string> group,
        Func<CreationLayer, List<TraitEntry>> target)
    {
        var sources = SourceSelection.ForCharacter(Editor.Character, Editor.Rules.Books);
        Editor.OpenPicker(title,
            list.Where(t => sources.IsAvailable(t.Publications)).Select(t => new PickerItemViewModel(
                t.Id, t.Name + (t.OpenLevels ? " (Stufen)" : t.MaxLevel is { } max ? $" I–{TraitRowViewModel.Roman(max)}" : ""),
                group(t), t.ApValueText.Split('\n')[0])),
            null,
            id => Editor.Apply(c => target(c).Add(new TraitEntry { Id = id })));
    }
}

/// <summary>Vor- und Nachteile.</summary>
public sealed partial class TraitsPageViewModel(EditorViewModel editor) : TraitListPageViewModel(editor)
{
    public ObservableCollection<TraitRowViewModel> Advantages { get; } = [];

    public ObservableCollection<TraitRowViewModel> Disadvantages { get; } = [];

    [ObservableProperty]
    public partial string LimitsText { get; private set; } = "";

    public void Update(CharacterState state)
    {
        var cr = Editor.Rules.Creation;
        LimitsText = $"Vorteile {state.AdvantageApTotal} / {cr.MaxAdvantageAp} AP · " +
                     $"Nachteile {-state.DisadvantageApTotal} / {cr.MaxDisadvantageAp} AP (inkl. automatischer der Spezies)";
        Sync(Advantages, state.Traits.Where(t => !t.Definition.IsDisadvantage).ToList(), state);
        Sync(Disadvantages, state.Traits.Where(t => t.Definition.IsDisadvantage).ToList(), state);
    }

    [RelayCommand]
    private void AddAdvantage() => Add(Editor.Rules.Advantages, "Vorteil hinzufügen", t => Group(t.Name), c => c.Traits);

    [RelayCommand]
    private void AddDisadvantage() => Add(Editor.Rules.Disadvantages, "Nachteil hinzufügen", t => Group(t.Name), c => c.Traits);

    /// <summary>Gruppierung nach Anfangsbuchstabe (die Wiki-Listen sind alphabetisch).</summary>
    private static string Group(string name) => name[..1].ToUpperInvariant();
}

/// <summary>Profane, magische und karmale Sonderfertigkeiten.</summary>
public sealed partial class SpecialAbilitiesPageViewModel(EditorViewModel editor) : TraitListPageViewModel(editor)
{
    public ObservableCollection<TraitRowViewModel> SpecialAbilities { get; } = [];

    [ObservableProperty]
    public partial string TotalText { get; private set; } = "";

    /// <summary>„Die Profession sieht 6 AP für Sprachen und Schriften vor“ – frei zu verteilen.</summary>
    [ObservableProperty]
    public partial string? LanguageHint { get; private set; }

    public void Update(CharacterState state)
    {
        Sync(SpecialAbilities, state.SpecialAbilities.ToList(), state);
        TotalText = $"{state.Ap.SpecialAbilities} AP";
        LanguageHint = state.Origin.Profession?.LanguageAp is { } budget
            ? $"Die Vorlage {state.Origin.Profession.Name} sieht Sprachen und Schriften für insgesamt {budget} AP vor – " +
              "bitte selbst über „Sprache“ bzw. „Schrift“ hinzufügen."
            : null;
    }

    [RelayCommand]
    private void AddSpecialAbility() => Add(Editor.Rules.SpecialAbilities, "Sonderfertigkeit hinzufügen",
        t => t.Category ?? "Sonstige", c => c.SpecialAbilities);
}

public sealed partial class TraitRowViewModel : ObservableObject
{
    private readonly EditorViewModel _editor;
    private TraitState _state;

    public TraitRowViewModel(EditorViewModel editor, TraitState state)
    {
        _editor = editor;
        _state = state;
        var def = state.Definition;
        Options = def.OptionTarget switch
        {
            TraitOptionTarget.Skill => editor.Rules.Skills.OrderBy(s => s.Name, StringComparer.CurrentCulture)
                .Select(s => new ChoiceOption(s.Id, $"{s.Name} ({s.ImprovementCost})"))
                // Begabung: auch Zauber und Liturgien (nur die mit Fertigkeitswert)
                .Concat(TraitRules.AppliesToSpells(def)
                    ? editor.Rules.Spells.Where(s => s.HasValue).OrderBy(s => s.Name, StringComparer.CurrentCulture)
                        .Select(s => new ChoiceOption(s.Id, $"{s.Name} ({s.Category}, {s.ImprovementCost})"))
                    : []).ToList(),
            TraitOptionTarget.CombatTechnique => editor.Rules.CombatTechniques.OrderBy(s => s.Name, StringComparer.CurrentCulture)
                .Select(s => new ChoiceOption(s.Id, $"{s.Name} ({s.ImprovementCost})")).ToList(),
            TraitOptionTarget.Language => editor.Rules.Languages
                .OrderBy(l => l.Region != "Aventurien").ThenBy(l => l.Name, StringComparer.CurrentCulture)
                .Select(l => new ChoiceOption(l.Id, l.Region == "Aventurien" ? l.Name : $"{l.Name} ({l.Region})")).ToList(),
            TraitOptionTarget.Script => editor.Rules.Scripts
                .OrderBy(l => l.Region != "Aventurien").ThenBy(l => l.Name, StringComparer.CurrentCulture)
                .Select(l => new ChoiceOption(l.Id, $"{l.Name} ({l.ImprovementCost}{(l.Region == "Aventurien" ? "" : ", " + l.Region)})")).ToList(),
            _ when def.Cost.Type == TraitCostType.ByOption =>
                def.Cost.Options!.Select(o => new ChoiceOption(o.Name, $"{o.Name} ({o.Ap} AP)")).ToList(),
            _ => [],
        };
    }

    public int Index => _state.Index;

    public string DefinitionId => _state.Definition.Id;

    public bool IsAutomatic => _state.Automatic;

    /// <summary>
    /// Eintrag aus der Erschaffung in der Steigerungsphase: nur die Stufe lässt sich ändern; ablegen lassen sich nur
    /// Nachteile (abbauen).
    /// </summary>
    private bool IsLocked => _editor.Character.Phase == CharacterPhase.Advancement && _state.Entry.LogEntryId is null;

    public bool IsEditable => !_state.Automatic && !IsLocked;

    public bool CanRemove => !_state.Automatic && (!IsLocked || _state.Definition.IsDisadvantage);

    public bool HasLevels => !_state.Automatic && _state.Definition.MaxLevel is not null;

    public bool HasOptions => IsEditable && Options.Count > 0;

    public bool IsManualCost => IsEditable && _state.Definition.Cost.Type == TraitCostType.Manual && !_state.Entry.Free;

    public IReadOnlyList<ChoiceOption> Options { get; }

    public string Details => string.Join(Environment.NewLine + Environment.NewLine, new[]
    {
        _state.Definition.RuleText,
        _state.Definition.Tradition is { } tr ? TraditionText(tr) : null,
        _state.Definition.DetailsText,
        _state.Definition.PrerequisitesText is { } p ? "Voraussetzung: " + p : null,
        "AP-Wert: " + _state.Definition.ApValueText,
        _state.Definition.CombatTechniquesText is { } ct ? "Kampftechniken: " + ct : null,
        _state.Definition.FittingStylesText is { } fs ? "Passende Stile: " + fs : null,
        _state.Definition.ExtendedAbilitiesText is { } ea ? "Erweiterte Sonderfertigkeiten: " + ea : null,
        _state.Definition.Note is { } n ? "Anmerkung: " + n : null,
    }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Replace("\n", " ")));

    private static string TraditionText(TraditionInfo t) =>
        (t.PrimaryAttribute is { } a ? $"Leiteigenschaft: {a}" + (t.HalfPrimaryAttribute ? " (zählt für die AsP nur zur Hälfte)" : "") : "Keine Leiteigenschaft") +
        (t.FavouredSkillsText is { } f ? $"\nWohlgefällige Talente: {f}" : "");

    [ObservableProperty]
    public partial string Title { get; private set; } = "";

    [ObservableProperty]
    public partial string ApText { get; private set; } = "";

    /// <summary>Fehler und Warnungen.</summary>
    [ObservableProperty]
    public partial string? IssueText { get; private set; }

    /// <summary>Hinweise (z. B. „Voraussetzung von Hand prüfen“).</summary>
    [ObservableProperty]
    public partial string? HintText { get; private set; }

    /// <summary>Präzisierungsfeld direkt sichtbar (sonst im aufgeklappten Bereich).</summary>
    public bool ShowSpecificationInline => IsEditable && (_state.Entry.Specification is not null || _state.Definition.Name.Contains('…')
        || _state.Definition.Name is "Ortskenntnis" or "Fertigkeitsspezialisierung (Talente)");

    public bool ShowSpecificationInDetails => IsEditable && !ShowSpecificationInline;

    [ObservableProperty]
    public partial bool ShowDetails { get; set; }

    public static string Roman(int level) => TraitRules.Roman(level);

    public void Update(TraitState state, CharacterState character)
    {
        _state = state;
        var def = state.Definition;
        Title = def.Name + (def.MaxLevel is not null ? " " + Roman(state.Level) : "") +
                (IsAutomatic ? " (automatisch durch Spezies)" : "");
        var source = state.Entry.LogEntryId is not null ? " · Steigerung" : state.Entry.Source switch
        {
            Core.Rules.Templates.CultureSource => " · aus Kultur",
            Core.Rules.Templates.ProfessionSource => " · aus Profession",
            _ => "",
        };
        ApText = (IsAutomatic ? "im Spezies-AP enthalten" : state.Entry.Free ? "kostenlos" : state.Ap is { } ap ? $"{ap} AP" : "AP offen")
                 + source;
        var target = def.IsSpecialAbility
            ? CharacterCalculator.SpecialAbilityTarget(state.Index)
            : CharacterCalculator.TraitTarget(state.Index);
        var issues = IsAutomatic ? [] : character.IssuesFor(target).ToList();
        string? Join(IEnumerable<Issue> list) => list.Any() ? string.Join(Environment.NewLine, list.Select(i => i.Message)) : null;
        IssueText = Join(issues.Where(i => i.Severity != IssueSeverity.Info));
        HintText = Join(issues.Where(i => i.Severity == IssueSeverity.Info));
        OnPropertyChanged(nameof(IsEditable));
        OnPropertyChanged(nameof(CanRemove));
        OnPropertyChanged(nameof(HasOptions));
        OnPropertyChanged(nameof(IsManualCost));
        OnPropertyChanged(nameof(ShowSpecificationInline));
        OnPropertyChanged(nameof(ShowSpecificationInDetails));
        OnPropertyChanged(nameof(SelectedOption));
        OnPropertyChanged(nameof(Specification));
        OnPropertyChanged(nameof(ManualApText));
        IncreaseLevelCommand.NotifyCanExecuteChanged();
        DecreaseLevelCommand.NotifyCanExecuteChanged();
    }

    public ChoiceOption? SelectedOption
    {
        get => Options.FirstOrDefault(o => o.Id == _state.Entry.Option);
        set
        {
            if (value is null || value.Id == _state.Entry.Option) return;
            Change(e => e.Option = value.Id);
        }
    }

    public string? Specification
    {
        get => _state.Entry.Specification;
        set
        {
            var text = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (text == _state.Entry.Specification) return;
            Change(e => e.Specification = text);
        }
    }

    /// <summary>Eigener AP-Wert als Text (Nachteile negativ).</summary>
    public string ManualApText
    {
        get => _state.Entry.ManualAp?.ToString() ?? "";
        set
        {
            int? ap = int.TryParse(value.Replace("–", "-").Trim(), out var v) ? v : null;
            if (ap is { } a && _state.Definition.IsDisadvantage) ap = -Math.Abs(a);
            if (ap == _state.Entry.ManualAp) return;
            Change(e => e.ManualAp = ap);
        }
    }

    private List<TraitEntry> Entries(CreationLayer c) => _state.Definition.IsSpecialAbility ? c.SpecialAbilities : c.Traits;

    private void Change(Action<TraitEntry> change)
    {
        var index = _state.Index;
        _editor.Apply(c => change(Entries(c)[index]));
    }

    private bool CanIncreaseLevel() => HasLevels && _state.Level < _state.Definition.MaxLevel;

    [RelayCommand(CanExecute = nameof(CanIncreaseLevel))]
    private void IncreaseLevel()
    {
        if (CanIncreaseLevel()) Change(e => e.Level = _state.Level + 1);
    }

    /// <summary>Nach der Erschaffung nicht unter die Stufe aus der Erschaffung (außer Nachteile verringern).</summary>
    private bool CanDecreaseLevel() => HasLevels && _state.Level > Math.Max(1, IsLocked && !_state.Definition.IsDisadvantage
        ? (_editor.Character.Creation.SpecialAbilities.Concat(_editor.Character.Creation.Traits)
            .FirstOrDefault(e => e.Key == _state.Entry.Key)?.Level ?? 1)
        : 1);

    [RelayCommand(CanExecute = nameof(CanDecreaseLevel))]
    private void DecreaseLevel()
    {
        if (CanDecreaseLevel()) Change(e => e.Level = _state.Level - 1);
    }

    [RelayCommand]
    private void Remove()
    {
        if (!CanRemove) return;
        var index = _state.Index;
        _editor.Apply(c => Entries(c).RemoveAt(index));
    }

    [RelayCommand]
    private void ToggleDetails() => ShowDetails = !ShowDetails;
}
