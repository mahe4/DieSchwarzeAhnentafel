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
        IReadOnlyList<TraitDefinition> disadvantages,
        IReadOnlyList<TraitDefinition> specialAbilities,
        IReadOnlyList<LanguageDefinition> languages,
        IReadOnlyList<ScriptDefinition> scripts)
    {
        SpecialAbilities = specialAbilities;
        Languages = languages;
        Scripts = scripts;
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

    public IReadOnlyList<TraitDefinition> SpecialAbilities { get; }

    public IReadOnlyList<LanguageDefinition> Languages { get; }

    public IReadOnlyList<ScriptDefinition> Scripts { get; }

    /// <summary>Zauber, Rituale, Zaubertricks, magische Handlungen, Liturgien, Zeremonien, Segnungen.</summary>
    public IReadOnlyList<SpellDefinition> Spells { get; private init; } = [];

    public IReadOnlyList<WeaponDefinition> Weapons { get; private init; } = [];

    public IReadOnlyList<ArmorDefinition> Armors { get; private init; } = [];

    /// <summary>Würfeltabellen für das Aussehen je Spezies.</summary>
    public IReadOnlyList<SpeciesAppearance> Appearances { get; private init; } = [];

    public StartAgeTable StartAges { get; private init; } = new([], []);

    public BirthdateTable Birthdates { get; private init; } = new([], []);

    /// <summary>Tierarten aus dem Bestiarium (Tiergefährten).</summary>
    public IReadOnlyList<AnimalDefinition> Animals { get; private init; } = [];

    /// <summary>Tricks, Ausbildungsaufsätze, Vertrautentricks, Tier-SF und Vor-/Nachteile für Tiere.</summary>
    public IReadOnlyList<AnimalAbilityDefinition> AnimalAbilities { get; private init; } = [];

    /// <summary>Vertraute: Tierarten je Tradition, Boni, Besonderheiten der Vertrautentricks.</summary>
    public FamiliarRules Familiars { get; private init; } = new([], new Dictionary<string, string>(), new(0, 0, 0, 0, 0, 0), [], []);

    public AnimalDefinition? FindAnimal(string? id) => Lookup(ref _animals, () => Animals, a => a.Id, id);

    public AnimalAbilityDefinition? FindAnimalAbility(string? id) =>
        Lookup(ref _animalAbilities, () => AnimalAbilities, a => a.Id, id);

    private Dictionary<string, AnimalDefinition>? _animals;
    private Dictionary<string, AnimalAbilityDefinition>? _animalAbilities;

    public SpeciesAppearance? FindAppearance(string? speciesId) =>
        Lookup(ref _appearances, () => Appearances, a => a.Species, speciesId);

    private Dictionary<string, SpeciesAppearance>? _appearances;

    /// <summary>Würfelausdruck für das Startalter („16+1W3“) oder <c>null</c>.</summary>
    public string? StartAge(string? speciesId, string levelId) =>
        StartAges.Species.FirstOrDefault(s => s.Species == speciesId)?.Ages.GetValueOrDefault(levelId);

    public WeaponDefinition? FindWeapon(string? id) => Lookup(ref _weapons, () => Weapons, w => w.Id, id);

    public ArmorDefinition? FindArmor(string? id) => Lookup(ref _armors, () => Armors, a => a.Id, id);

    public IReadOnlyList<ItemDefinition> Items { get; private init; } = [];

    public IReadOnlyList<EquipmentPackageDefinition> EquipmentPackages { get; private init; } = [];

    public ItemDefinition? FindItem(string? id) => Lookup(ref _items, () => Items, i => i.Id, id);

    public EquipmentPackageDefinition? FindEquipmentPackage(string? id) =>
        Lookup(ref _packages, () => EquipmentPackages, p => p.Id, id);

    /// <summary>Waffe, Rüstung oder sonstiger Gegenstand.</summary>
    public IPricedItem? FindAnyItem(string? id) => FindWeapon(id) ?? FindArmor(id) ?? (IPricedItem?)FindItem(id);

    private Dictionary<string, ItemDefinition>? _items;
    private Dictionary<string, EquipmentPackageDefinition>? _packages;

    private Dictionary<string, WeaponDefinition>? _weapons;
    private Dictionary<string, ArmorDefinition>? _armors;

    public SpellDefinition? FindSpell(string? id) => Lookup(ref _spells, () => Spells, s => s.Id, id);

    public TraitDefinition? FindTrait(string? id) =>
        Lookup(ref _traits, () => [.. Advantages, .. Disadvantages], t => t.Id, id);

    public TraitDefinition? FindSpecialAbility(string? id) =>
        Lookup(ref _specialAbilities, () => SpecialAbilities, t => t.Id, id);

    public SpeciesDefinition? FindSpecies(string? id) => Lookup(ref _species, () => Species, s => s.Id, id);

    public CultureDefinition? FindCulture(string? id) => Lookup(ref _cultures, () => Cultures, c => c.Id, id);

    public ProfessionDefinition? FindProfession(string? id) =>
        Lookup(ref _professions, () => Professions, p => p.Id, id);

    public SkillDefinition? FindSkill(string? id) => Lookup(ref _skills, () => Skills, s => s.Id, id);

    public CombatTechniqueDefinition? FindCombatTechnique(string? id) => Lookup(ref _combatTechniques, () => CombatTechniques, s => s.Id, id);

    public LanguageDefinition? FindLanguage(string? id) => Lookup(ref _languages, () => Languages, s => s.Id, id);

    public ScriptDefinition? FindScript(string? id) => Lookup(ref _scripts, () => Scripts, s => s.Id, id);

    // Verzeichnisse nach ID, beim ersten Zugriff aufgebaut (die Listen haben bis zu ~1900 Einträge).
    private Dictionary<string, SpellDefinition>? _spells;
    private Dictionary<string, TraitDefinition>? _traits;
    private Dictionary<string, TraitDefinition>? _specialAbilities;
    private Dictionary<string, SpeciesDefinition>? _species;
    private Dictionary<string, CultureDefinition>? _cultures;
    private Dictionary<string, ProfessionDefinition>? _professions;
    private Dictionary<string, SkillDefinition>? _skills;
    private Dictionary<string, CombatTechniqueDefinition>? _combatTechniques;
    private Dictionary<string, LanguageDefinition>? _languages;
    private Dictionary<string, ScriptDefinition>? _scripts;

    private static T? Lookup<T>(ref Dictionary<string, T>? index, Func<IEnumerable<T>> items, Func<T, string> key, string? id)
        where T : class
    {
        if (id is null) return null;
        index ??= items().GroupBy(key).ToDictionary(g => g.Key, g => g.First());
        return index.GetValueOrDefault(id);
    }

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
            Spells = set.Spells ?? profession.Spells,
            SpellChoices = set.SpellChoices ?? profession.SpellChoices,
            UnparsedSpells = set.UnparsedSpells,
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

        var specialAbilities = ReadFolder(open, all, "special-abilities/", ctx.ListTraitDefinition)
            .Select(d => d with { IsSpecialAbility = true }).ToList();
        var languages = ReadFolder(open, all, "languages/", ctx.ListLanguageDefinition);
        var scripts = ReadFolder(open, all, "scripts/", ctx.ListScriptDefinition);
        var spells = ReadFolder(open, all, "spells/", ctx.ListSpellDefinition);
        var weapons = ReadFolder(open, all, "weapons/", ctx.ListWeaponDefinition);
        var armors = ReadFolder(open, all, "armor/", ctx.ListArmorDefinition);
        var items = ReadFolder(open, all, "items/", ctx.ListItemDefinition);
        var packages = ReadFolder(open, all, "equipment-packages/", ctx.ListEquipmentPackageDefinition);
        var appearances = ReadFolder(open, all, "appearance/", ctx.ListSpeciesAppearance);
        var startAges = Read(open, "rules/start-age.json", ctx.StartAgeTable);
        var birthdates = Read(open, "rules/birthdate.json", ctx.BirthdateTable);
        var animals = ReadFolder(open, all, "animals/", ctx.ListAnimalDefinition);
        var animalAbilities = ReadFolder(open, all, "animal-abilities/", ctx.ListAnimalAbilityDefinition);
        var familiars = Read(open, "rules/familiars.json", ctx.FamiliarRules);

        var data = new RuleData(books, attributes, levels, creation, costs, skills, combatTechniques,
            species, cultures, professions, advantages, disadvantages, specialAbilities, languages, scripts)
            {
                Spells = spells, Weapons = weapons, Armors = armors, Items = items, EquipmentPackages = packages,
                Appearances = appearances, StartAges = startAges, Birthdates = birthdates,
                Animals = animals, AnimalAbilities = animalAbilities, Familiars = familiars,
            };
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
        Unique(SpecialAbilities.Select(t => t.Id), "Sonderfertigkeits");
        Unique(Languages.Select(t => t.Id), "Sprach");
        Unique(Scripts.Select(t => t.Id), "Schrift");
        Unique(Spells.Select(t => t.Id), "Zauber-/Liturgie");
        Unique(Weapons.Select(t => t.Id), "Waffen");
        Unique(Armors.Select(t => t.Id), "Rüstungs");
        foreach (var w in Weapons)
        {
            Sources(w.Id, w.Publications);
            if (w.CombatTechnique is { } ct) Combat(w.Id, ct);
        }
        foreach (var a in Armors) Sources(a.Id, a.Publications);
        Unique(Weapons.Select(t => t.Id).Concat(Armors.Select(t => t.Id)).Concat(Items.Select(t => t.Id)), "Gegenstands");
        Unique(EquipmentPackages.Select(t => t.Id), "Ausrüstungspaket");
        foreach (var i in Items) Sources(i.Id, i.Publications);
        foreach (var p in EquipmentPackages)
        {
            Sources(p.Id, p.Publications);
            foreach (var line in p.Items.Where(l => l.Ref is not null && FindAnyItem(l.Ref) is null))
                errors.Add($"'{p.Id}' verweist auf unbekannten Gegenstand '{line.Ref}'.");
        }
        foreach (var sp in Spells)
        {
            Sources(sp.Id, sp.Publications);
            if (sp.Check is { } check && (check.Count != 3 || check.Any(a => Attributes.All(x => x.Id != a))))
                errors.Add($"'{sp.Id}' hat eine ungültige Probe.");
        }
        foreach (var l in Languages) Sources(l.Id, l.Publications);
        foreach (var sc in Scripts) Sources(sc.Id, sc.Publications);
        foreach (var t in Advantages.Concat(Disadvantages).Concat(SpecialAbilities))
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
            foreach (var p in (t.Prerequisites ?? []).Concat((t.Prerequisites ?? []).SelectMany(p => p.Options ?? [])))
            {
                if (p.Type == PrerequisiteType.Skill) Skill(t.Id, p.Name!);
                if (p.Type == PrerequisiteType.CombatTechnique) Combat(t.Id, p.Name!);
            }
            var sign = t.IsDisadvantage ? -1 : 1;
            if ((c.Ap ?? 0) * sign < 0) errors.Add($"'{t.Id}': AP-Wert mit falschem Vorzeichen.");
        }

        ValidatePersonalTables(errors);
        ValidateAnimals(errors);

        if (errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
    }

    /// <summary>Tierarten und Tierfähigkeiten: eindeutige IDs, Quellen, Verweise auf Talente, Tricks und Aufsätze.</summary>
    private void ValidateAnimals(List<string> errors)
    {
        foreach (var dup in Animals.Select(a => a.Id).Concat(AnimalAbilities.Select(a => a.Id)).GroupBy(i => i).Where(g => g.Count() > 1))
            errors.Add($"Doppelte Tier-ID '{dup.Key}'.");
        var attributeIds = Attributes.Select(a => a.Id).ToHashSet();
        var abilities = AnimalAbilities.ToDictionary(a => a.Id, a => a.Kind);
        void Ability(string owner, string id, AnimalAbilityKind kind)
        {
            if (abilities.GetValueOrDefault(id, (AnimalAbilityKind)(-1)) != kind)
                errors.Add($"'{owner}' verweist auf unbekannten Eintrag '{id}' ({kind}).");
        }
        void Sources(string id, IReadOnlyList<Publication> publications)
        {
            if (publications.Count == 0) errors.Add($"'{id}' hat keine Publikation.");
            foreach (var p in publications.Where(p => !Books.Contains(p.Book)))
                errors.Add($"'{id}' verweist auf unbekanntes Buch '{p.Book}'.");
        }

        foreach (var a in Animals)
        {
            Sources(a.Id, a.Publications);
            foreach (var key in a.Attributes.Keys.Where(k => !attributeIds.Contains(k)))
                errors.Add($"'{a.Id}' hat unbekannte Eigenschaft '{key}'.");
            foreach (var s in (a.Skills ?? []).Where(s => FindSkill(s.Skill) is null))
                errors.Add($"'{a.Id}' verweist auf unbekanntes Talent '{s.Skill}'.");
            foreach (var t in a.Training?.Trainings ?? []) Ability(a.Id, t, AnimalAbilityKind.Training);
        }
        foreach (var a in AnimalAbilities)
        {
            Sources(a.Id, a.Publications);
            foreach (var t in (a.RequiredTricks ?? []).Concat(a.Tricks ?? [])) Ability(a.Id, t, AnimalAbilityKind.Trick);
            foreach (var b in a.Bonuses ?? [])
                foreach (var target in b.Options ?? [b.Target])
                    if (target.StartsWith("skill.", StringComparison.Ordinal) && FindSkill(target) is null)
                        errors.Add($"'{a.Id}': Bonus auf unbekanntes Talent '{target}'.");
        }

        Sources("Vertrautentiere", Familiars.Publications);
        foreach (var t in Familiars.Traditions)
        {
            if (FindSpecialAbility(t.Tradition)?.Tradition is null) errors.Add($"Vertraute: unbekannte Tradition '{t.Tradition}'.");
            foreach (var animal in t.Choices.SelectMany(c => c.Animals).Where(a => FindAnimal(a) is null))
                errors.Add($"Vertraute '{t.Tradition}': unbekannte Tierart '{animal}'.");
            foreach (var trick in (t.ExcludedTricks ?? []).Concat(t.TrickNames?.Keys ?? []))
                Ability(t.Tradition, trick, AnimalAbilityKind.FamiliarTrick);
        }
        foreach (var animal in Familiars.Kinds.Keys.Where(a => FindAnimal(a) is null))
            errors.Add($"Vertraute: unbekannte Tierart '{animal}'.");
        foreach (var animal in Familiars.Traditions.SelectMany(t => t.Choices).SelectMany(c => c.Animals).Where(a => !Familiars.Kinds.ContainsKey(a)))
            errors.Add($"Vertraute: Tierart '{animal}' ohne Bezeichnung für die Vertrautentricks.");
        foreach (var trick in Familiars.FreeTricks) Ability("Vertrautentiere", trick, AnimalAbilityKind.FamiliarTrick);
    }

    /// <summary>Würfeltabellen der persönlichen Angaben: W20-Tabellen lückenlos, Würfelausdrücke lesbar, Verweise gültig.</summary>
    private void ValidatePersonalTables(List<string> errors)
    {
        void Table(string owner, IReadOnlyList<RollEntry>? table)
        {
            if (table is null) return;
            var covered = table.SelectMany(e => Enumerable.Range(e.Min, Math.Max(0, e.Max - e.Min + 1))).ToList();
            if (!covered.SequenceEqual(Enumerable.Range(1, 20)))
                errors.Add($"'{owner}': W20-Tabelle deckt nicht genau 1–20 ab.");
        }

        void Dice(string owner, string? text, bool reference)
        {
            if (text is null) return;
            if (!DiceExpression.TryParse(text, out var e) || e.UsesReference != reference)
                errors.Add($"'{owner}': ungültiger Würfelausdruck '{text}'.");
        }

        var speciesIds = Species.Select(s => s.Id).ToHashSet();
        var cultureIds = Cultures.Select(c => c.Id).ToHashSet();
        foreach (var a in Appearances)
        {
            Sources(a.Species, a.Publications);
            if (!speciesIds.Contains(a.Species)) errors.Add($"Aussehen für unbekannte Spezies '{a.Species}'.");
            Table(a.Species, a.Hair);
            Table(a.Species, a.Eyes);
            Dice(a.Species, a.Height, false);
            Dice(a.Species, a.Weight, true);
            foreach (var v in a.Variants ?? [])
            {
                var owner = $"{a.Species}/{v.Name}";
                Table(owner, v.Hair);
                Table(owner, v.Eyes);
                Dice(owner, v.Height, false);
                Dice(owner, v.Weight, true);
                if (v.BasedOn is { } b && a.Variants!.All(x => x.Name != b)) errors.Add($"'{owner}': unbekannte Grundlage '{b}'.");
                foreach (var c in (v.Cultures ?? []).Where(c => !cultureIds.Contains(c)))
                    errors.Add($"'{owner}' verweist auf unbekannte Kultur '{c}'.");
            }
            foreach (var choice in a.Choices.Select(c => a.Resolve(c.Name)))
                if (choice.Hair is null || choice.Eyes is null || choice.Height is null || choice.Weight is null)
                    errors.Add($"'{a.Species}/{choice.Name}': Aussehen unvollständig.");
        }
        Unique(Appearances.Select(a => a.Species), "Aussehens");

        void Sources(string id, IReadOnlyList<Publication> publications)
        {
            if (publications.Count == 0) errors.Add($"'{id}' hat keine Publikation.");
            foreach (var p in publications.Where(p => !Books.Contains(p.Book)))
                errors.Add($"'{id}' verweist auf unbekanntes Buch '{p.Book}'.");
        }

        void Unique(IEnumerable<string> ids, string what)
        {
            foreach (var dup in ids.GroupBy(i => i).Where(g => g.Count() > 1))
                errors.Add($"Doppelte {what}-ID '{dup.Key}'.");
        }

        Sources("Startalter", StartAges.Publications);
        foreach (var s in StartAges.Species)
        {
            if (!speciesIds.Contains(s.Species)) errors.Add($"Startalter für unbekannte Spezies '{s.Species}'.");
            foreach (var (level, dice) in s.Ages)
            {
                if (ExperienceLevels.All(l => l.Id != level)) errors.Add($"Startalter '{s.Species}': unbekannter Erfahrungsgrad '{level}'.");
                Dice(s.Species, dice, false);
            }
        }
        Sources("Geburtsdatum", Birthdates.Publications);
        Table("Geburtsmonat", Birthdates.Months);

        foreach (var c in Cultures)
        {
            if (c.Names is not { } n) continue;
            foreach (var f in (n.From ?? []).Where(f => !cultureIds.Contains(f)))
                errors.Add($"'{c.Id}': Namen aus unbekannter Kultur '{f}'.");
            foreach (var format in (n.MaleFormats ?? []).Concat(n.FemaleFormats ?? []))
                if (!format.Contains("{first}")) errors.Add($"'{c.Id}': Namensformat ohne Vorname.");
        }
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
[JsonSerializable(typeof(List<LanguageDefinition>))]
[JsonSerializable(typeof(List<ScriptDefinition>))]
[JsonSerializable(typeof(List<SpellDefinition>))]
[JsonSerializable(typeof(List<WeaponDefinition>))]
[JsonSerializable(typeof(List<ArmorDefinition>))]
[JsonSerializable(typeof(List<ItemDefinition>))]
[JsonSerializable(typeof(List<EquipmentPackageDefinition>))]
[JsonSerializable(typeof(List<SpeciesAppearance>))]
[JsonSerializable(typeof(StartAgeTable))]
[JsonSerializable(typeof(BirthdateTable))]
[JsonSerializable(typeof(List<AnimalDefinition>))]
[JsonSerializable(typeof(List<AnimalAbilityDefinition>))]
[JsonSerializable(typeof(FamiliarRules))]
internal partial class RuleJsonContext : JsonSerializerContext;
