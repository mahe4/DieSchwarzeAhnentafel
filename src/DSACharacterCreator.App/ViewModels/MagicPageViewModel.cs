using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.ViewModels;

/// <summary>Zauber, Rituale, Zaubertricks, magische Handlungen, Liturgien, Zeremonien und Segnungen.</summary>
public sealed partial class MagicPageViewModel(EditorViewModel editor) : ViewModelBase
{
    private CharacterState? _state;

    /// <summary>Zaubersprüche, Rituale und magische Handlungen (mit Fertigkeitswert).</summary>
    public ObservableCollection<SpellRowViewModel> Spells { get; } = [];

    public ObservableCollection<SpellRowViewModel> Cantrips { get; } = [];

    /// <summary>Liturgien und Zeremonien (mit Fertigkeitswert).</summary>
    public ObservableCollection<SpellRowViewModel> Liturgies { get; } = [];

    public ObservableCollection<SpellRowViewModel> Blessings { get; } = [];

    [ObservableProperty]
    public partial bool ShowMagic { get; private set; }

    [ObservableProperty]
    public partial bool ShowKarma { get; private set; }

    [ObservableProperty]
    public partial string MagicText { get; private set; } = "";

    [ObservableProperty]
    public partial string KarmaText { get; private set; } = "";

    public void Update(CharacterState state)
    {
        _state = state;
        Sync(Spells, state.Spells.Where(s => !s.Definition.IsKarmal && s.Definition.HasValue), state);
        Sync(Cantrips, state.Spells.Where(s => !s.Definition.IsKarmal && !s.Definition.HasValue), state);
        Sync(Liturgies, state.Spells.Where(s => s.Definition.IsKarmal && s.Definition.HasValue), state);
        Sync(Blessings, state.Spells.Where(s => s.Definition.IsKarmal && !s.Definition.HasValue), state);
        ShowMagic = state.Derived.Astral is not null || Spells.Count + Cantrips.Count > 0;
        ShowKarma = state.Derived.Karma is not null || Liturgies.Count + Blessings.Count > 0;
        var level = state.ExperienceLevel;
        var counted = state.Spells.Count(s => s.Definition.CountsForCreationLimit);
        var limits = editor.IsCreation
            ? $" · Erschaffung: {counted} von höchstens {level.MaxSpellsAndLiturgies} Zaubern/Liturgien, " +
              $"{state.Spells.Count(s => s.Foreign)} von {level.MaxForeignSpells} Fremdzaubern"
            : "";
        MagicText = $"{state.Ap.Spells} AP" + (state.Derived.Astral is { } asp ? $" · {asp} AsP" : "") + limits;
        KarmaText = $"{state.Ap.Liturgies} AP" + (state.Derived.Karma is { } kap ? $" · {kap} KaP" : "");
    }

    private void Sync(ObservableCollection<SpellRowViewModel> rows, IEnumerable<SpellState> spells, CharacterState state)
    {
        var list = spells.ToList();
        var same = rows.Count == list.Count && rows.Zip(list).All(p => p.First.Index == p.Second.Index && p.First.Id == p.Second.Definition.Id);
        if (!same)
        {
            rows.Clear();
            foreach (var s in list) rows.Add(new SpellRowViewModel(editor, s));
        }
        foreach (var (row, s) in rows.Zip(list)) row.Update(s, state);
    }

    [RelayCommand]
    private void AddSpell() => Add("Zauber, Ritual oder magische Handlung", d => !d.IsKarmal && d.HasValue);

    [RelayCommand]
    private void AddCantrip() => Add("Zaubertrick", d => d.Group == SpellGroup.Cantrip);

    [RelayCommand]
    private void AddLiturgy() => Add("Liturgie oder Zeremonie", d => d.IsKarmal && d.HasValue);

    [RelayCommand]
    private void AddBlessing() => Add("Segnung", d => d.Group == SpellGroup.Blessing);

    /// <summary>Auswahl gruppiert: eigene Tradition zuerst, danach fremde (Fremdzauber bzw. andere Gottheiten).</summary>
    private void Add(string what, Func<SpellDefinition, bool> filter)
    {
        if (_state is not { } state) return;
        var sources = SourceSelection.ForCharacter(editor.Character, editor.Rules.Books);
        var known = state.Spells.Select(s => s.Definition.Id).ToHashSet();
        var items = editor.Rules.Spells
            .Where(d => filter(d) && sources.IsAvailable(d.Publications) && !known.Contains(d.Id))
            .Select(d =>
            {
                var own = CharacterCalculator.IsOwnTradition(d, state.SpecialAbilities);
                var group = d.Group == SpellGroup.MagicalAction ? d.Category : d.Category + (own ? "" : d.IsKarmal ? " (andere Gottheiten)" : " (fremd)");
                return (own, d.Group, item: new PickerItemViewModel(d.Id, d.Name, group, SpellRowViewModel.Summary(d, editor.Rules)));
            })
            // eigene Tradition zuerst; innerhalb: Zaubersprüche, Rituale, magische Handlungen
            .OrderBy(x => !x.own).ThenBy(x => x.Group).ThenBy(x => x.item.Group, StringComparer.CurrentCulture)
            .Select(x => x.item);
        editor.OpenPicker(what + " hinzufügen", items, null,
            id => editor.Apply(c => c.Spells.Add(new SpellEntry
            {
                Id = id, Value = editor.Rules.FindSpell(id)?.HasValue == true ? 0 : null,
            })), sortGroups: false);
    }
}

/// <summary>Ein Zauber bzw. eine Liturgie; Zaubertricks und Segnungen ohne Wert.</summary>
public sealed partial class SpellRowViewModel : ValueRowViewModel
{
    private SpellState _state;

    public SpellRowViewModel(EditorViewModel editor, SpellState state)
        : base(editor, state.Definition.Id, state.Definition.Name)
    {
        _state = state;
    }

    public int Index => _state.Index;

    public SpellDefinition Definition => _state.Definition;

    public bool HasValue => _state.Definition.HasValue;

    [ObservableProperty]
    public partial string SourceText { get; private set; } = "";

    public override string Detail => Summary(_state.Definition, Editor.Rules) + SourceText;

    public override string Details => string.Join(Environment.NewLine + Environment.NewLine, new[]
    {
        Definition.RuleText,
        Definition.DetailsText,
        Definition.TraditionsText is { } t ? "Verbreitung: " + t : null,
        Definition.Note is { } n ? "Anmerkung: " + n : null,
    }.Where(x => !string.IsNullOrWhiteSpace(x)));

    /// <summary>„MU/KL/CH · C · Elementar“ bzw. „Zaubertrick · 1 AP“.</summary>
    public static string Summary(SpellDefinition d, RuleData rules) => string.Join(" · ", new[]
    {
        d.Check is { } check ? string.Join("/", check) : null,
        d.ImprovementCost?.ToString(),
        d.Property ?? d.Aspect,
        d.HasValue ? null : $"{d.Ap ?? 1} AP",
        d.Group == SpellGroup.MagicalAction ? d.Category : null,
    }.Where(x => x is not null));

    [ObservableProperty]
    public partial string ApText { get; private set; } = "";

    public void Update(SpellState state, CharacterState character)
    {
        _state = state;
        SourceText = (state.Foreign ? " · Fremdzauber" : "") +
                     (state.Entry.LogEntryId is not null ? " · Steigerung" : state.Entry.Source == Templates.ProfessionSource ? " · aus Profession" : "");
        OnPropertyChanged(nameof(Detail));
        ApText = $"{state.Ap} AP";
        SetValues(state.Value ?? 0, state.Min, state.Max, character.IssuesFor(CharacterCalculator.SpellTarget(state.Index)));
        RemoveCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanRemove));
        OnPropertyChanged(nameof(ShowRemove));
    }

    protected override int? NextStepCost(int value) =>
        _state.Definition.ImprovementCost is { } f && value <= Editor.Rules.Costs.MaxValue ? Editor.Rules.Costs.StepCost(f, value) : null;

    protected override void Store(int value)
    {
        var index = _state.Index;
        Editor.Apply(c => c.Spells[index].Value = value);
    }

    /// <summary>In der Steigerungsphase lassen sich nur selbst aktivierte Einträge wieder entfernen.</summary>
    public bool CanRemove => Editor.IsCreation || _state.Entry.LogEntryId is not null;

    /// <summary>Bei Einträgen mit Wert steht „Entfernen“ im aufgeklappten Bereich.</summary>
    public bool ShowRemove => ShowDetails && CanRemove;

    protected override void OnDetailsToggled() => OnPropertyChanged(nameof(ShowRemove));

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void Remove()
    {
        if (!CanRemove) return;
        var index = _state.Index;
        Editor.Apply(c => c.Spells.RemoveAt(index));
    }
}
