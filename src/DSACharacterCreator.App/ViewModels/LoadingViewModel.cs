using CommunityToolkit.Mvvm.ComponentModel;

namespace DSACharacterCreator.ViewModels;

/// <summary>Ladehinweis beim Start, solange die Regeldaten im Hintergrund geladen werden.</summary>
public sealed partial class LoadingViewModel : ViewModelBase
{
    /// <summary>Fehlermeldung, falls das Laden scheitert; sonst <c>null</c>.</summary>
    [ObservableProperty]
    public partial string? Error { get; set; }
}
