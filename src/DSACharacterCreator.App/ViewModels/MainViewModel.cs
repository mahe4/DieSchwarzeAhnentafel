using CommunityToolkit.Mvvm.ComponentModel;
using DSACharacterCreator.Content;
using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
        using var books = EmbeddedContent.Open("books.json");
        var catalog = BookCatalog.Load(books);
        Summary = new SummaryViewModel(catalog, SourceSelection.Default(catalog));

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

    public IReadOnlyList<SectionViewModel> Sections { get; }

    [ObservableProperty]
    public partial SectionViewModel? SelectedSection { get; set; }

    public SummaryViewModel Summary { get; }
}
