using CommunityToolkit.Mvvm.ComponentModel;

namespace DSACharacterCreator.ViewModels;

/// <summary>Seite des Editors mit den AP, die in diesem Bereich angefallen sind (rechts neben der Überschrift).</summary>
public abstract partial class PageViewModel : ViewModelBase
{
    /// <summary>„124 AP“; leer bei Seiten ohne AP-Kosten (Grundlagen, Ausrüstung, Bogen).</summary>
    [ObservableProperty]
    public partial string PageApText { get; set; } = "";
}
