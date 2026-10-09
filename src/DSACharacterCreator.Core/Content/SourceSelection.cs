namespace DSACharacterCreator.Core.Content;

/// <summary>Die für einen Charakter aktivierten Publikationen.</summary>
public sealed class SourceSelection
{
    private readonly HashSet<string> _enabled;

    public SourceSelection(IEnumerable<string> enabledBookIds)
    {
        _enabled = new HashSet<string>(enabledBookIds, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<string> EnabledBookIds => _enabled;

    /// <summary>Standard für neue Charaktere: alle Publikationen außer Ü18-Werken.</summary>
    public static SourceSelection Default(BookCatalog catalog) =>
        new(catalog.Books.Where(b => !b.Adult).Select(b => b.Id));

    public bool IsEnabled(string bookId) => _enabled.Contains(bookId);

    /// <summary>Ein Inhalt ist verfügbar, sobald mindestens eine seiner Publikationen aktiv ist.</summary>
    public bool IsAvailable(IEnumerable<Publication> publications) =>
        publications.Any(p => _enabled.Contains(p.Book));

    public SourceSelection With(string bookId, bool enabled)
    {
        var ids = new HashSet<string>(_enabled, StringComparer.Ordinal);
        if (enabled) ids.Add(bookId);
        else ids.Remove(bookId);
        return new SourceSelection(ids);
    }
}
