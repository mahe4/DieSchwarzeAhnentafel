using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.Core.Sheet;

/// <summary>
/// Regeltexte der gewählten Zauber, Rituale, Liturgien usw. aus dem Regel-Wiki, für einen eigenen Ausdruck neben dem
/// Charakterbogen. Reine Funktion über Held, Berechnung und Regeldaten.
/// </summary>
public sealed record SpellTextSheet(string Name, IReadOnlyList<SheetValue> Attributes, IReadOnlyList<SheetGroup<SpellText>> Groups)
{
    public bool IsEmpty => Groups.Count == 0;

    public static SpellTextSheet Create(Character character, CharacterState state, RuleData rules)
    {
        var groups = SheetBuilder.SpellGroupTitles
            .Select(g => new SheetGroup<SpellText>(g.Title, state.Spells
                .Where(s => s.Definition.Group == g.Group)
                .OrderBy(s => s.Definition.Name, StringComparer.CurrentCulture)
                .Select(s => Text(s, state, rules)).ToList()))
            .Where(g => g.Items.Count > 0)
            .ToList();
        return new SpellTextSheet(string.IsNullOrWhiteSpace(character.Name) ? "Unbenannter Held" : character.Name,
            state.Attributes.Select(a => new SheetValue(a.Definition.Id, a.Definition.Name, a.Value.ToString())).ToList(), groups);
    }

    private static SpellText Text(SpellState s, CharacterState state, RuleData rules)
    {
        var d = s.Definition;
        var fields = new List<SheetField>();
        if (d.Check is { } check)
            fields.Add(new("Probe", $"{string.Join("/", check)} ({string.Join("/", check.Select(state.Attribute))})"));
        else if (d.CheckText is { Length: > 0 } checkText) fields.Add(new("Probe", checkText));
        // Kosten, Dauer, Reichweite, Wirkungsdauer, Zielkategorie … in der Reihenfolge des Wikis
        foreach (var line in (d.DetailsText ?? "").Split('\n'))
        {
            var parts = line.Split(':', 2);
            if (parts.Length == 2 && parts[1].Trim().Length > 0) fields.Add(new(parts[0].Trim(), parts[1].Trim()));
        }
        if (d.Property is { } property) fields.Add(new("Merkmal", property));
        if (d.Aspect is { } aspect) fields.Add(new("Aspekt", aspect));
        if (d.TraditionsText is { Length: > 0 } traditions) fields.Add(new("Verbreitung", traditions));
        var summary = new List<string> { d.Category };
        if (s.Value is { } value) summary.Add($"FW {value}");
        if (d.FactorFor(s.Entry.Option) is { } factor) summary.Add($"Sf. {factor}");
        var publications = string.Join(", ", d.Publications.Select(p =>
            (rules.Books.Find(p.Book)?.Name ?? p.Book) + (p.Page is { } page ? $" S. {page}" : "")));
        return new SpellText(d.Name + (s.Entry.Option is { } o ? $" ({o})" : ""), string.Join(" · ", summary), fields,
            d.RuleText, string.IsNullOrWhiteSpace(d.Note) ? null : d.Note, publications.Length > 0 ? publications : null);
    }
}

/// <param name="Summary">„Zaubersprüche · FW 6 · Sf. C“.</param>
/// <param name="Fields">Probe, Kosten, Dauer, Reichweite … wie im Wiki.</param>
/// <param name="RuleText">Wirkung laut Wiki.</param>
public sealed record SpellText(string Name, string Summary, IReadOnlyList<SheetField> Fields, string RuleText, string? Note,
    string? Publications);
