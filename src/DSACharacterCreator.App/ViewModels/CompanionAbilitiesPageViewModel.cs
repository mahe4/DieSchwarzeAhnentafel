using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.ViewModels;

/// <summary>Abschnitt einer Fähigkeitenseite (z. B. „Vorteile“ mit Knopf „＋ Vorteil“).</summary>
public sealed record CompanionAbilityGroupSpec(string Title, string AddLabel, IReadOnlyList<AnimalAbilityKind> Kinds);

/// <summary>Tabs mit Tricks, Ausbildungsaufsätzen, Vertrautentricks, Sonderfertigkeiten bzw. Vor-/Nachteilen.</summary>
public sealed class CompanionAbilitiesPageViewModel : PageViewModel
{
    private readonly CompanionEditorViewModel _owner;

    public CompanionAbilitiesPageViewModel(CompanionEditorViewModel owner, string title, string hint,
        IReadOnlyList<CompanionAbilityGroupSpec> groups)
    {
        _owner = owner;
        Title = title;
        Hint = hint;
        Groups = groups.Select(g => new CompanionAbilityGroupViewModel(owner, g)).ToList();
    }

    public string Title { get; }

    public string Hint { get; }

    public IReadOnlyList<CompanionAbilityGroupViewModel> Groups { get; }

    public void Update(CompanionState s)
    {
        foreach (var g in Groups) g.Update(s);
        PageApText = $"{s.Abilities.Where(a => a.Source == CompanionAbilitySource.Acquired && Groups.Any(g => g.Spec.Kinds.Contains(a.Definition.Kind)))
            .Sum(a => a.Ap ?? 0)} AP";
    }
}

public sealed partial class CompanionAbilityGroupViewModel(CompanionEditorViewModel owner, CompanionAbilityGroupSpec spec)
    : ObservableObject
{
    public CompanionAbilityGroupSpec Spec { get; } = spec;

    public string Title => Spec.Title;

    public string AddLabel => Spec.AddLabel;

    public ObservableCollection<CompanionAbilityRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    private CompanionState? _state;

    public void Update(CompanionState s)
    {
        _state = s;
        var items = s.Abilities.Where(a => Spec.Kinds.Contains(a.Definition.Kind)).ToList();
        // Fähigkeiten der Tierart (nur als Namen in den Tierwerten) zuerst, kostenlos
        var animalNames = Spec.Kinds.Contains(AnimalAbilityKind.Advantage) ? s.Animal?.Advantages
            : Spec.Kinds.Contains(AnimalAbilityKind.Disadvantage) ? s.Animal?.Disadvantages
            : Spec.Kinds.Contains(AnimalAbilityKind.SpecialAbility) ? s.Animal?.SpecialAbilities
            : null;
        var keys = (animalNames ?? []).Select(n => "animal:" + n).Concat(items.Select(Key)).ToList();
        if (Rows.Count != keys.Count || !Rows.Zip(keys).All(p => p.First.Key == p.Second))
        {
            Rows.Clear();
            foreach (var n in animalNames ?? []) Rows.Add(new CompanionAbilityRowViewModel(owner, "animal:" + n, n));
            foreach (var a in items) Rows.Add(new CompanionAbilityRowViewModel(owner, Key(a), a.Name));
        }
        foreach (var (row, a) in Rows.Skip(animalNames?.Count ?? 0).Zip(items)) row.Update(a, s);
        IsEmpty = Rows.Count == 0;
    }

    private static string Key(CompanionAbility a) =>
        $"{a.Source}:{a.Definition.Id}:{a.Entry.LogEntryId}:{a.From?.Id}";

    [RelayCommand]
    private void Add()
    {
        if (_state is not { } s) return;
        var rules = owner.Rules;
        var sources = SourceSelection.ForCharacter(owner.Editor.Character, rules.Books);
        var kind = s.Animal is null ? null : rules.Familiars.Kinds.GetValueOrDefault(s.Animal.Id);
        var excluded = s.Tradition?.ExcludedTricks ?? [];
        string Group(AnimalAbilityDefinition d)
        {
            if (d.Kind == AnimalAbilityKind.FamiliarTrick)
                return !excluded.Contains(d.Id) && CompanionCalculator.FitsAnimal(d.AnimalsText, kind)
                    ? "Für diese Tierart" : "Andere Tierarten (Meisterentscheid)";
            if (d.Kind == AnimalAbilityKind.Training && s.Animal?.Training is { } t)
                return t.Trainings.Contains(d.Id) ? "Für diese Tierart möglich" : "Andere (Meisterentscheid)";
            return Spec.Kinds.Count > 1 ? KindTitle(d.Kind) : d.Name[..1].ToUpperInvariant();
        }
        // Was das Tier schon kostenlos hat (Zwiegespräch, Hexensinn), nicht noch einmal anbieten
        var free = s.Abilities.Where(a => a.Source == CompanionAbilitySource.Familiar).Select(a => a.Definition.Id).ToHashSet();
        var list = rules.AnimalAbilities
            .Where(d => Spec.Kinds.Contains(d.Kind) && sources.IsAvailable(d.Publications) && !free.Contains(d.Id))
            .OrderBy(d => Group(d).StartsWith("Andere"))
            .ThenBy(d => d.Name, StringComparer.CurrentCulture)
            .Select(d => new PickerItemViewModel(d.Id, s.Tradition?.TrickNames?.GetValueOrDefault(d.Id) ?? d.Name, Group(d),
                d.ApValueText.Split('\n')[0], () => CompanionAbilityRowViewModel.Describe(d),
                unique: d.Id != "animal-trick.kunststueck"));
        owner.Editor.OpenPicker(AddLabel + " hinzufügen", list, null, id => owner.Change(c =>
        {
            var def = rules.FindAnimalAbility(id);
            c.Log.Add(new AcquireEntry
            {
                Kind = EntryKind.AnimalAbility,
                Entry = new TraitEntry { Id = id, Option = def?.Bonuses?.FirstOrDefault(b => b.Options is { Count: > 1 })?.Options?[0] },
            });
        }), sortGroups: false, multiple: true);
    }

    private static string KindTitle(AnimalAbilityKind kind) => kind switch
    {
        AnimalAbilityKind.Trick => "Tricks",
        AnimalAbilityKind.Training => "Ausbildungsaufsätze",
        AnimalAbilityKind.SpecialAbility => "Tiersonderfertigkeiten",
        AnimalAbilityKind.CombatSpecialAbility => "Kampfsonderfertigkeiten",
        AnimalAbilityKind.Advantage => "Vorteile",
        AnimalAbilityKind.Disadvantage => "Nachteile",
        _ => "Vertrautentricks",
    };
}

/// <summary>Eine Fähigkeit: von der Tierart (nur Name), kostenlos (Vertraute, Ausbildungsaufsatz) oder erworben.</summary>
public sealed partial class CompanionAbilityRowViewModel(CompanionEditorViewModel owner, string key, string title) : ObservableObject
{
    public string Key { get; } = key;

    [ObservableProperty]
    public partial string Title { get; private set; } = title;

    [ObservableProperty]
    public partial string ApText { get; private set; } = "von der Tierart · kostenlos";

    [ObservableProperty]
    public partial bool CanRemove { get; private set; }

    [ObservableProperty]
    public partial string? IssueText { get; private set; }

    [ObservableProperty]
    public partial string? Details { get; private set; }

    [ObservableProperty]
    public partial bool ShowDetails { get; set; }

    public bool HasDetails => Details is not null;

    /// <summary>Auswahl bei „Fährtensuchen- oder Sinnesschärfe-FW +8“.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ChoiceOption> Options { get; private set; } = [];

    [ObservableProperty]
    public partial ChoiceOption? SelectedOption { get; set; }

    public bool HasOptions => Options.Count > 0;

    [ObservableProperty]
    public partial bool IsManualCost { get; private set; }

    [ObservableProperty]
    public partial string ManualApText { get; set; } = "";

    private Guid? _logEntryId;
    private bool _updating;

    public void Update(CompanionAbility a, CompanionState s)
    {
        _updating = true;
        var d = a.Definition;
        _logEntryId = a.Entry.LogEntryId;
        Title = a.Name + (d.MaxLevel is > 1 ? $" {a.Level}" : "") +
                (a.Entry.Specification is { Length: > 0 } spec ? $" ({spec})" : "");
        ApText = a.Source switch
        {
            CompanionAbilitySource.Familiar => "hat jeder Vertraute · kostenlos",
            CompanionAbilitySource.Training => $"aus {a.From?.Name} · kostenlos",
            _ => a.Ap is { } ap ? $"{ap} AP" : "AP-Wert eintragen",
        } + (d.Loyalty is { } l ? $" · Loyalität {l}" : "");
        CanRemove = a.Source == CompanionAbilitySource.Acquired && _logEntryId is not null;
        Details = Describe(d);
        OnPropertyChanged(nameof(HasDetails));
        IsManualCost = CanRemove && (d.Cost.Type == TraitCostType.Manual || a.Entry.ManualAp is not null);
        ManualApText = a.Entry.ManualAp?.ToString() ?? "";
        var options = CanRemove
            ? d.Bonuses?.FirstOrDefault(b => b.Options is { Count: > 1 })?.Options
                ?.Select(id => new ChoiceOption(id, owner.Rules.FindSkill(id)?.Name ?? id)).ToList() ?? []
            : [];
        if (options.Count != Options.Count) Options = options;
        OnPropertyChanged(nameof(HasOptions));
        SelectedOption = Options.FirstOrDefault(o => o.Id == a.Entry.Option) ?? Options.FirstOrDefault();
        var target = _logEntryId is { } id ? "log:" + id : d.Id;
        var issues = s.Issues.Where(i => i.TargetId == target).Select(i => i.Message).ToList();
        IssueText = issues.Count > 0 ? string.Join(Environment.NewLine, issues) : null;
        _updating = false;
    }

    /// <summary>Regeltext und Felder der Wiki-Seite.</summary>
    public static string Describe(AnimalAbilityDefinition d)
    {
        var lines = new List<string> { d.RuleText };
        void Add(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) lines.Add($"{label}: {value}");
        }
        Add("Boni", d.BonusesText?.Replace("\n", ", "));
        Add("Tricks", d.Tricks is { } t ? string.Join(", ", t.Select(x => x.Split('.')[^1])) : null);
        Add("Tierarten", d.AnimalsText);
        Add("AsP-Kosten", d.AstralCostText);
        Add("Wirkungsdauer", d.DurationText);
        Add("Merkmal", d.Property);
        Add("Erschwernis", d.Penalty);
        Add("Voraussetzung", d.PrerequisitesText);
        Add("AP-Wert", d.ApValueText);
        Add("Anmerkung", d.Note);
        Add("Errata", d.Errata);
        return string.Join(Environment.NewLine, lines.Where(l => l.Length > 0));
    }

    partial void OnSelectedOptionChanged(ChoiceOption? value)
    {
        if (_updating || value is null || _logEntryId is not { } id) return;
        owner.Change(c =>
        {
            if (c.Log.FirstOrDefault(e => e.Id == id) is AcquireEntry acquire) acquire.Entry.Option = value.Id;
        });
    }

    partial void OnManualApTextChanged(string value)
    {
        if (_updating || _logEntryId is not { } id) return;
        int? ap = int.TryParse(value.Trim().Replace('−', '-'), out var v) ? v : null;
        owner.Change(c =>
        {
            if (c.Log.FirstOrDefault(e => e.Id == id) is AcquireEntry acquire) acquire.Entry.ManualAp = ap;
        });
    }

    /// <summary>Entfernt den Erwerb aus dem Protokoll des Tiers (Korrektur).</summary>
    [RelayCommand]
    private void Remove()
    {
        if (_logEntryId is not { } id) return;
        owner.Change(c => c.Log.RemoveAll(e => e.Id == id));
    }

    [RelayCommand]
    private void ToggleDetails() => ShowDetails = !ShowDetails;
}
