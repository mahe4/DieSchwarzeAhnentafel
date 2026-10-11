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
        Magic = new MagicPageViewModel(this);
        Equipment = new EquipmentPageViewModel(this);
        Sheet = new CharacterSheetPageViewModel(this);
        Sections =
        [
            new("Grundlagen", "Basis", Basics),
            new("Eigenschaften", "Eigen.", Attributes),
            new("Vor-/Nachteile", "Vor/N", Traits),
            new("Sonderfertigkeiten", "SF", SpecialAbilities),
            new("Kampftechniken", "Kampf", CombatTechniques),
            new("Talente", "Talente", Skills),
            new("Magie & Götterwirken", "Magie", Magic),
            new("Ausrüstung", "Ausr.", Equipment),
            new("Charakterbogen", "Bogen", Sheet),
        ];
        SelectedSection = Sections[0];
        Recalculate();
    }

    public Character Character { get; }

    public RuleData Rules => _shell.Rules;

    public Services.IFileDialogs FileDialogs => _shell.FileDialogs;

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

    public MagicPageViewModel Magic { get; }

    public EquipmentPageViewModel Equipment { get; }

    public CharacterSheetPageViewModel Sheet { get; }

    public AttributesPageViewModel Attributes { get; }

    public SkillsPageViewModel Skills { get; }

    public CombatTechniquesPageViewModel CombatTechniques { get; }

    public IReadOnlyList<SectionViewModel> Sections { get; }

    [ObservableProperty]
    public partial SectionViewModel? SelectedSection { get; set; }

    partial void OnSelectedSectionChanged(SectionViewModel? oldValue, SectionViewModel? newValue)
    {
#if DSA_TIMING
        // Seitenwechsel bis nach dem Zeichnen messen (Background kommt nach Layout und Rendern dran)
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Avalonia.Threading.Dispatcher.UIThread.Post(
            () => Services.Timing.Log($"Seitenwechsel {newValue?.Title}", watch), Avalonia.Threading.DispatcherPriority.Background);
#endif
        if (oldValue is not null) oldValue.IsSelected = false;
        if (newValue is not null) newValue.IsSelected = true;
        Sheet.SetActive(newValue?.Page == Sheet);
    }

    public SummaryViewModel Summary { get; } = new();

    /// <summary>Offene Auswahlliste (Kultur, Profession); <c>null</c> = keine.</summary>
    [ObservableProperty]
    public partial PickerViewModel? Picker { get; private set; }

    public void OpenPicker(string title, IEnumerable<PickerItemViewModel> items, string? selectedId, Action<string> onPick,
        bool sortGroups = true, bool multiple = false) =>
        Picker = new PickerViewModel(title, items, selectedId, onPick, () => Picker = null, sortGroups, multiple);

    /// <summary>Offene Quellenauswahl des Helden; <c>null</c> = geschlossen.</summary>
    [ObservableProperty]
    public partial SourcesViewModel? Sources { get; private set; }

    /// <summary>
    /// Quellen des Helden wählen. Gilt in jeder Phase und kostet keine AP; Gewähltes aus abgewählten Quellen bleibt
    /// erhalten (Warnung). Jede Änderung ist rückgängig zu machen.
    /// </summary>
    public void OpenSources()
    {
        var catalog = Rules.Books;
        Sources = new SourcesViewModel("Quellen des Helden",
            "Nur Inhalte aus aktiven Publikationen stehen zur Auswahl. Bereits Gewähltes aus abgewählten Quellen " +
            "bleibt erhalten und wird als Warnung angezeigt. Ü18-Werke sind gekennzeichnet.",
            catalog, _shell.BookUsage, SourceSelection.ForCharacter(Character, catalog),
            selection => Mutate(() => selection.ApplyTo(Character, catalog)),
            () => Sources = null,
            selection => SourceOverview.DependentChoices(State, selection));
    }

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
        if (Sources is not null)
        {
            Sources = null;
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

    /// <summary>Ausrüstung ändern: kostet keine AP und gilt in jeder Phase direkt (nicht im Steigerungsprotokoll).</summary>
    public void ChangeEquipment(Action<List<EquipmentEntry>> change) => Mutate(() => change(Character.Equipment));

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
        Character.Equipment = saved.Equipment;
        Character.Money = saved.Money;
        Character.Personal = saved.Personal;
        Name = saved.Name;
        Save();
        Recalculate();
    }

    /// <summary>Berechnet neu und baut die Anzeige neu auf (z. B. nach abgebrochener Rückfrage).</summary>
    public void Refresh() => Recalculate();

    private void Recalculate()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        State = _calculator.Calculate(Character);
        Services.Timing.Log($"Berechnung ({Character.Log.Count} Protokolleinträge)", watch);
        watch.Restart();
        IsCreation = Character.Phase == CharacterPhase.Creation;
        Basics.Update(State);
        Phase.Update(State);
        Traits.Update(State);
        SpecialAbilities.Update(State);
        Attributes.Update(State);
        Skills.Update(State);
        CombatTechniques.Update(State);
        Magic.Update(State);
        Equipment.Update(State);
        Sheet.Update(State);
        Summary.Update(State);
        Services.Timing.Log("Seiten aktualisieren", watch);
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void Save() => _shell.Repository.Save(Character);

    [RelayCommand]
    private void Back() => _shell.ShowCharacterList();
}
