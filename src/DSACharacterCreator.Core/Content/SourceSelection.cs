using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Persistence;

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

    /// <summary>
    /// Auswahl eines Helden: seine aktivierten Bücher plus alle Bücher, die er noch nicht kannte und die nach der
    /// Standardregel aktiv wären (z. B. nach einem Daten-Update neu hinzugekommene Publikationen).
    /// </summary>
    public static SourceSelection ForCharacter(Character character, BookCatalog catalog) =>
        Stored(character.EnabledBooks, character.KnownBooks, catalog);

    /// <summary>Standardauswahl aus den Einstellungen (neue Bücher wie bei <see cref="ForCharacter"/>).</summary>
    public static SourceSelection ForSettings(AppSettings settings, BookCatalog catalog) =>
        settings.DefaultBooks is { } books ? Stored(books, settings.KnownBooks, catalog) : Default(catalog);

    /// <summary>Nur das Regelwerk (und Inhalte des Wikis ohne Quellenangabe).</summary>
    public static SourceSelection CoreRules(BookCatalog catalog) =>
        new(new[] { "grw", "wiki" }.Where(catalog.Contains));

    /// <summary>Alle Publikationen einschließlich Ü18.</summary>
    public static SourceSelection All(BookCatalog catalog) => new(catalog.Books.Select(b => b.Id));

    private static SourceSelection Stored(IEnumerable<string> enabled, IReadOnlyCollection<string> known, BookCatalog catalog) =>
        new(enabled.Where(catalog.Contains).Concat(catalog.Books
            .Where(b => !b.Adult && !known.Contains(b.Id))
            .Select(b => b.Id)));

    /// <summary>Speichert die Auswahl am Helden und merkt sich alle derzeit bekannten Bücher.</summary>
    public void ApplyTo(Character character, BookCatalog catalog)
    {
        character.EnabledBooks = _enabled.Order(StringComparer.Ordinal).ToList();
        character.KnownBooks = catalog.Books.Select(b => b.Id).ToList();
    }

    /// <summary>Speichert die Auswahl als Standard für neue Helden.</summary>
    public void ApplyTo(AppSettings settings, BookCatalog catalog)
    {
        settings.DefaultBooks = _enabled.Order(StringComparer.Ordinal).ToList();
        settings.KnownBooks = catalog.Books.Select(b => b.Id).ToList();
    }

    public bool SetEquals(SourceSelection other) => _enabled.SetEquals(other._enabled);

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
