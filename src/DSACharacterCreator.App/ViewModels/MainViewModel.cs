using CommunityToolkit.Mvvm.ComponentModel;
using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Persistence;
using DSACharacterCreator.Services;

namespace DSACharacterCreator.ViewModels;

/// <summary>Hülle der App: wechselt zwischen Charakterliste (Hauptmenü) und Editor.</summary>
public partial class MainViewModel : ViewModelBase
{
    public MainViewModel(CharacterRepository repository, BookCatalog catalog, IFileDialogs fileDialogs)
    {
        Repository = repository;
        Catalog = catalog;
        FileDialogs = fileDialogs;
        CurrentPage = new CharacterListViewModel(this);
    }

    public CharacterRepository Repository { get; }

    public BookCatalog Catalog { get; }

    public IFileDialogs FileDialogs { get; }

    [ObservableProperty]
    public partial ViewModelBase CurrentPage { get; set; }

    public void OpenCharacter(Character character) => CurrentPage = new EditorViewModel(this, character);

    public void ShowCharacterList() => CurrentPage = new CharacterListViewModel(this);

    /// <summary>Zurück-Navigation (Android-Zurück-Taste). Gibt <c>false</c> zurück, wenn es nichts zurückzugehen gibt.</summary>
    public bool GoBack()
    {
        if (CurrentPage is CharacterListViewModel list) return list.CancelPendingDelete();
        ShowCharacterList();
        return true;
    }
}
