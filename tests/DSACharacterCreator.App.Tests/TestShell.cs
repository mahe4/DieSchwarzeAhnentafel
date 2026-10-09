using DSACharacterCreator.Content;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Persistence;
using DSACharacterCreator.Services;
using DSACharacterCreator.ViewModels;

namespace DSACharacterCreator.UiTests;

/// <summary>App-Hülle mit eigenem temporärem Speicherordner und gespielten Dateidialogen.</summary>
public sealed class TestShell : IDisposable
{
    public TestShell()
    {
        Directory = Path.Combine(Path.GetTempPath(), "dsa-ui-tests-" + Guid.NewGuid());
        using var books = EmbeddedContent.Open("books.json");
        Shell = new MainViewModel(new CharacterRepository(Directory), BookCatalog.Load(books), Dialogs);
    }

    public string Directory { get; }

    public FakeFileDialogs Dialogs { get; } = new();

    public MainViewModel Shell { get; }

    public CharacterListViewModel List => Assert.IsType<CharacterListViewModel>(Shell.CurrentPage);

    public EditorViewModel Editor => Assert.IsType<EditorViewModel>(Shell.CurrentPage);

    public void Dispose()
    {
        if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, recursive: true);
    }
}

public sealed class FakeFileDialogs : IFileDialogs
{
    public string? NextOpenContent { get; set; }

    public string? LastSavedContent { get; private set; }

    public Task<string?> OpenTextAsync(string title, string typeName, string pattern) => Task.FromResult(NextOpenContent);

    public Task<bool> SaveTextAsync(string title, string suggestedName, string typeName, string extension, string content)
    {
        LastSavedContent = content;
        return Task.FromResult(true);
    }
}
