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
        IReadOnlyList<CombatTechniqueDefinition> combatTechniques,
        IReadOnlyList<SpeciesDefinition> species,
        IReadOnlyList<CultureDefinition> cultures,
        IReadOnlyList<ProfessionDefinition> professions,
        IReadOnlyList<TraitDefinition> advantages,
        IReadOnlyList<TraitDefinition> disadvantages)
    {
        Advantages = advantages;
        Disadvantages = disadvantages;
        Species = species;
        Cultures = cultures;
        Professions = professions;
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

    public IReadOnlyList<SpeciesDefinition> Species { get; }

    public IReadOnlyList<CultureDefinition> Cultures { get; }

    public IReadOnlyList<ProfessionDefinition> Professions { get; }

    public IReadOnlyList<TraitDefinition> Advantages { get; }

    public IReadOnlyList<TraitDefinition> Disadvantages { get; }

    public TraitDefinition? FindTrait(string? id) =>
        id is null ? null : Advantages.FirstOrDefault(t => t.Id == id) ?? Disadvantages.FirstOrDefault(t => t.Id == id);

    public SpeciesDefinition? FindSpecies(string? id) => id is null ? null : Species.FirstOrDefault(s => s.Id == id);

    public CultureDefinition? FindCulture(string? id) => id is null ? null : Cultures.FirstOrDefault(c => c.Id == id);

    public ProfessionDefinition? FindProfession(string? id) =>
        id is null ? null : Professions.FirstOrDefault(p => p.Id == id);

    /// <summary>
    /// Profession mit den Werten für den Erfahrungsgrad des Helden. Gibt es keinen Wertesatz für genau diesen Grad,
    /// wird der nächstliegende genommen.
    /// </summary>
    public ProfessionDefinition ProfessionForLevel(ProfessionDefinition profession, string levelId)
    {
        if (profession.ByExperienceLevel is not { Count: > 0 } sets) return profession;
        int Order(string id) => ExperienceLevels.ToList().FindIndex(l => l.Id == id || l.Name.Equals(id, StringComparison.OrdinalIgnoreCase));
        var target = Order(levelId);
        var set = sets.MinBy(s => Math.Abs(Order(s.Level) - target))!;
        return profession with
        {
            WikiAp = set.WikiAp,
            Skills = set.Skills,
            CombatTechniques = set.CombatTechniques,
            CombatTechniqueChoices = set.CombatTechniqueChoices,
            AttributeRequirements = set.AttributeRequirements,
        };
    }

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

        var species = ReadFolder(open, all, "species/", ctx.ListSpeciesDefinition);
        var cultures = ReadFolder(open, all, "cultures/", ctx.ListCultureDefinition);
        var professions = ReadFolder(open, all, "professions/", ctx.ListProfessionDefinition);

        var advantages = ReadFolder(open, all, "advantages/", ctx.ListTraitDefinition);
        var disadvantages = ReadFolder(open, all, "disadvantages/", ctx.ListTraitDefinition)
            .Select(d => d with { IsDisadvantage = true }).ToList();

        var data = new RuleData(books, attributes, levels, creation, costs, skills, combatTechniques,
            species, cultures, professions, advantages, disadvantages);
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

        var skillIds = Skills.Select(s => s.Id).ToHashSet();
        var ctIds = CombatTechniques.Select(c => c.Id).ToHashSet();
        void Skill(string owner, string id)
        {
            if (!skillIds.Contains(id)) errors.Add($"'{owner}' verweist auf unbekanntes Talent '{id}'.");
        }
        void Combat(string owner, string id)
        {
            if (!ctIds.Contains(id)) errors.Add($"'{owner}' verweist auf unbekannte Kampftechnik '{id}'.");
        }

        Unique(Species.Select(s => s.Id), "Spezies");
        Unique(Cultures.Select(c => c.Id), "Kultur");
        Unique(Professions.Select(p => p.Id), "Professions");
        foreach (var s in Species)
        {
            Sources(s.Id, s.Publications);
            foreach (var a in s.AttributeChanges.SelectMany(c => c.Options).Where(a => !attributeIds.Contains(a)))
                errors.Add($"'{s.Id}' ändert unbekannte Eigenschaft '{a}'.");
        }
        foreach (var c in Cultures)
        {
            Sources(c.Id, c.Publications);
            foreach (var v in c.Package?.Skills ?? []) Skill(c.Id, v.Skill);
        }
        foreach (var p in Professions)
        {
            Sources(p.Id, p.Publications);
            foreach (var v in p.Skills) Skill(p.Id, v.Skill);
            foreach (var v in p.CombatTechniques) Combat(p.Id, v.CombatTechnique);
            foreach (var o in p.CombatTechniqueChoices?.SelectMany(c => c.Options) ?? []) Combat(p.Id, o);
            foreach (var a in p.AttributeRequirements?.Keys ?? [])
                if (!attributeIds.Contains(a)) errors.Add($"'{p.Id}' setzt unbekannte Eigenschaft '{a}' voraus.");
            foreach (var variant in p.Variants ?? [])
            {
                foreach (var v in variant.Skills) Skill(p.Id, v.Skill);
                foreach (var v in variant.CombatTechniques) Combat(p.Id, v.CombatTechnique);
            }
            Unique((p.Variants ?? []).Select(v => p.Id + "/" + v.Id), "Varianten");
            foreach (var set in p.ByExperienceLevel ?? [])
            {
                if (ExperienceLevels.All(l => !l.Name.Equals(set.Level, StringComparison.OrdinalIgnoreCase) && l.Id != set.Level))
                    errors.Add($"'{p.Id}': unbekannter Erfahrungsgrad '{set.Level}'.");
                foreach (var v in set.Skills) Skill(p.Id, v.Skill);
                foreach (var v in set.CombatTechniques) Combat(p.Id, v.CombatTechnique);
                foreach (var o in set.CombatTechniqueChoices?.SelectMany(c => c.Options) ?? []) Combat(p.Id, o);
            }
        }

        Unique(Advantages.Concat(Disadvantages).Select(t => t.Id), "Vor-/Nachteils");
        foreach (var t in Advantages.Concat(Disadvantages))
        {
            Sources(t.Id, t.Publications);
            var c = t.Cost;
            var ok = c.Type switch
            {
                TraitCostType.Fixed or TraitCostType.PerLevel => c.Ap is not null,
                TraitCostType.Levels => c.Levels is { Count: > 0 },
                TraitCostType.BySkillFactor => c.ByFactor is { Count: > 0 } && c.Target is not null,
                TraitCostType.ByOption => c.Options is { Count: > 1 },
                _ => true,
            };
            if (!ok) errors.Add($"'{t.Id}' hat unvollständige Kosten ({c.Type}).");
            if (c.Type == TraitCostType.PerLevel && t.MaxLevel is null) errors.Add($"'{t.Id}': Kosten pro Stufe ohne Stufen.");
            var sign = t.IsDisadvantage ? -1 : 1;
            if ((c.Ap ?? 0) * sign < 0) errors.Add($"'{t.Id}': AP-Wert mit falschem Vorzeichen.");
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
[JsonSerializable(typeof(List<SpeciesDefinition>))]
[JsonSerializable(typeof(List<CultureDefinition>))]
[JsonSerializable(typeof(List<ProfessionDefinition>))]
[JsonSerializable(typeof(List<TraitDefinition>))]
internal partial class RuleJsonContext : JsonSerializerContext;
