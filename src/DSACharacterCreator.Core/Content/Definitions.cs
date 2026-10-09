namespace DSACharacterCreator.Core.Content;

/// <summary>Steigerungsfaktor (Spalte der Kostentabelle).</summary>
public enum ImprovementCost
{
    A,
    B,
    C,
    D,
    E,
}

public sealed record AttributeDefinition(string Id, string Name, IReadOnlyList<Publication> Publications);

public enum SkillGroup
{
    Body,
    Social,
    Nature,
    Knowledge,
    Craft,
}

public enum Encumbrance
{
    Yes,
    No,
    Maybe,
}

/// <summary>Ein Talent. Die Texte stammen wörtlich aus dem Regel-Wiki.</summary>
public sealed record SkillDefinition(
    string Id,
    string Name,
    SkillGroup Group,
    IReadOnlyList<string> Check,
    ImprovementCost ImprovementCost,
    Encumbrance Encumbrance,
    string EncumbranceText,
    string Applications,
    string? Tools,
    string Quality,
    string FailedCheck,
    string CriticalSuccess,
    string Botch,
    IReadOnlyList<Publication> Publications);

public enum CombatTechniqueCategory
{
    Melee,
    Ranged,
}

public sealed record CombatTechniqueDefinition(
    string Id,
    string Name,
    CombatTechniqueCategory Category,
    IReadOnlyList<string> PrimaryAttributes,
    ImprovementCost ImprovementCost,
    string AttackAttribute,
    bool CanParry,
    string? Special,
    IReadOnlyList<Publication> Publications);

/// <summary>Erfahrungsgrad: AP-Konto und Höchstwerte bei der Heldenerschaffung.</summary>
public sealed record ExperienceLevel(
    string Id,
    string Name,
    int Ap,
    int MaxAttribute,
    int MaxSkill,
    int MaxCombatTechnique,
    int MaxAttributeTotal,
    int MaxSpellsAndLiturgies,
    int MaxForeignSpells);

/// <summary>Feste Werte der Heldenerschaffung.</summary>
public sealed record CreationRules(
    string DefaultExperienceLevel,
    int AttributeStartValue,
    int CombatTechniqueStartValue,
    int FatePoints,
    int MaxAdvantageAp,
    int MaxDisadvantageAp,
    int MaxLeftoverAp,
    int SkillMaxAboveCheckAttribute,
    int CombatTechniqueMaxAbovePrimaryAttribute,
    IReadOnlyList<Publication> Publications);
