namespace DSACharacterCreator.ViewModels;

/// <summary>Platzhalterseite für Bereiche, die noch nicht umgesetzt sind.</summary>
public sealed class PlaceholderPageViewModel(string title, string description) : ViewModelBase
{
    public string Title { get; } = title;

    public string Description { get; } = description;
}
