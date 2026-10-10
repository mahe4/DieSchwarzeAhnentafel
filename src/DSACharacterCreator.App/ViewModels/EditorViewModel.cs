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
        Values = new ValuesPageViewModel(this);
        Sections =
        [
            new("Grundlagen", "Basis", Basics),
            new("Eigenschaften & Fertigkeiten", "Werte", Values),
            new("Vor-/Nachteile & SF", "SF", new PlaceholderPageViewModel("Vor-/Nachteile & Sonderfertigkeiten",
                "Vorteile, Nachteile und Sonderfertigkeiten (Meilenstein 4).")),
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

    public ValuesPageViewModel Values { get; }

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

    public void Confirm(string title, string message, string confirmText, Action onConfirm) =>
        Confirmation = new ConfirmationViewModel(title, message, confirmText,
            () =>
            {
                Confirmation = null;
                onConfirm();
            },
            () => Confirmation = null);

    /// <summary>Schließt Rückfrage bzw. Auswahlliste, falls offen (Android-Zurück-Taste).</summary>
    public bool ClosePicker()
    {
        if (Confirmation is not null)
        {
            Confirmation = null;
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

    private void Recalculate()
    {
        State = _calculator.Calculate(Character);
        Basics.Update(State);
        Values.Update(State);
        Summary.Update(State);
    }

    private void Save() => _shell.Repository.Save(Character);

    [RelayCommand]
    private void Back() => _shell.ShowCharacterList();
}
