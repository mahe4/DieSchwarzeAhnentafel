using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Persistence;

namespace DSACharacterCreator.ViewModels;

/// <summary>Hauptmenü: alle gespeicherten Helden.</summary>
public partial class CharacterListViewModel : ViewModelBase
{
    private const string FileTypeName = "DSA5-Held";

    private readonly MainViewModel _shell;

    public CharacterListViewModel(MainViewModel shell)
    {
        _shell = shell;
        Reload();
    }

    public ObservableCollection<CharacterCardViewModel> Characters { get; } = [];

    public bool HasCharacters => Characters.Count > 0;

    public string StorageLocation => _shell.Repository.Directory;

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    /// <summary>Held, dessen Löschung gerade bestätigt werden muss.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDeletePending), nameof(PendingDeleteText))]
    public partial CharacterCardViewModel? PendingDelete { get; set; }

    public bool IsDeletePending => PendingDelete is not null;

    public string PendingDeleteText => PendingDelete is null
        ? ""
        : $"„{PendingDelete.Name}“ wird endgültig gelöscht. Das lässt sich nicht rückgängig machen.";

    private void Reload()
    {
        var result = _shell.Repository.LoadAll();
        Characters.Clear();
        foreach (var character in result.Characters) Characters.Add(new CharacterCardViewModel(character, this));
        OnPropertyChanged(nameof(HasCharacters));
        if (result.Failed.Count > 0)
            StatusText = $"{result.Failed.Count} Datei(en) im Speicherordner konnten nicht gelesen werden.";
    }

    [RelayCommand]
    private void New()
    {
        var character = new Character
        {
            EnabledBooks = SourceSelection.Default(_shell.Catalog).EnabledBookIds.ToList(),
        };
        _shell.Repository.Save(character);
        _shell.OpenCharacter(character);
    }

    public void Open(CharacterCardViewModel card) => _shell.OpenCharacter(card.Character);

    public void Duplicate(CharacterCardViewModel card)
    {
        var copy = _shell.Repository.Duplicate(card.Character);
        Reload();
        StatusText = $"„{CharacterCardViewModel.DisplayName(copy)}“ angelegt.";
    }

    public void RequestDelete(CharacterCardViewModel card) => PendingDelete = card;

    [RelayCommand]
    private void ConfirmDelete()
    {
        if (PendingDelete is not { } card) return;
        _shell.Repository.Delete(card.Character.Id);
        PendingDelete = null;
        Reload();
        StatusText = $"„{card.Name}“ gelöscht.";
    }

    [RelayCommand]
    private void CancelDelete() => PendingDelete = null;

    /// <summary>Schließt die Löschbestätigung, falls offen.</summary>
    public bool CancelPendingDelete()
    {
        if (PendingDelete is null) return false;
        PendingDelete = null;
        return true;
    }

    [RelayCommand]
    private async Task Import()
    {
        var json = await _shell.FileDialogs.OpenTextAsync("Held importieren", FileTypeName, "*.json");
        if (json is null) return;
        try
        {
            var character = _shell.Repository.Import(json);
            Reload();
            StatusText = $"„{CharacterCardViewModel.DisplayName(character)}“ importiert.";
        }
        catch (InvalidDataException ex)
        {
            StatusText = "Import fehlgeschlagen: " + ex.Message;
        }
    }

    public async Task Export(CharacterCardViewModel card)
    {
        var fileName = string.Concat(card.Name.Split(Path.GetInvalidFileNameChars())) + CharacterSerializer.FileExtension;
        var saved = await _shell.FileDialogs.SaveTextAsync("Held exportieren", fileName, FileTypeName,
            CharacterSerializer.FileExtension, CharacterSerializer.Serialize(card.Character));
        if (saved) StatusText = $"„{card.Name}“ exportiert.";
    }
}
