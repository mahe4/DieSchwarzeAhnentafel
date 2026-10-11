using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Core.Tests;

public class SourceSelectionTests
{
    private static readonly BookCatalog Catalog = new([
        new Book("grw", "Regelwerk", ["Regelwerk"]),
        new Book("kom", "Kompendium", ["Kompendium"]),
        new Book("wdv", "Wege der Vereinigungen", ["Wege der Vereinigungen"], Adult: true),
    ]);

    [Fact]
    public void Default_EnablesEverythingExceptAdultBooks()
    {
        var selection = SourceSelection.Default(Catalog);

        Assert.True(selection.IsEnabled("grw"));
        Assert.True(selection.IsEnabled("kom"));
        Assert.False(selection.IsEnabled("wdv"));
    }

    [Fact]
    public void IsAvailable_WhenAnyPublicationIsEnabled()
    {
        var selection = new SourceSelection(["kom"]);

        Assert.True(selection.IsAvailable([new Publication("grw", 10), new Publication("kom", 99)]));
        Assert.False(selection.IsAvailable([new Publication("grw", 10)]));
        Assert.False(selection.IsAvailable([]));
    }

    [Fact]
    public void With_TogglesBookWithoutMutatingOriginal()
    {
        var original = SourceSelection.Default(Catalog);

        var withAdult = original.With("wdv", true);
        var withoutCore = original.With("grw", false);

        Assert.True(withAdult.IsEnabled("wdv"));
        Assert.False(withoutCore.IsEnabled("grw"));
        Assert.False(original.IsEnabled("wdv"));
        Assert.True(original.IsEnabled("grw"));
    }

    [Fact]
    public void Catalog_RejectsDuplicateIds()
    {
        Assert.Throws<InvalidDataException>(() => new BookCatalog([
            new Book("grw", "A", ["A"]),
            new Book("grw", "B", ["B"]),
        ]));
    }

    [Fact]
    public void ForCharacter_EnablesBooksAddedLater_ExceptAdult()
    {
        var character = new DSACharacterCreator.Core.Characters.Character();
        new SourceSelection(["grw"]).ApplyTo(character, new BookCatalog([new Book("grw", "Regelwerk", ["Regelwerk"])]));

        var selection = SourceSelection.ForCharacter(character, Catalog);

        Assert.True(selection.IsEnabled("grw"));
        Assert.True(selection.IsEnabled("kom"));   // neu hinzugekommen
        Assert.False(selection.IsEnabled("wdv"));  // Ü18 nie automatisch
    }

    [Fact]
    public void ForCharacter_KeepsDeliberatelyDisabledBooksOff()
    {
        var character = new DSACharacterCreator.Core.Characters.Character();
        new SourceSelection(["grw"]).ApplyTo(character, Catalog);

        Assert.False(SourceSelection.ForCharacter(character, Catalog).IsEnabled("kom"));
    }
}

public class SourceSettingsTests : IDisposable
{
    private static readonly BookCatalog Catalog = new([
        new Book("grw", "Regelwerk", ["Regelwerk"]),
        new Book("kom", "Kompendium", ["Kompendium"]),
        new Book("wdv", "Wege der Vereinigungen", ["Wege der Vereinigungen"], Adult: true),
    ]);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dsa-settings-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void NoSettings_UsesDefaultRule()
    {
        var store = new DSACharacterCreator.Core.Persistence.SettingsStore(Path.Combine(_directory, "settings.json"));

        var selection = SourceSelection.ForSettings(store.Load(), Catalog);

        Assert.True(selection.SetEquals(SourceSelection.Default(Catalog)));
    }

    [Fact]
    public void SavedDefault_RoundTrips_NewBooksFollowDefaultRule()
    {
        var store = new DSACharacterCreator.Core.Persistence.SettingsStore(Path.Combine(_directory, "settings.json"));
        var settings = store.Load();
        new SourceSelection(["grw", "wdv"]).ApplyTo(settings, new BookCatalog(Catalog.Books.Where(b => b.Id != "kom")));
        store.Save(settings);

        var selection = SourceSelection.ForSettings(store.Load(), Catalog);

        Assert.True(selection.IsEnabled("wdv"));
        Assert.True(selection.IsEnabled("kom"));   // später hinzugekommen
    }

    [Fact]
    public void UnreadableSettings_FallBackToDefaults()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(path, "{ kaputt");

        Assert.Null(new DSACharacterCreator.Core.Persistence.SettingsStore(path).Load().DefaultBooks);
    }
}
