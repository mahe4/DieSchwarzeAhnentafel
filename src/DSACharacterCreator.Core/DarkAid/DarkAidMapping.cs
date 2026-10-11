using System.Text.Json;

namespace DSACharacterCreator.Core.DarkAid;

/// <summary>Spezies der App und Würfeltabelle fürs Aussehen zu einer Dark-Aid-Rasse (Mittelländer, Tulamiden …).</summary>
public sealed record DarkAidRace(string Species, string? Table);

/// <summary>Profession der App zu einer Dark-Aid-Profession; eine gewählte Variante hat in Dark Aid eine eigene ID.</summary>
public sealed record DarkAidProfession(string Id, string? Variant);

/// <summary>Vorteil, Nachteil oder Sonderfertigkeit der App zu einem Dark-Aid-Eintrag.</summary>
/// <param name="Option">Dark Aid führt die Auswahl als eigenen Eintrag („Schnellladen (Bögen)“): Auswahl in der App.</param>
/// <param name="Text">Ebenso, aber in der App eine Präzisierung („Klinge wider Geister“).</param>
public sealed record DarkAidActivatable(string Id, string? Option, string? Text);

/// <summary>
/// Zuordnung Dark-Aid-ID → App-ID (<c>data/darkaid/mapping.json</c>, erzeugt von <c>tools/darkaid/build_mapping.py</c> aus
/// den Daten von The Dark Aid 15/16). Enthält nur IDs und Namen, keine Regeltexte.
/// </summary>
public sealed class DarkAidMapping
{
    public const string ResourcePath = "darkaid/mapping.json";

    public required IReadOnlyDictionary<string, string> Attributes { get; init; }
    public required IReadOnlyDictionary<string, string> ExperienceLevels { get; init; }
    public required IReadOnlyDictionary<string, DarkAidRace> Races { get; init; }
    public required IReadOnlyDictionary<string, string> Cultures { get; init; }
    public required IReadOnlyDictionary<string, DarkAidProfession> Professions { get; init; }
    public required IReadOnlyDictionary<string, string> Skills { get; init; }
    public required IReadOnlyDictionary<string, string> CombatTechniques { get; init; }

    /// <summary>Zauber, Rituale, Zaubertricks und magische Handlungen (<c>spells</c>) sowie Liturgien, Zeremonien und
    /// Segnungen (<c>chants</c>).</summary>
    public required IReadOnlyDictionary<string, string> Spells { get; init; }

    public required IReadOnlyDictionary<string, string> Languages { get; init; }
    public required IReadOnlyDictionary<string, string> Scripts { get; init; }

    /// <summary>Magische und karmale Tradition (Auswahl der SF „Tradition“) → SF „Tradition (…)“ der App.</summary>
    public required IReadOnlyDictionary<string, string> Traditions { get; init; }

    public required IReadOnlyDictionary<string, DarkAidActivatable> Activatables { get; init; }

    /// <summary>Art der Auswahl eines Eintrags („string“, „stringwithcost“, „ruleelement:language“ …).</summary>
    public required IReadOnlyDictionary<string, string> Variants { get; init; }

    /// <summary>Einträge, die Dark Aid unter <c>enchantments</c> führt (Stab-, Kugel-, Gewandzauber …).</summary>
    public required IReadOnlySet<string> Enchantments { get; init; }

    public required IReadOnlySet<string> BlessedTraditions { get; init; }

    /// <summary>Namen weiterer Auswahlen je Typ (god, poison, spellproperty, tradesecret …) für die Präzisierung.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Names { get; init; }

    /// <summary>Dark-Aid-Zauber → Dark-Aid-Erweiterung → Erweiterung der App.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Enhancements { get; init; }

    /// <summary>„meleeweapon/langschwert“ (Typ/ID) → Gegenstand der App.</summary>
    public required IReadOnlyDictionary<string, string> Items { get; init; }

    public required IReadOnlyDictionary<string, string> Rulebooks { get; init; }

    /// <summary>Namen der Dark-Aid-Einträge ohne Gegenstück in der App (für den Importbericht).</summary>
    public required IReadOnlyDictionary<string, string> Unmapped { get; init; }

    public static DarkAidMapping Load(Stream stream)
    {
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        Dictionary<string, string> Strings(string name) =>
            root.GetProperty(name).EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);

        Dictionary<string, T> Objects<T>(string name, Func<JsonElement, T> read) =>
            root.GetProperty(name).EnumerateObject().ToDictionary(p => p.Name, p => read(p.Value));

        HashSet<string> Set(string name) =>
            root.GetProperty(name).EnumerateArray().Select(e => e.GetString()!).ToHashSet(StringComparer.Ordinal);

        static string? Text(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? v.GetString() : null;

        static IReadOnlyDictionary<string, string> Nested(JsonElement e) =>
            e.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);

        return new DarkAidMapping
        {
            Attributes = Strings("attributes"),
            ExperienceLevels = Strings("experienceLevels"),
            Races = Objects("races", e => new DarkAidRace(Text(e, "species")!, Text(e, "table"))),
            Cultures = Strings("cultures"),
            Professions = Objects("professions", e => new DarkAidProfession(Text(e, "id")!, Text(e, "variant"))),
            Skills = Strings("skills"),
            CombatTechniques = Strings("combatTechniques"),
            Spells = Strings("spells"),
            Languages = Strings("languages"),
            Scripts = Strings("scripts"),
            Traditions = Strings("traditions"),
            Activatables = Objects("activatables", e => e.ValueKind == JsonValueKind.String
                ? new DarkAidActivatable(e.GetString()!, null, null)
                : new DarkAidActivatable(Text(e, "id")!, Text(e, "option"), Text(e, "text"))),
            Variants = Strings("variants"),
            Enchantments = Set("enchantments"),
            BlessedTraditions = Set("blessedTraditions"),
            Names = Objects("names", Nested),
            Enhancements = Objects("enhancements", Nested),
            Items = Strings("items"),
            Rulebooks = Strings("rulebooks"),
            Unmapped = Strings("unmapped"),
        };
    }
}
