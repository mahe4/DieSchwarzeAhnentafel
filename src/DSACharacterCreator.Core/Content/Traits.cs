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
    /// <summary>Je nach Anzahl (erste, zweite, dritte Merkmalskenntnis …): <c>Levels</c> in dieser Reihenfolge.</summary>
    ByOccurrence,
}

public enum TraitOptionTarget
{
    Skill,
    CombatTechnique,
    Script,
    Language,
    /// <summary>Merkmal eines Zaubers (Merkmalskenntnis).</summary>
    Property,
    /// <summary>Aspekt einer Liturgie (Aspektkenntnis).</summary>
    Aspect,
}

/// <param name="PermanentAstral">Permanente AsP dieser Ausprägung (Bindung des Bannschwerts je Kampftechnik).</param>
public sealed record TraitCostOption(string Name, int Ap, int? PermanentAstral = null);

/// <summary>Kosten eines Vor- oder Nachteils (Nachteile negativ).</summary>
public sealed record TraitCost(
    TraitCostType Type,
    int? Ap,
    IReadOnlyList<int>? Levels,
    TraitOptionTarget? Target,
    IReadOnlyDictionary<ImprovementCost, int>? ByFactor,
    IReadOnlyList<TraitCostOption>? Options,
    bool PerUse,
    /// <summary>n-ter Eintrag für dasselbe Talent kostet das n-Fache (Fertigkeitsspezialisierung).</summary>
    bool Escalating = false);

public enum PrerequisiteType
{
    Attribute,
    /// <summary>Talent (<c>Name</c> = Talent-ID) mindestens auf <c>Min</c>.</summary>
    Skill,
    /// <summary>Kampftechnik (<c>Name</c> = ID) mindestens auf <c>Min</c>.</summary>
    CombatTechnique,
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
    /// <summary>Eine der <c>Options</c> genügt.</summary>
    AnyOf,
    /// <summary>Ein Kampf- bzw. Talentstil, der diese erweiterte Sonderfertigkeit freischaltet.</summary>
    FittingStyle,
    /// <summary>Höchstens <c>Count</c> Einträge dieses Vor-/Nachteils (jeweils mit anderer Fertigkeit).</summary>
    MaxCount,
    /// <summary>Leiteigenschaft der (magischen bzw. karmalen) Tradition mindestens auf <c>Min</c>.</summary>
    PrimaryAttribute,
    /// <summary><c>Count</c> Zauber des gewählten Merkmals bzw. Liturgien des gewählten Aspekts auf mindestens <c>Min</c>.</summary>
    SpellsWithOption,
}

/// <summary>Herkunftsbereich einer Sonderfertigkeit.</summary>
public enum AbilityDomain
{
    Profane,
    Magical,
    Karmal,
}

/// <summary>Angaben einer Tradition (Sonderfertigkeit „Tradition (…)“).</summary>
public sealed record TraditionInfo(
    AbilityDomain Kind,
    /// <summary>Eigenschafts-ID der Leiteigenschaft, null bei Traditionen ohne Leiteigenschaft.</summary>
    string? PrimaryAttribute,
    /// <summary>Leiteigenschaft zählt nur zur Hälfte (aufgerundet) für den AsP-Grundwert (Heldenerschaffung › Leiteigenschaften).</summary>
    bool HalfPrimaryAttribute = false,
    /// <summary>Wohlgefällige Talente (Wiki-Text, karmale Traditionen).</summary>
    string? FavouredSkillsText = null);

public sealed record Prerequisite(
    PrerequisiteType Type,
    string? Attribute,
    int? Min,
    string? Name,
    IReadOnlyList<string>? Names,
    string? Group,
    int? Count = null,
    /// <summary>Gilt erst ab dieser Stufe des Eintrags („Stufe II: KK 15“).</summary>
    int? FromLevel = null,
    IReadOnlyList<Prerequisite>? Options = null,
    /// <summary>Bei <c>MaxCount</c>: höchstens so oft mit derselben Auswahl („keine Fertigkeit mehr als zweimal“).</summary>
    int? PerOption = null);

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

/// <summary>Vorteil, Nachteil oder Sonderfertigkeit. Texte stammen aus dem Regel-Wiki.</summary>
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

    /// <summary>Wird beim Laden gesetzt.</summary>
    public bool IsSpecialAbility { get; init; }

    /// <summary>Kategorie der Sonderfertigkeit im Wiki (z. B. „Kampfsonderfertigkeiten“).</summary>
    public string? Category { get; init; }

    /// <summary>Betroffene Kampftechniken (Wiki-Text, bei Kampfsonderfertigkeiten).</summary>
    public string? CombatTechniquesText { get; init; }

    /// <summary>Bei Stilen: freigeschaltete erweiterte Sonderfertigkeiten (Wiki-Text).</summary>
    public string? ExtendedAbilitiesText { get; init; }

    /// <summary>Bei erweiterten Sonderfertigkeiten: passende Stile (Wiki-Text).</summary>
    public string? FittingStylesText { get; init; }

    public string? Penalty { get; init; }

    /// <summary>
    /// Stufen ohne festes Ende („Stufe I: 5 AP, Stufe II: 5 AP … usw.“); <c>MaxLevel</c> ist dann nur eine technische Grenze.
    /// </summary>
    public bool OpenLevels { get; init; }

    /// <summary>Profan, magisch oder karmal (nur Sonderfertigkeiten).</summary>
    public AbilityDomain Domain { get; init; }

    /// <summary>Weitere Wiki-Felder (Zeremonialgegenstand, Aspekt, Merkmal …) als „Feld: Wert“-Zeilen.</summary>
    public string? DetailsText { get; init; }

    /// <summary>
    /// Permanente AsP, die die Sonderfertigkeit kostet (Bindung eines Traditionsartefakts). Sie verringern den AsP-Höchstwert
    /// und lassen sich für 2 AP je Punkt zurückkaufen (Erfahrung).
    /// </summary>
    public int? PermanentAstralCost { get; init; }

    /// <summary>Nur bei Traditionen gesetzt.</summary>
    public TraditionInfo? Tradition { get; init; }

    /// <summary>Wofür eine Auswahl nötig ist (Sprache, Schrift, Talent …); ergibt sich sonst aus den Kosten.</summary>
    public TraitOptionTarget? OptionTarget
    {
        get => _optionTarget ?? (Cost.Type == TraitCostType.BySkillFactor ? Cost.Target : null);
        init => _optionTarget = value;
    }

    private readonly TraitOptionTarget? _optionTarget;
}

/// <summary>Sprache (für die Sonderfertigkeit Sprache I–III).</summary>
public sealed record LanguageDefinition(
    string Id,
    string Name,
    string Region,
    string? DialectsText,
    string? ScriptText,
    string? Note,
    int? MaxLevel,
    bool Extinct,
    IReadOnlyList<Publication> Publications);

/// <summary>Schrift (für die Sonderfertigkeit Schrift; Kosten nach Komplexität A–D).</summary>
public sealed record ScriptDefinition(
    string Id,
    string Name,
    string Region,
    ImprovementCost ImprovementCost,
    string? AlphabetText,
    string? LanguagesText,
    bool Extinct,
    IReadOnlyList<Publication> Publications);
