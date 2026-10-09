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
            Assert.False(string.IsNullOrWhiteSpace(book.WikiName), $"Buch '{book.Id}' ohne Wiki-Namen.");
        }
    }

    [Fact]
    public void WegeDerVereinigungen_IsMarkedAdult()
    {
        Assert.True(Load().Find("wdv")?.Adult);
    }
}
