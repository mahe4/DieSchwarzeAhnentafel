using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.ViewModels;

/// <summary>Dauerhafte Übersicht: AP-Konto, Eigenschaften, Quellen.</summary>
public sealed class SummaryViewModel(BookCatalog catalog, SourceSelection sources) : ViewModelBase
{
    public int ApTotal => 0;

    public int ApSpent => 0;

    public string ApText => $"{ApSpent} / {ApTotal} AP";

    public IReadOnlyList<string> Attributes { get; } = ["MU", "KL", "IN", "CH", "FF", "GE", "KO", "KK"];

    public string SourcesText =>
        $"{sources.EnabledBookIds.Count} von {catalog.Books.Count} Publikationen aktiv";
}
