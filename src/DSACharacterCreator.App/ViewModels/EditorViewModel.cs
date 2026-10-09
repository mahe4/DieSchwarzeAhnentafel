using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.ViewModels;

/// <summary>Bearbeitung eines Helden. Jede Änderung wird sofort gespeichert.</summary>
public partial class EditorViewModel : ViewModelBase
{
    private readonly MainViewModel _shell;

    public EditorViewModel(MainViewModel shell, Character character)
    {
        _shell = shell;
        Character = character;
        Name = character.Name;
        Summary = new SummaryViewModel(shell.Catalog, new SourceSelection(character.EnabledBooks));

        Sections =
        [
            new("Grundlagen", "Basis", new PlaceholderPageViewModel("Grundlagen",
                "Erfahrungsgrad, Spezies, Kultur und Profession (Meilenstein 3).")),
            new("Eigenschaften & Fertigkeiten", "Werte", new PlaceholderPageViewModel("Eigenschaften & Fertigkeiten",
                "Eigenschaften, Talente und Kampftechniken (Meilenstein 2).")),
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
    }

    public Character Character { get; }

    [ObservableProperty]
    public partial string Name { get; set; }

    partial void OnNameChanged(string value)
    {
        if (Character.Name == value) return;
        Character.Name = value;
        Save();
    }

    public IReadOnlyList<SectionViewModel> Sections { get; }

    [ObservableProperty]
    public partial SectionViewModel? SelectedSection { get; set; }

    public SummaryViewModel Summary { get; }

    private void Save() => _shell.Repository.Save(Character);

    [RelayCommand]
    private void Back() => _shell.ShowCharacterList();
}
