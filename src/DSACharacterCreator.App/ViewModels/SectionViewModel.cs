using CommunityToolkit.Mvvm.ComponentModel;

namespace DSACharacterCreator.ViewModels;

/// <summary>Ein Hauptbereich der App (Navigationseintrag).</summary>
/// <param name="icon">Strichsymbol (24×24), siehe <see cref="SectionIcons"/>.</param>
public sealed partial class SectionViewModel(string title, string shortTitle, ViewModelBase page,
    Avalonia.Media.Geometry? icon = null) : ObservableObject
{
    public Avalonia.Media.Geometry? Icon { get; } = icon;

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
