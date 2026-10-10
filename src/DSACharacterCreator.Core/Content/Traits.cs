namespace DSACharacterCreator.Core.Content;

public enum TraitCostType
{
    /// <summary>Fester AP-Wert (je Eintrag).</summary>
    Fixed,
    /// <summary>AP-Wert je Stufe.</summary>
    PerLevel,
    /// <summary>Eigener AP-Wert je Stufe (Liste, Stufe I zuerst).</summary>
    Levels,
    /// <summary>Je nach Steigerungsfaktor der gewählten Fertigkeit/Kampftechnik.</summary>
    BySkillFactor,
    /// <summary>Je nach gewählter Ausprägung.</summary>
    ByOption,
    /// <summary>Im Wiki nicht als fester Wert angegeben; der Spieler trägt den AP-Wert ein.</summary>
    Manual,
}

public enum TraitOptionTarget
{
    Skill,
    CombatTechnique,
}

public sealed record TraitCostOption(string Name, int Ap);

/// <summary>Kosten eines Vor- oder Nachteils (Nachteile negativ).</summary>
public sealed record TraitCost(
    TraitCostType Type,
    int? Ap,
    IReadOnlyList<int>? Levels,
    TraitOptionTarget? Target,
    IReadOnlyDictionary<ImprovementCost, int>? ByFactor,
    IReadOnlyList<TraitCostOption>? Options,
    bool PerUse);

public enum PrerequisiteType
{
    Attribute,
    RequiresAdvantage,
    ForbidsAdvantage,
    RequiresDisadvantage,
    ForbidsDisadvantage,
    RequiresSpecialAbility,
    Species,
    ForbidsSpecies,
    OriginRecommends,
    ExclusiveGroup,
    Culture,
    /// <summary>Die Kultur muss den Sozialstatus <c>Name</c> kennen (z. B. „Adel“).</summary>
    CultureSocialStatus,
    ForbidsSpecialAbility,
    /// <summary>Höchstens <c>Count</c> Einträge dieses Vor-/Nachteils (jeweils mit anderer Fertigkeit).</summary>
    MaxCount,
}

public sealed record Prerequisite(
    PrerequisiteType Type,
    string? Attribute,
    int? Min,
    string? Name,
    IReadOnlyList<string>? Names,
    string? Group,
    int? Count = null);

public enum EffectTarget
{
    LifePoints,
    Spirit,
    Toughness,
    Speed,
    FatePoints,
    AstralBase,
    KarmaBase,
    Astral,
    Karma,
}

public sealed record TraitEffect(EffectTarget Target, int Value, bool PerLevel);

/// <summary>Vorteil oder Nachteil. Texte stammen aus dem Regel-Wiki.</summary>
public sealed record TraitDefinition(
    string Id,
    string Name,
    int? MaxLevel,
    string RuleText,
    string? PrerequisitesText,
    IReadOnlyList<Prerequisite>? Prerequisites,
    IReadOnlyList<string>? UnparsedPrerequisites,
    string ApValueText,
    TraitCost Cost,
    IReadOnlyList<TraitEffect>? Effects,
    string? Range,
    string? Note,
    string? Errata,
    IReadOnlyList<Publication> Publications)
{
    /// <summary>Wird beim Laden gesetzt.</summary>
    public bool IsDisadvantage { get; init; }
}
