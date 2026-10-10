using CommunityToolkit.Mvvm.ComponentModel;

namespace DSACharacterCreator.ViewModels;

/// <summary>Ein Hauptbereich der App (Navigationseintrag).</summary>
public sealed partial class SectionViewModel(string title, string shortTitle, ViewModelBase page) : ObservableObject
{
    public string Title { get; } = title;

    /// <summary>Kurzbeschriftung für die untere Navigationsleiste auf schmalen Bildschirmen.</summary>
    public string ShortTitle { get; } = shortTitle;

    public ViewModelBase Page { get; } = page;

    /// <summary>Die Seite wird gerade angezeigt.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>
    /// Seite für die Anzeige: <c>null</c> bis zum ersten Öffnen, danach bleibt sie erhalten (ein Neuaufbau bei jedem
    /// Wechsel kostete am Handy bis zu 2 s für die Talente).
    /// </summary>
    [ObservableProperty]
    public partial ViewModelBase? LoadedPage { get; private set; }

    partial void OnIsSelectedChanged(bool value)
    {
        if (value) LoadedPage ??= Page;
    }
}
