using DSACharacterCreator.Content;
using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Content.Tests;

public class BookCatalogDataTests
{
    private static BookCatalog Load()
    {
        using var stream = EmbeddedContent.Open("books.json");
        return BookCatalog.Load(stream);
    }

    [Fact]
    public void BooksJson_IsEmbedded()
    {
        Assert.Contains("books.json", EmbeddedContent.List());
    }

    [Fact]
    public void BooksJson_LoadsAndContainsCoreRules()
    {
        var catalog = Load();

        Assert.NotNull(catalog.Find("grw"));
    }

    [Fact]
    public void Books_HaveRequiredFields()
    {
        foreach (var book in Load().Books)
        {
            Assert.False(string.IsNullOrWhiteSpace(book.Id));
            Assert.False(string.IsNullOrWhiteSpace(book.Name), $"Buch '{book.Id}' ohne Namen.");
            Assert.NotEmpty(book.WikiNames);
        }
    }

    [Fact]
    public void WegeDerVereinigungen_IsMarkedAdult()
    {
        Assert.True(Load().Find("wdv")?.Adult);
    }
}

public class RuleDataTests
{
    private static readonly RuleData Data = RuleData.Load(EmbeddedContent.Open, EmbeddedContent.List());

    [Fact]
    public void LoadsAndValidates()
    {
        Assert.Equal(8, Data.Attributes.Count);
        Assert.Equal(7, Data.ExperienceLevels.Count);
        Assert.Equal(59, Data.Skills.Count);
        Assert.Equal(21, Data.CombatTechniques.Count);
    }

    [Fact]
    public void EveryDataFile_IsWellFormedJson()
    {
        foreach (var path in EmbeddedContent.List())
        {
            using var stream = EmbeddedContent.Open(path);
            using var doc = System.Text.Json.JsonDocument.Parse(stream);
        }
    }

    [Fact]
    public void CoreRulebook_HasFourteenCombatTechniques()
    {
        var core = new SourceSelection(["grw"]);
        Assert.Equal(14, Data.CombatTechniques.Count(c => core.IsAvailable(c.Publications)));
        Assert.Equal(59, Data.Skills.Count(s => core.IsAvailable(s.Publications)));
    }
}
