using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.ViewModels;

/// <summary>Vor- und Nachteile (Sonderfertigkeiten folgen mit Meilenstein 4b).</summary>
public sealed partial class TraitsPageViewModel : ViewModelBase
{
    private readonly EditorViewModel _editor;

    public TraitsPageViewModel(EditorViewModel editor)
    {
        _editor = editor;
    }

    public ObservableCollection<TraitRowViewModel> Advantages { get; } = [];

    public ObservableCollection<TraitRowViewModel> Disadvantages { get; } = [];

    [ObservableProperty]
    public partial string LimitsText { get; private set; } = "";

    public void Update(CharacterState state)
    {
        var cr = _editor.Rules.Creation;
        LimitsText = $"Vorteile {state.AdvantageApTotal} / {cr.MaxAdvantageAp} AP · " +
                     $"Nachteile {-state.DisadvantageApTotal} / {cr.MaxDisadvantageAp} AP (inkl. automatischer der Spezies)";
        Sync(Advantages, state.Traits.Where(t => !t.Definition.IsDisadvantage).ToList(), state);
        Sync(Disadvantages, state.Traits.Where(t => t.Definition.IsDisadvantage).ToList(), state);
    }

    /// <summary>Zeilen bleiben erhalten, solange sich die Einträge nicht ändern (sonst verlöre ein Textfeld den Fokus).</summary>
    private void Sync(ObservableCollection<TraitRowViewModel> rows, List<TraitState> traits, CharacterState state)
    {
        var same = rows.Count == traits.Count &&
                   rows.Zip(traits).All(p => p.First.Index == p.Second.Index && p.First.DefinitionId == p.Second.Definition.Id);
        if (!same)
        {
            rows.Clear();
            foreach (var t in traits) rows.Add(new TraitRowViewModel(_editor, t));
        }
        foreach (var (row, t) in rows.Zip(traits)) row.Update(t, state);
    }

    [RelayCommand]
    private void AddAdvantage() => Add(_editor.Rules.Advantages, "Vorteil hinzufügen");

    [RelayCommand]
    private void AddDisadvantage() => Add(_editor.Rules.Disadvantages, "Nachteil hinzufügen");

    private void Add(IReadOnlyList<TraitDefinition> list, string title)
    {
        var sources = SourceSelection.ForCharacter(_editor.Character, _editor.Rules.Books);
        _editor.OpenPicker(title,
            list.Where(t => sources.IsAvailable(t.Publications)).Select(t => new PickerItemViewModel(
                t.Id, t.Name + (t.MaxLevel is { } max ? $" I–{TraitRowViewModel.Roman(max)}" : ""),
                Group(t.Name), t.ApValueText.Split('\n')[0])),
            null,
            id => _editor.Apply(c => c.Traits.Add(new TraitEntry { Id = id })));
    }

    /// <summary>Gruppierung nach Anfangsbuchstabe (die Wiki-Listen sind alphabetisch).</summary>
    private static string Group(string name) => name[..1].ToUpperInvariant();
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
        Options = def.Cost.Type switch
        {
            TraitCostType.BySkillFactor when def.Cost.Target == TraitOptionTarget.Skill =>
                editor.Rules.Skills.OrderBy(s => s.Name, StringComparer.CurrentCulture)
                    .Select(s => new ChoiceOption(s.Id, $"{s.Name} ({s.ImprovementCost})")).ToList(),
            TraitCostType.BySkillFactor =>
                editor.Rules.CombatTechniques.OrderBy(s => s.Name, StringComparer.CurrentCulture)
                    .Select(s => new ChoiceOption(s.Id, $"{s.Name} ({s.ImprovementCost})")).ToList(),
            TraitCostType.ByOption => def.Cost.Options!.Select(o => new ChoiceOption(o.Name, $"{o.Name} ({o.Ap} AP)")).ToList(),
            _ => [],
        };
    }

    public int Index => _state.Index;

    public string DefinitionId => _state.Definition.Id;

    public bool IsAutomatic => _state.Automatic;

    public bool IsEditable => !_state.Automatic;

    public bool HasLevels => IsEditable && _state.Definition.MaxLevel is not null;

    public bool HasOptions => IsEditable && Options.Count > 0;

    public bool IsManualCost => IsEditable && _state.Definition.Cost.Type == TraitCostType.Manual;

    public IReadOnlyList<ChoiceOption> Options { get; }

    public string Details => string.Join(Environment.NewLine + Environment.NewLine, new[]
    {
        _state.Definition.RuleText,
        _state.Definition.PrerequisitesText is { } p ? "Voraussetzung: " + p : null,
        "AP-Wert: " + _state.Definition.ApValueText,
        _state.Definition.Note is { } n ? "Anmerkung: " + n : null,
    }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Replace("\n", " ")));

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
    public bool ShowSpecificationInline => IsEditable && (_state.Entry.Specification is not null || _state.Definition.Name.Contains('…'));

    public bool ShowSpecificationInDetails => IsEditable && !ShowSpecificationInline;

    [ObservableProperty]
    public partial bool ShowDetails { get; set; }

    public static string Roman(int level) => level switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", _ => "VII" };

    public void Update(TraitState state, CharacterState character)
    {
        _state = state;
        var def = state.Definition;
        Title = def.Name + (def.MaxLevel is not null ? " " + Roman(state.Level) : "") +
                (IsAutomatic ? " (automatisch durch Spezies)" : "");
        ApText = IsAutomatic ? "im Spezies-AP enthalten" : state.Ap is { } ap ? $"{ap} AP" : "AP offen";
        var issues = IsAutomatic ? [] : character.IssuesFor(CharacterCalculator.TraitTarget(state.Index)).ToList();
        string? Join(IEnumerable<Issue> list) => list.Any() ? string.Join(Environment.NewLine, list.Select(i => i.Message)) : null;
        IssueText = Join(issues.Where(i => i.Severity != IssueSeverity.Info));
        HintText = Join(issues.Where(i => i.Severity == IssueSeverity.Info));
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

    private void Change(Action<TraitEntry> change)
    {
        var index = _state.Index;
        _editor.Apply(c => change(c.Traits[index]));
    }

    private bool CanIncreaseLevel() => HasLevels && _state.Level < _state.Definition.MaxLevel;

    [RelayCommand(CanExecute = nameof(CanIncreaseLevel))]
    private void IncreaseLevel()
    {
        if (CanIncreaseLevel()) Change(e => e.Level = _state.Level + 1);
    }

    private bool CanDecreaseLevel() => HasLevels && _state.Level > 1;

    [RelayCommand(CanExecute = nameof(CanDecreaseLevel))]
    private void DecreaseLevel()
    {
        if (CanDecreaseLevel()) Change(e => e.Level = _state.Level - 1);
    }

    [RelayCommand]
    private void Remove()
    {
        if (IsAutomatic) return;
        var index = _state.Index;
        _editor.Apply(c => c.Traits.RemoveAt(index));
    }

    [RelayCommand]
    private void ToggleDetails() => ShowDetails = !ShowDetails;
}
