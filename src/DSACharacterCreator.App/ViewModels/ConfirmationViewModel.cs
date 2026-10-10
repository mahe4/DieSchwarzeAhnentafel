using CommunityToolkit.Mvvm.Input;

namespace DSACharacterCreator.ViewModels;

/// <summary>Rückfrage vor einer Aktion, die Eingaben verwirft.</summary>
public sealed partial class ConfirmationViewModel(string title, string message, string confirmText, Action confirm, Action cancel)
{
    public string Title { get; } = title;

    public string Message { get; } = message;

    public string ConfirmText { get; } = confirmText;

    [RelayCommand]
    private void Confirm() => confirm();

    [RelayCommand]
    private void Cancel() => cancel();
}
