using System.Text.Json;

namespace DSACharacterCreator.Core.Optolith;

/// <summary>Profession der App zu einer Optolith-Profession; manche Optolith-Professionen sind bei uns Varianten.</summary>
public sealed record OptolithProfession(string Id, string? Variant);

/// <summary>Variante der App zu einer Optolith-Professionsvariante.</summary>
public sealed record OptolithVariant(string Profession, string Variant);

/// <summary>Ausprägung einer Optolith-Auswahl (<c>sid</c> als Zahl).</summary>
/// <param name="Option">Auswahl in der App (Sprache, Schrift, Merkmal, Ausprägung mit eigenem AP-Wert …).</param>
/// <param name="Text">Ohne Gegenstück in der App: Name als Präzisierung (freier Text).</param>
/// <param name="Ap">AP-Wert laut Optolith, wenn der Eintrag in der App einen selbst eingetragenen Wert braucht.</param>
public sealed record OptolithOption(string? Option, string? Text, int? Ap);

/// <summary>Vorteil, Nachteil oder Sonderfertigkeit (<c>ADV_</c>, <c>DISADV_</c>, <c>SA_</c>).</summary>
/// <param name="Ap">AP-Wert laut Optolith, wenn der Eintrag in der App einen selbst eingetragenen Wert braucht.</param>
/// <param name="Input">Optolith erlaubt eine freie Eingabe (<c>sid</c> als Text).</param>
public sealed record OptolithActivatable(string Id, IReadOnlyDictionary<string, OptolithOption> Options, int? Ap, bool Input);

/// <summary>Zauber- bzw. Liturgieerweiterung der App (Zauber und Erweiterung) zu einer Optolith-Auswahl.</summary>
public sealed record OptolithEnhancement(string Spell, string Id);

/// <summary>Gegenstand der App zu einer Optolith-Vorlage (<c>ITEMTPL_</c>) mit Optolith-Gruppe.</summary>
public sealed record OptolithItem(string Id, int Group);

/// <summary>
/// Zuordnung Optolith-ID → App-ID (<c>data/optolith/mapping.json</c>, erzeugt von <c>tools/optolith/build_mapping.py</c>
/// aus den Daten von Optolith 1.5.1). Enthält nur IDs und Namen, keine Regeltexte.
/// </summary>
public sealed class OptolithMapping
{
    public const string ResourcePath = "optolith/mapping.json";

    public required IReadOnlyDictionary<string, string> Attributes { get; init; }
    public required IReadOnlyDictionary<string, string> ExperienceLevels { get; init; }
    public required IReadOnlyDictionary<string, string> Species { get; init; }

    /// <summary>Spezies-Variante → Name der Würfeltabelle fürs Aussehen (Mittelländer …).</summary>
    public required IReadOnlyDictionary<string, string> SpeciesVariants { get; init; }

    public required IReadOnlyDictionary<string, string> Cultures { get; init; }
    public required IReadOnlyDictionary<string, OptolithProfession> Professions { get; init; }
    public required IReadOnlyDictionary<string, OptolithVariant> ProfessionVariants { get; init; }
    public required IReadOnlyDictionary<string, string> Skills { get; init; }

    /// <summary>Talent → Nummer des Anwendungsgebiets → Name (Fertigkeitsspezialisierung).</summary>
    public required IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> SkillApplications { get; init; }

    public required IReadOnlyDictionary<string, string> CombatTechniques { get; init; }

    /// <summary>Zauber, Rituale, Liturgien, Zeremonien, Zaubertricks und Segnungen.</summary>
    public required IReadOnlyDictionary<string, string> Spells { get; init; }

    public required IReadOnlyDictionary<string, OptolithActivatable> Activatables { get; init; }

    /// <summary>„SA_414“ (Zaubererweiterung) bzw. „SA_663“ (Liturgieerweiterung) → Optolith-Nummer → Erweiterung.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyDictionary<string, OptolithEnhancement>> Enhancements { get; init; }
    public required IReadOnlyDictionary<string, OptolithItem> Items { get; init; }
    public required IReadOnlyDictionary<string, string> ItemGroups { get; init; }
    public required IReadOnlyDictionary<string, string> HairColors { get; init; }
    public required IReadOnlyDictionary<string, string> EyeColors { get; init; }
    public required IReadOnlyDictionary<string, string> SocialStatus { get; init; }

    /// <summary>Namen der Optolith-Einträge ohne Gegenstück in der App (für den Importbericht).</summary>
    public required IReadOnlyDictionary<string, string> Unmapped { get; init; }

    public static OptolithMapping Load(Stream stream)
    {
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        Dictionary<string, string> Strings(string name) =>
            root.GetProperty(name).EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);

        Dictionary<string, T> Objects<T>(string name, Func<JsonElement, T> read) =>
            root.GetProperty(name).EnumerateObject().ToDictionary(p => p.Name, p => read(p.Value));

        static string? Text(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? v.GetString() : null;
        static int? Number(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? v.GetInt32() : null;

        return new OptolithMapping
        {
            Attributes = Strings("attributes"),
            ExperienceLevels = Strings("experienceLevels"),
            Species = Strings("species"),
            SpeciesVariants = Strings("speciesVariants"),
            Cultures = Strings("cultures"),
            Professions = Objects("professions", e => new OptolithProfession(Text(e, "id")!, Text(e, "variant"))),
            ProfessionVariants = Objects("professionVariants",
                e => new OptolithVariant(Text(e, "profession")!, Text(e, "variant")!)),
            Skills = Strings("skills"),
            SkillApplications = Objects<IReadOnlyDictionary<string, string>>("skillApplications",
                e => e.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!)),
            CombatTechniques = Strings("combatTechniques"),
            Spells = Strings("spells"),
            Activatables = Objects("activatables", e => new OptolithActivatable(
                Text(e, "id")!,
                e.TryGetProperty("options", out var options)
                    ? options.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ValueKind == JsonValueKind.String
                        ? new OptolithOption(p.Value.GetString(), null, null)
                        : new OptolithOption(null, Text(p.Value, "text"), Number(p.Value, "ap")))
                    : new Dictionary<string, OptolithOption>(),
                Number(e, "ap"),
                e.TryGetProperty("input", out var input) && input.GetBoolean())),
            Enhancements = Objects<IReadOnlyDictionary<string, OptolithEnhancement>>("enhancements",
                e => e.EnumerateObject().ToDictionary(p => p.Name, p => new OptolithEnhancement(Text(p.Value, "spell")!, Text(p.Value, "id")!))),
            Items = Objects("items", e => new OptolithItem(Text(e, "id")!, Number(e, "gr") ?? 0)),
            ItemGroups = Strings("itemGroups"),
            HairColors = Strings("hairColors"),
            EyeColors = Strings("eyeColors"),
            SocialStatus = Strings("socialStatus"),
            Unmapped = Strings("unmapped"),
        };
    }

    /// <summary>Umkehrung einer Zuordnung (App-ID → erste Optolith-ID).</summary>
    internal static Dictionary<string, string> Reverse(IEnumerable<KeyValuePair<string, string>> map)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (optolith, ours) in map.OrderBy(p => OrderKey(p.Key))) result.TryAdd(ours, optolith);
        return result;
    }

    /// <summary>„SA_29“ vor „SA_100“: kleinere (ältere, meist Grundregel-) IDs zuerst.</summary>
    internal static (string, int) OrderKey(string id)
    {
        var cut = id.LastIndexOf('_');
        return cut > 0 && int.TryParse(id[(cut + 1)..], out var n) ? (id[..cut], n) : (id, 0);
    }
}
