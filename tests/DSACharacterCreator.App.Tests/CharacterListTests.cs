using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.UiTests;

public sealed class CharacterListTests : IDisposable
{
    private readonly TestShell _app = new();

    public void Dispose() => _app.Dispose();

    [Fact]
    public void StartsWithEmptyCharacterList()
    {
        Assert.False(_app.List.HasCharacters);
    }

    [Fact]
    public void New_OpensEditorWithDefaultSources()
    {
        _app.List.NewCommand.Execute(null);

        var character = _app.Editor.Character;
        Assert.Contains("grw", character.EnabledBooks);
        Assert.DoesNotContain("wdv", character.EnabledBooks);
    }

    [Fact]
    public void RenamingInEditor_IsSavedAndShownInList()
    {
        _app.List.NewCommand.Execute(null);
        _app.Editor.Name = "Alrik";

        _app.Editor.BackCommand.Execute(null);

        var card = Assert.Single(_app.List.Characters);
        Assert.Equal("Alrik", card.Name);
    }

    [Fact]
    public void UnnamedCharacter_GetsPlaceholderName()
    {
        _app.List.NewCommand.Execute(null);
        _app.Shell.GoBack();

        Assert.Equal("Unbenannter Held", Assert.Single(_app.List.Characters).Name);
    }

    [Fact]
    public void Open_ShowsEditorForThatCharacter()
    {
        CreateNamed("Alrik");
        var card = Assert.Single(_app.List.Characters);

        card.OpenCommand.Execute(null);

        Assert.Equal("Alrik", _app.Editor.Name);
    }

    [Fact]
    public void Duplicate_AddsCopy()
    {
        CreateNamed("Alrik");

        _app.List.Characters[0].DuplicateCommand.Execute(null);

        Assert.Equal(2, _app.List.Characters.Count);
        Assert.Contains(_app.List.Characters, c => c.Name == "Alrik (Kopie)");
    }

    [Fact]
    public void Delete_RequiresConfirmation()
    {
        CreateNamed("Alrik");
        var list = _app.List;

        list.Characters[0].DeleteCommand.Execute(null);
        Assert.True(list.IsDeletePending);
        Assert.Single(list.Characters);

        list.CancelDeleteCommand.Execute(null);
        Assert.False(list.IsDeletePending);
        Assert.Single(list.Characters);

        list.Characters[0].DeleteCommand.Execute(null);
        list.ConfirmDeleteCommand.Execute(null);
        Assert.Empty(list.Characters);
    }

    [Fact]
    public void BackButton_ClosesDeleteConfirmationFirst()
    {
        CreateNamed("Alrik");
        _app.List.Characters[0].DeleteCommand.Execute(null);

        Assert.True(_app.Shell.GoBack());
        Assert.False(_app.List.IsDeletePending);
        Assert.False(_app.Shell.GoBack());
    }

    [Fact]
    public async Task ExportThenImport_CreatesSecondCharacter()
    {
        CreateNamed("Alrik");

        await _app.List.Characters[0].ExportCommand.ExecuteAsync(null);
        _app.Dialogs.NextOpenContent = _app.Dialogs.LastSavedContent;
        await _app.List.ImportCommand.ExecuteAsync(null);

        Assert.Equal(2, _app.List.Characters.Count);
        Assert.All(_app.List.Characters, c => Assert.Equal("Alrik", c.Name));
    }

    [Fact]
    public async Task Import_InvalidFile_ShowsError()
    {
        _app.Dialogs.NextOpenContent = "kein Held";

        await _app.List.ImportCommand.ExecuteAsync(null);

        Assert.StartsWith("Import fehlgeschlagen", _app.List.StatusText);
        Assert.Empty(_app.List.Characters);
    }

    [Fact]
    public async Task OptolithExportThenImport_CreatesHeroFromOptolithFile()
    {
        CreateNamed("Alrik");

        await _app.List.Characters[0].ExportOptolithCommand.ExecuteAsync(null);
        Assert.Contains("\"clientVersion\"", _app.Dialogs.LastSavedContent);
        Assert.StartsWith("„Alrik“ für Optolith exportiert.", _app.List.StatusText);

        _app.Dialogs.NextOpenContent = _app.Dialogs.LastSavedContent;
        await _app.List.ImportCommand.ExecuteAsync(null);

        Assert.StartsWith("„Alrik“ aus Optolith importiert.", _app.List.StatusText);
        Assert.Equal(2, _app.List.Characters.Count);
    }

    [Fact]
    public async Task DarkAidExportThenImport_CreatesHeroFromTdcFile()
    {
        CreateNamed("Alrik");

        await _app.List.Characters[0].ExportDarkAidCommand.ExecuteAsync(null);
        Assert.Contains("\"rulesystem\": \"dsa5aventurien\"", _app.Dialogs.LastSavedContent);
        Assert.StartsWith("„Alrik“ für Dark Aid exportiert.", _app.List.StatusText);

        _app.Dialogs.NextOpenContent = _app.Dialogs.LastSavedContent;
        await _app.List.ImportCommand.ExecuteAsync(null);

        Assert.StartsWith("„Alrik“ aus Dark Aid importiert.", _app.List.StatusText);
        Assert.Equal(2, _app.List.Characters.Count);
    }

    private void CreateNamed(string name)
    {
        _app.List.NewCommand.Execute(null);
        _app.Editor.Name = name;
        _app.Shell.GoBack();
    }
}
