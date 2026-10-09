namespace DSACharacterCreator.ViewModels;

/// <summary>Ein Hauptbereich der App (Navigationseintrag).</summary>
public sealed class SectionViewModel(string title, string shortTitle, ViewModelBase page)
{
    public string Title { get; } = title;

    /// <summary>Kurzbeschriftung für die untere Navigationsleiste auf schmalen Bildschirmen.</summary>
    public string ShortTitle { get; } = shortTitle;

    public ViewModelBase Page { get; } = page;
}
