using System.Text.Json;
using System.Text.Json.Serialization;

namespace DSACharacterCreator.Core.Content;

/// <summary>Alle bekannten Publikationen (Inhalt von <c>data/books.json</c>).</summary>
public sealed class BookCatalog
{
    private readonly Dictionary<string, Book> _byId;

    public BookCatalog(IEnumerable<Book> books)
    {
        Books = books.ToList();
        _byId = new Dictionary<string, Book>(StringComparer.Ordinal);
        foreach (var book in Books)
        {
            if (!_byId.TryAdd(book.Id, book))
                throw new InvalidDataException($"Doppelte Buch-ID '{book.Id}'.");
        }
    }

    public IReadOnlyList<Book> Books { get; }

    public Book? Find(string id) => _byId.GetValueOrDefault(id);

    public bool Contains(string id) => _byId.ContainsKey(id);

    public static BookCatalog Load(Stream json)
    {
        var books = JsonSerializer.Deserialize(json, ContentJsonContext.Default.ListBook)
                    ?? throw new InvalidDataException("books.json ist leer.");
        return new BookCatalog(books);
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(List<Book>))]
internal partial class ContentJsonContext : JsonSerializerContext;
