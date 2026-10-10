namespace DSACharacterCreator.Core.Content;

/// <summary>Eigenschaftsänderung einer Spezies. Mehr als eine Option = Auswahl (z. B. „KL oder KK −2“).</summary>
public sealed record AttributeChange(IReadOnlyList<string> Options, int Value)
{
    public bool IsChoice => Options.Count > 1;
}

/// <summary>Spezies. Texte stammen aus dem Regel-Wiki.</summary>
public sealed record SpeciesDefinition(
    string Id,
    string Name,
    int Ap,
    int LifeBase,
    int SpiritBase,
    int ToughnessBase,
    int SpeedBase,
    string AttributeChangesText,
    IReadOnlyList<AttributeChange> AttributeChanges,
    string? AutomaticAdvantagesText,
    string? AutomaticDisadvantagesText,
    string? AdvantageApText,
    string? CommonCulturesText,
    string? RecommendedText,
    string? TypicalAdvantagesText,
    string? TypicalDisadvantagesText,
    string? UntypicalAdvantagesText,
    string? UntypicalDisadvantagesText,
    string? Note,
    IReadOnlyList<Publication> Publications)
{
    /// <summary>Namen der automatischen Vorteile (im Spezies-AP-Wert enthalten).</summary>
    public IReadOnlyList<string> AutomaticAdvantages { get; init; } = [];

    public IReadOnlyList<string> AutomaticDisadvantages { get; init; } = [];

    /// <summary>AP-Anteil der automatischen Vorteile; zählt auf die 80-AP-Grenze für Vorteile.</summary>
    public int AutomaticAdvantageAp { get; init; }

    /// <summary>AP-Anteil der automatischen Nachteile (negativ); zählt auf die 80-AP-Grenze für Nachteile.</summary>
    public int AutomaticDisadvantageAp { get; init; }
}

public sealed record SkillValue(string Skill, int Value);

public sealed record CombatTechniqueValue(string CombatTechnique, int Value);

/// <summary>„Eine der folgenden Kampftechniken 10: …“. Mehrere Auswahlen mit gleichen Optionen müssen verschiedene Techniken wählen.</summary>
public sealed record CombatTechniqueChoice(IReadOnlyList<string> Options, int Value);

/// <param name="WikiAp">AP-Wert laut Wiki – nur zum Vergleich; die Kosten werden aus den Talenten berechnet.</param>
public sealed record CulturePackage(string Name, int WikiAp, IReadOnlyList<SkillValue> Skills);

public sealed record CultureDefinition(
    string Id,
    string Name,
    string Group,
    string? LanguageText,
    string? ScriptText,
    string? LocalKnowledgeText,
    string? SocialStatusText,
    string? CommonProfessionsText,
    string? TypicalAdvantagesText,
    string? TypicalDisadvantagesText,
    string? UntypicalAdvantagesText,
    string? UntypicalDisadvantagesText,
    string? TypicalSkillsText,
    string? UntypicalSkillsText,
    string? NamesText,
    CulturePackage? Package,
    IReadOnlyList<Publication> Publications);

/// <summary>Variante einer Profession: geänderte Werte („Klettern 7 statt 3“).</summary>
/// <param name="WikiAp">AP-Wert laut Wiki – nur zum Vergleich.</param>
/// <param name="Text">Nicht strukturierte Änderungen (z. B. Sonderfertigkeiten).</param>
public sealed record ProfessionVariant(
    string Id,
    string Name,
    int WikiAp,
    IReadOnlyList<SkillValue> Skills,
    IReadOnlyList<CombatTechniqueValue> CombatTechniques,
    string? Text);

/// <summary>
/// Profession mit Professionspaket. <c>WikiAp</c> ist <c>null</c>, wenn das Wiki mehrere Werte nennt (je nach Variante
/// oder Erfahrungsgrad). Talente und Kampftechniken sind strukturiert; Sonderfertigkeiten, Zauber und
/// Liturgien vorerst Wiki-Text. <see cref="Unparsed"/> enthält Teile, die beim Import nicht zugeordnet werden konnten.
/// </summary>
public sealed record ProfessionDefinition(
    string Id,
    string Name,
    string Group,
    int? WikiAp,
    string? PrerequisitesText,
    IReadOnlyDictionary<string, int>? AttributeRequirements,
    string? SpecialAbilitiesText,
    IReadOnlyList<CombatTechniqueValue> CombatTechniques,
    IReadOnlyList<CombatTechniqueChoice>? CombatTechniqueChoices,
    IReadOnlyList<SkillValue> Skills,
    string? SpellsText,
    string? LiturgiesText,
    string? StylesText,
    string? RecommendedAdvantagesText,
    string? RecommendedDisadvantagesText,
    string? UnsuitableAdvantagesText,
    string? UnsuitableDisadvantagesText,
    string? Note,
    IReadOnlyList<ProfessionVariant>? Variants,
    bool VariantRequired,
    IReadOnlyList<string>? Unparsed,
    IReadOnlyList<Publication> Publications)
{
    /// <summary>
    /// Wertesätze je Erfahrungsgrad („Elementarist (erfahren/kompetent/meisterlich)“); <c>null</c> bei normalen Professionen.
    /// </summary>
    public IReadOnlyList<ProfessionLevelValues>? ByExperienceLevel { get; init; }

    /// <summary>Sonderfertigkeiten der Vorlage (profane, magische und karmale).</summary>
    public IReadOnlyList<TemplateEntry> SpecialAbilities
    {
        get => _specialAbilities ?? [];
        init => _specialAbilities = value;
    }

    private readonly IReadOnlyList<TemplateEntry>? _specialAbilities;

    /// <summary>„Sprachen und Schriften für insgesamt N AP“ – frei zu verteilen.</summary>
    public int? LanguageAp { get; init; }

    /// <summary>Sonderfertigkeiten der Vorlage, die (noch) nicht zugeordnet werden konnten.</summary>
    public IReadOnlyList<string>? UnparsedSpecialAbilities { get; init; }

    /// <summary>
    /// Vor-/Nachteile und Sonderfertigkeiten aus den Voraussetzungen (Zauberer, Tradition, Prinzipientreue …). Sie gehören
    /// nicht zum AP-Wert der Profession, werden aber als Vorauswahl übernommen.
    /// </summary>
    public IReadOnlyList<TemplateEntry> PrerequisiteEntries
    {
        get => _prerequisiteEntries ?? [];
        init => _prerequisiteEntries = value;
    }

    private readonly IReadOnlyList<TemplateEntry>? _prerequisiteEntries;

    /// <summary>Zauber, Rituale, magische Handlungen, Liturgien, Zeremonien, Segnungen der Vorlage.</summary>
    public IReadOnlyList<TemplateSpell> Spells
    {
        get => _spells ?? [];
        init => _spells = value;
    }

    private readonly IReadOnlyList<TemplateSpell>? _spells;

    /// <summary>Auswahlen (bisher nur Zaubertricks).</summary>
    public IReadOnlyList<SpellChoice> SpellChoices
    {
        get => _spellChoices ?? [];
        init => _spellChoices = value;
    }

    private readonly IReadOnlyList<SpellChoice>? _spellChoices;

    /// <summary>Nicht zugeordnete Teile der Zauber/Liturgien (Werte je Erfahrungsgrad, Platzhalter …).</summary>
    public IReadOnlyList<string>? UnparsedSpells { get; init; }

    /// <summary>„Flüche für insgesamt N AP“ – frei zu wählen.</summary>
    public int? CurseAp { get; init; }
}

/// <summary>Zauber/Liturgie einer Vorlage; <c>Value</c> fehlt bei Zaubertricks und Segnungen.</summary>
public sealed record TemplateSpell(string Id, int? Value = null);

/// <summary>„Ein Zaubertrick aus folgender Liste: …“</summary>
public sealed record SpellChoice(int Count, IReadOnlyList<string> Options);

public enum TemplateEntryKind
{
    SpecialAbility,
    Advantage,
    Disadvantage,
}

/// <summary>Eintrag einer Vorlage (Sonderfertigkeit mit Stufe/Auswahl).</summary>
public sealed record TemplateEntry(string Id, int? Level = null, string? Option = null, string? Specification = null,
    TemplateEntryKind Kind = TemplateEntryKind.SpecialAbility);

/// <summary>Werte einer Profession für einen Erfahrungsgrad.</summary>
public sealed record ProfessionLevelValues(
    string Level,
    int? WikiAp,
    IReadOnlyList<SkillValue> Skills,
    IReadOnlyList<CombatTechniqueValue> CombatTechniques,
    IReadOnlyList<CombatTechniqueChoice>? CombatTechniqueChoices,
    IReadOnlyDictionary<string, int>? AttributeRequirements);
