using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
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
        Traits = new TraitsPageViewModel(this);
        Attributes = new AttributesPageViewModel(this);
        Skills = new SkillsPageViewModel(this);
        CombatTechniques = new CombatTechniquesPageViewModel(this);
        Sections =
        [
            new("Grundlagen", "Basis", Basics),
            new("Vor-/Nachteile & SF", "Vor/N", Traits),
            new("Eigenschaften", "Eigen.", Attributes),
            new("Talente", "Talente", Skills),
            new("Kampftechniken", "Kampf", CombatTechniques),
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

    public TraitsPageViewModel Traits { get; }

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

    /// <summary>Ändert die Erschaffungsentscheidungen, speichert und berechnet neu.</summary>
    public void Apply(Action<CreationLayer> change)
    {
        change(Character.Creation);
        Save();
        Recalculate();
    }

    /// <summary>Berechnet neu und baut die Anzeige neu auf (z. B. nach abgebrochener Rückfrage).</summary>
    public void Refresh() => Recalculate();

    private void Recalculate()
    {
        State = _calculator.Calculate(Character);
        Basics.Update(State);
        Traits.Update(State);
        Attributes.Update(State);
        Skills.Update(State);
        CombatTechniques.Update(State);
        Summary.Update(State);
    }

    private void Save() => _shell.Repository.Save(Character);

    [RelayCommand]
    private void Back() => _shell.ShowCharacterList();
}
