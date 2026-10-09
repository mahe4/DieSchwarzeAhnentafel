using System.Text.Json;
using System.Text.Json.Serialization;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.Core.Content;

/// <summary>Alle Regelinhalte, geladen aus den JSON-Dateien unter <c>data/</c>.</summary>
public sealed class RuleData
{
    private RuleData(
        BookCatalog books,
        IReadOnlyList<AttributeDefinition> attributes,
        IReadOnlyList<ExperienceLevel> experienceLevels,
        CreationRules creation,
        CostTable costs,
        IReadOnlyList<SkillDefinition> skills,
        IReadOnlyList<CombatTechniqueDefinition> combatTechniques)
    {
        Books = books;
        Attributes = attributes;
        ExperienceLevels = experienceLevels;
        Creation = creation;
        Costs = costs;
        Skills = skills;
        CombatTechniques = combatTechniques;
    }

    public BookCatalog Books { get; }

    public IReadOnlyList<AttributeDefinition> Attributes { get; }

    public IReadOnlyList<ExperienceLevel> ExperienceLevels { get; }

    public CreationRules Creation { get; }

    public CostTable Costs { get; }

    public IReadOnlyList<SkillDefinition> Skills { get; }

    public IReadOnlyList<CombatTechniqueDefinition> CombatTechniques { get; }

    public ExperienceLevel ExperienceLevel(string? id) =>
        ExperienceLevels.FirstOrDefault(l => l.Id == id)
        ?? ExperienceLevels.First(l => l.Id == Creation.DefaultExperienceLevel);

    /// <param name="open">Öffnet eine Datei relativ zu <c>data/</c>.</param>
    /// <param name="paths">Alle vorhandenen Dateien relativ zu <c>data/</c>.</param>
    /// <exception cref="InvalidDataException">Daten fehlen, sind doppelt oder verweisen ins Leere.</exception>
    public static RuleData Load(Func<string, Stream> open, IEnumerable<string> paths)
    {
        var all = paths.ToList();
        var ctx = RuleJsonContext.Default;

        BookCatalog books;
        using (var s = open("books.json")) books = BookCatalog.Load(s);

        var attributes = Read(open, "attributes.json", ctx.ListAttributeDefinition);
        var levels = Read(open, "rules/experience-levels.json", ctx.ListExperienceLevel);
        var creation = Read(open, "rules/creation.json", ctx.CreationRules);
        CostTable costs;
        using (var s = open("rules/improvement-costs.json")) costs = ReadCostTable(s);

        var skills = ReadFolder(open, all, "skills/", ctx.ListSkillDefinition);
        var combatTechniques = ReadFolder(open, all, "combat-techniques/", ctx.ListCombatTechniqueDefinition);

        var data = new RuleData(books, attributes, levels, creation, costs, skills, combatTechniques);
        data.Validate();
        return data;
    }

    private static T Read<T>(Func<string, Stream> open, string path, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type)
    {
        using var stream = open(path);
        try
        {
            return JsonSerializer.Deserialize(stream, type) ?? throw new InvalidDataException($"{path} ist leer.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{path}: {ex.Message}", ex);
        }
    }

    private static List<T> ReadFolder<T>(Func<string, Stream> open, List<string> all, string folder,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<List<T>> type) =>
        all.Where(p => p.StartsWith(folder, StringComparison.Ordinal) && p.EndsWith(".json", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .SelectMany(p => Read(open, p, type))
            .ToList();

    private static CostTable ReadCostTable(Stream stream)
    {
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;
        var activation = root.GetProperty("activation").EnumerateObject()
            .ToDictionary(p => Enum.Parse<ImprovementCost>(p.Name), p => p.Value.GetInt32());
        var rows = root.GetProperty("rows").EnumerateArray().Select(row => new CostRow(
                row.GetProperty("upTo").GetInt32(),
                Enum.GetValues<ImprovementCost>().ToDictionary(c => c, c => row.GetProperty(c.ToString()).GetInt32())))
            .ToList();
        return new CostTable(activation, rows);
    }

    private void Validate()
    {
        var errors = new List<string>();
        var attributeIds = Attributes.Select(a => a.Id).ToHashSet();

        void Unique(IEnumerable<string> ids, string what)
        {
            foreach (var dup in ids.GroupBy(i => i).Where(g => g.Count() > 1))
                errors.Add($"Doppelte {what}-ID '{dup.Key}'.");
        }

        void Sources(string id, IReadOnlyList<Publication> publications)
        {
            if (publications.Count == 0) errors.Add($"'{id}' hat keine Publikation.");
            foreach (var p in publications.Where(p => !Books.Contains(p.Book)))
                errors.Add($"'{id}' verweist auf unbekanntes Buch '{p.Book}'.");
        }

        Unique(Attributes.Select(a => a.Id), "Eigenschafts");
        Unique(ExperienceLevels.Select(l => l.Id), "Erfahrungsgrad");
        Unique(Skills.Select(s => s.Id).Concat(CombatTechniques.Select(c => c.Id)), "Fertigkeits");
        if (ExperienceLevels.All(l => l.Id != Creation.DefaultExperienceLevel))
            errors.Add($"Standard-Erfahrungsgrad '{Creation.DefaultExperienceLevel}' fehlt.");

        foreach (var a in Attributes) Sources(a.Id, a.Publications);
        foreach (var s in Skills)
        {
            Sources(s.Id, s.Publications);
            if (s.Check.Count != 3 || s.Check.Any(a => !attributeIds.Contains(a)))
                errors.Add($"'{s.Id}' hat eine ungültige Probe.");
            if (s.ImprovementCost == ImprovementCost.E) errors.Add($"'{s.Id}' darf nicht Spalte E haben.");
        }
        foreach (var c in CombatTechniques)
        {
            Sources(c.Id, c.Publications);
            if (c.PrimaryAttributes.Count == 0 || c.PrimaryAttributes.Append(c.AttackAttribute).Any(a => !attributeIds.Contains(a)))
                errors.Add($"'{c.Id}' hat ungültige Leiteigenschaften.");
        }

        if (errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(List<AttributeDefinition>))]
[JsonSerializable(typeof(List<ExperienceLevel>))]
[JsonSerializable(typeof(CreationRules))]
[JsonSerializable(typeof(List<SkillDefinition>))]
[JsonSerializable(typeof(List<CombatTechniqueDefinition>))]
internal partial class RuleJsonContext : JsonSerializerContext;
