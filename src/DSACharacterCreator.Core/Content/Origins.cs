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
    IReadOnlyList<Publication> Publications);

public sealed record SkillValue(string Skill, int Value);

public sealed record CombatTechniqueValue(string CombatTechnique, int Value);

/// <summary>„Eine der folgenden Kampftechniken 10: …“. Mehrere Auswahlen mit gleichen Optionen müssen verschiedene Techniken wählen.</summary>
public sealed record CombatTechniqueChoice(IReadOnlyList<string> Options, int Value);

public sealed record CulturePackage(string Name, int Ap, IReadOnlyList<SkillValue> Skills);

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

/// <summary>Variante einer Profession: eigener AP-Wert und geänderte Werte („Klettern 7 statt 3“).</summary>
public sealed record ProfessionVariant(
    string Id,
    string Name,
    int Ap,
    IReadOnlyList<SkillValue> Skills,
    IReadOnlyList<CombatTechniqueValue> CombatTechniques,
    string? Text);

/// <summary>
/// Profession mit Professionspaket. Talente und Kampftechniken sind strukturiert; Sonderfertigkeiten, Zauber und
/// Liturgien vorerst Wiki-Text. <see cref="Unparsed"/> enthält Teile, die beim Import nicht zugeordnet werden konnten.
/// </summary>
public sealed record ProfessionDefinition(
    string Id,
    string Name,
    string Group,
    int Ap,
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
    IReadOnlyList<Publication> Publications);
