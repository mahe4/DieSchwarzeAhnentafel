using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.Core.Content;

/// <param name="Entries">Einträge der Regeldaten, die diese Publikation nennen.</param>
/// <param name="Exclusive">Davon nur in dieser Publikation (fallen beim Abwählen sicher weg).</param>
public sealed record BookUsage(Book Book, int Entries, int Exclusive);

/// <summary>Was in welcher Publikation steht und welche Entscheidungen eines Helden an einer Publikation hängen.</summary>
public static class SourceOverview
{
    /// <summary>Alle Bücher des Katalogs mit der Zahl ihrer Einträge (Reihenfolge des Katalogs).</summary>
    public static IReadOnlyList<BookUsage> Books(RuleData rules)
    {
        var entries = new Dictionary<string, int>(StringComparer.Ordinal);
        var exclusive = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var publications in Content(rules))
        {
            var books = publications.Select(p => p.Book).Distinct().ToList();
            foreach (var book in books) entries[book] = entries.GetValueOrDefault(book) + 1;
            if (books.Count == 1) exclusive[books[0]] = exclusive.GetValueOrDefault(books[0]) + 1;
        }
        return rules.Books.Books
            .Select(b => new BookUsage(b, entries.GetValueOrDefault(b.Id), exclusive.GetValueOrDefault(b.Id)))
            .ToList();
    }

    private static IEnumerable<IReadOnlyList<Publication>> Content(RuleData rules) =>
        rules.Attributes.Select(x => x.Publications)
            .Concat(rules.Skills.Select(x => x.Publications))
            .Concat(rules.CombatTechniques.Select(x => x.Publications))
            .Concat(rules.Species.Select(x => x.Publications))
            .Concat(rules.Cultures.Select(x => x.Publications))
            .Concat(rules.Professions.Select(x => x.Publications))
            .Concat(rules.Advantages.Select(x => x.Publications))
            .Concat(rules.Disadvantages.Select(x => x.Publications))
            .Concat(rules.SpecialAbilities.Select(x => x.Publications))
            .Concat(rules.Languages.Select(x => x.Publications))
            .Concat(rules.Scripts.Select(x => x.Publications))
            .Concat(rules.Spells.Select(x => x.Publications))
            .Concat(rules.Weapons.Select(x => x.Publications))
            .Concat(rules.Armors.Select(x => x.Publications))
            .Concat(rules.Items.Select(x => x.Publications))
            .Concat(rules.EquipmentPackages.Select(x => x.Publications));

    /// <summary>
    /// Gewählte Inhalte des Helden, die an einer Publikation hängen – je Buch-ID die Namen: bei aktiven Büchern die
    /// Inhalte, die nur noch über dieses Buch verfügbar sind (Abwählen ergibt eine Warnung, keine Sperre); bei
    /// inaktiven die Inhalte, die gerade aus keiner aktiven Quelle stammen und mit diesem Buch wieder verfügbar wären.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> DependentChoices(CharacterState state,
        SourceSelection sources)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (name, publications) in Choices(state))
        {
            var active = publications.Select(p => p.Book).Where(sources.IsEnabled).Distinct().ToList();
            var books = active.Count switch
            {
                0 => publications.Select(p => p.Book).Distinct().ToList(),
                1 => active,
                _ => [],
            };
            foreach (var book in books)
            {
                if (!result.TryGetValue(book, out var names)) result[book] = names = [];
                if (!names.Contains(name)) names.Add(name);
            }
        }
        return result.ToDictionary(p => p.Key, IReadOnlyList<string> (p) => p.Value, StringComparer.Ordinal);
    }

    private static IEnumerable<(string Name, IReadOnlyList<Publication> Publications)> Choices(CharacterState state)
    {
        var origin = state.Origin;
        if (origin.Species is { } species) yield return (species.Name, species.Publications);
        if (origin.Culture is { } culture) yield return (culture.Name, culture.Publications);
        if (origin.Profession is { } profession) yield return (profession.Name, profession.Publications);
        foreach (var t in state.Traits.Where(t => !t.Automatic)) yield return (t.Definition.Name, t.Definition.Publications);
        foreach (var t in state.SpecialAbilities) yield return (t.Definition.Name, t.Definition.Publications);
        foreach (var s in state.Spells) yield return (s.Definition.Name, s.Definition.Publications);
        foreach (var line in state.Equipment.Lines)
            if (line.Definition is { } item) yield return (item.Name, item.Publications);
    }
}
