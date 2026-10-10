using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Persistence;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.ViewModels;

/// <summary>Bearbeitung eines Helden. Jede Änderung wird sofort gespeichert und neu berechnet.</summary>
public partial class EditorViewModel : ViewModelBase
{
    private readonly MainViewModel _shell;
    private readonly CharacterCalculator _calculator;

    public EditorViewModel(MainViewModel shell, Character character)
    {
        _shell = shell;
        _calculator = new CharacterCalculator(shell.Rules);
        Character = character;
        Name = character.Name;

        Basics = new BasicsPageViewModel(this);
        Phase = new PhaseViewModel(this);
        Attributes = new AttributesPageViewModel(this);
        Traits = new TraitsPageViewModel(this);
        SpecialAbilities = new SpecialAbilitiesPageViewModel(this);
        CombatTechniques = new CombatTechniquesPageViewModel(this);
        Skills = new SkillsPageViewModel(this);
        Sections =
        [
            new("Grundlagen", "Basis", Basics),
            new("Eigenschaften", "Eigen.", Attributes),
            new("Vor-/Nachteile", "Vor/N", Traits),
            new("Sonderfertigkeiten", "SF", SpecialAbilities),
            new("Kampftechniken", "Kampf", CombatTechniques),
            new("Talente", "Talente", Skills),
            new("Magie & Götterwirken", "Magie", new PlaceholderPageViewModel("Magie & Götterwirken",
                "Zauber, Rituale, Liturgien, Zeremonien (Meilenstein 6).")),
            new("Ausrüstung", "Ausr.", new PlaceholderPageViewModel("Ausrüstung",
                "Waffen, Rüstungen, Gegenstände und Geld (Meilenstein 7).")),
            new("Charakterbogen", "Bogen", new PlaceholderPageViewModel("Charakterbogen",
                "Gesamtansicht und PDF-Export (Meilenstein 7).")),
        ];
        SelectedSection = Sections[0];
        Recalculate();
    }

    public Character Character { get; }

    public RuleData Rules => _shell.Rules;

    [ObservableProperty]
    public partial CharacterState State { get; private set; }

    [ObservableProperty]
    public partial string Name { get; set; }

    partial void OnNameChanged(string value)
    {
        if (Character.Name == value) return;
        Character.Name = value;
        Save();
    }

    public BasicsPageViewModel Basics { get; }

    /// <summary>Erschaffung abschließen bzw. korrigieren, AP-Gutschriften und Protokoll.</summary>
    public PhaseViewModel Phase { get; }

    /// <summary>Erschaffungsphase (auch während einer Korrektur): Änderungen betreffen die Erschaffung selbst.</summary>
    [ObservableProperty]
    public partial bool IsCreation { get; private set; } = true;

    public TraitsPageViewModel Traits { get; }

    public SpecialAbilitiesPageViewModel SpecialAbilities { get; }

    public AttributesPageViewModel Attributes { get; }

    public SkillsPageViewModel Skills { get; }

    public CombatTechniquesPageViewModel CombatTechniques { get; }

    public IReadOnlyList<SectionViewModel> Sections { get; }

    [ObservableProperty]
    public partial SectionViewModel? SelectedSection { get; set; }

    public SummaryViewModel Summary { get; } = new();

    /// <summary>Offene Auswahlliste (Kultur, Profession); <c>null</c> = keine.</summary>
    [ObservableProperty]
    public partial PickerViewModel? Picker { get; private set; }

    public void OpenPicker(string title, IEnumerable<PickerItemViewModel> items, string? selectedId, Action<string> onPick) =>
        Picker = new PickerViewModel(title, items, selectedId, onPick, () => Picker = null);

    /// <summary>Offene Rückfrage; <c>null</c> = keine.</summary>
    [ObservableProperty]
    public partial ConfirmationViewModel? Confirmation { get; private set; }

    public void Confirm(string title, string message, string confirmText, Action onConfirm, Action? onCancel = null)
    {
        _onCancelConfirmation = onCancel;
        Confirmation = new ConfirmationViewModel(title, message, confirmText,
            () =>
            {
                _onCancelConfirmation = null;
                Confirmation = null;
                onConfirm();
            },
            CancelConfirmation);
    }

    private Action? _onCancelConfirmation;

    private void CancelConfirmation()
    {
        var onCancel = _onCancelConfirmation;
        _onCancelConfirmation = null;
        Confirmation = null;
        onCancel?.Invoke();
    }

    /// <summary>Schließt Rückfrage bzw. Auswahlliste, falls offen (Android-Zurück-Taste).</summary>
    public bool ClosePicker()
    {
        if (Confirmation is not null)
        {
            CancelConfirmation();
            return true;
        }
        if (Picker is null) return false;
        Picker = null;
        return true;
    }

    /// <summary>
    /// Ändert die Entscheidungen, speichert und berechnet neu. In der Erschaffung direkt die Erschaffungsschicht; danach
    /// eine Arbeitskopie des aktuellen Stands, deren Unterschiede als Einträge ins Steigerungsprotokoll gehen.
    /// </summary>
    public void Apply(Action<CreationLayer> change)
    {
        if (Character.Phase == CharacterPhase.Creation || State.Advancement is not { } info)
        {
            Mutate(() => change(Character.Creation));
            return;
        }
        var state = State;
        var working = info.Working.Clone();
        change(working);
        if (Phases.NeedsGmPermission(info.Working, working))
            Confirm("Erlaubnis der Spielleitung",
                "Ob ein Vorteil erworben oder ein Nachteil abgebaut werden darf, entscheidet die Spielleitung (Regel-Wiki, " +
                "Erfahrung). Erlaubt sie es, gelten die angegebenen AP-Werte.",
                "Spielleitung erlaubt es", () => Mutate(() => Phases.Record(Character, state, working, Rules)), Refresh);
        else
            Mutate(() => Phases.Record(Character, state, working, Rules));
    }

    /// <summary>Jede Änderung am Helden: Sicherung für Rückgängig, speichern, neu berechnen.</summary>
    public void Mutate(Action change)
    {
        _undo.Add(CharacterSerializer.Serialize(Character));
        if (_undo.Count > MaxUndo) _undo.RemoveAt(0);
        _redo.Clear();
        change();
        Save();
        Recalculate();
    }

    private const int MaxUndo = 100;
    private readonly List<string> _undo = [];
    private readonly List<string> _redo = [];

    private bool CanUndo() => _undo.Count > 0;

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (!CanUndo()) return;
        _redo.Add(CharacterSerializer.Serialize(Character));
        Restore(_undo[^1]);
        _undo.RemoveAt(_undo.Count - 1);
    }

    private bool CanRedo() => _redo.Count > 0;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (!CanRedo()) return;
        _undo.Add(CharacterSerializer.Serialize(Character));
        Restore(_redo[^1]);
        _redo.RemoveAt(_redo.Count - 1);
    }

    /// <summary>Übernimmt einen gesicherten Stand in dasselbe Charakterobjekt (die Heldenliste hält es).</summary>
    private void Restore(string json)
    {
        var saved = CharacterSerializer.Deserialize(json);
        Character.Name = saved.Name;
        Character.Phase = saved.Phase;
        Character.EnabledBooks = saved.EnabledBooks;
        Character.KnownBooks = saved.KnownBooks;
        Character.Creation = saved.Creation;
        Character.Log = saved.Log;
        Character.CreationSnapshot = saved.CreationSnapshot;
        Name = saved.Name;
        Save();
        Recalculate();
    }

    /// <summary>Berechnet neu und baut die Anzeige neu auf (z. B. nach abgebrochener Rückfrage).</summary>
    public void Refresh() => Recalculate();

    private void Recalculate()
    {
        State = _calculator.Calculate(Character);
        IsCreation = Character.Phase == CharacterPhase.Creation;
        Basics.Update(State);
        Phase.Update(State);
        Traits.Update(State);
        SpecialAbilities.Update(State);
        Attributes.Update(State);
        Skills.Update(State);
        CombatTechniques.Update(State);
        Summary.Update(State);
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void Save() => _shell.Repository.Save(Character);

    [RelayCommand]
    private void Back() => _shell.ShowCharacterList();
}
