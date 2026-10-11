namespace DSACharacterCreator.Core.Content;

/// <summary>Angriff einer Tierart („Biss: AT 12 TP 1W3 RW kurz“).</summary>
public sealed record AnimalAttack(
    string Name,
    int? Attack,
    int? Ranged,
    string? LoadTime,
    string? Damage,
    string? Reach,
    string Text);

/// <summary>Talentwert einer Tierart; <c>Value</c> fehlt bei „Klettern (keine Probe erlaubt …)“.</summary>
public sealed record AnimalSkill(string Skill, int? Value, string? Note);

/// <summary>Abrichten: Mods und mögliche Ausbildungsaufsätze der Tierart (Tabelle „Tierarten und Modifikatoren“).</summary>
public sealed record AnimalTraining(
    int? TrainingModifier,
    int? TrickModifier,
    IReadOnlyList<string> Trainings,
    string? Complexity);

/// <summary>Tierart aus dem Bestiarium des Regel-Wikis.</summary>
/// <param name="AttributeNotes">„t“ = tierische Klugheit, „k“ = Körperkraft schwacher Wesen.</param>
/// <param name="Defense">Verteidigung (VW) nicht humanoider Wesen; humanoide haben <c>Parry</c>/<c>Dodge</c>.</param>
public sealed record AnimalDefinition(
    string Id,
    string Name,
    string? Group,
    string? SizeText,
    string? WeightText,
    IReadOnlyDictionary<string, int> Attributes,
    IReadOnlyDictionary<string, string>? AttributeNotes,
    int? LifePoints,
    int? AstralEnergy,
    int? KarmaEnergy,
    string? Initiative,
    int? Defense,
    int? Dodge,
    int? Parry,
    int? Spirit,
    int? Toughness,
    int? Speed,
    string? SpeedText,
    IReadOnlyList<AnimalAttack> Attacks,
    int? Protection,
    int? Encumbrance,
    string? ProtectionText,
    string? Actions,
    IReadOnlyList<string>? Advantages,
    IReadOnlyList<string>? Disadvantages,
    IReadOnlyList<string>? RecommendedAdvantages,
    IReadOnlyList<string>? RecommendedDisadvantages,
    IReadOnlyList<string>? SpecialAbilities,
    IReadOnlyList<AnimalSkill>? Skills,
    string? SpellsText,
    string? LiturgiesText,
    string? Count,
    string? SizeCategory,
    string? Type,
    string? Prey,
    string? CombatBehaviour,
    string? Flight,
    string? PainText,
    string? Hunting,
    string? SpecialRules,
    string? Note,
    string? Errata,
    AnimalTraining? Training,
    IReadOnlyList<Publication> Publications,
    /// <summary>Laut Tierkunde-Angabe domestiziert (Loyalität startet bei 4) bzw. Wildtier (0); <c>null</c> = keine Angabe.</summary>
    bool? Domesticated = null,
    /// <summary>Vertrautentricks der Tierart (Wiki-Text, z. B. bei der Maraske).</summary>
    string? FamiliarTricksText = null);

public enum AnimalAbilityKind
{
    Trick,
    Training,
    FamiliarTrick,
    SpecialAbility,
    CombatSpecialAbility,
    Advantage,
    Disadvantage,
}

/// <summary>Bonus eines Ausbildungsaufsatzes: „+2 MU“, „+25 % LeP“, „Einschüchtern-FW +6“.</summary>
/// <param name="Target">Eigenschaft, Kürzel (AT, VW, TP, LeP, GS …) oder Talent-ID.</param>
/// <param name="Options">„Fährtensuchen- oder Sinnesschärfe-FW +8“: eines der Talente.</param>
public sealed record AnimalBonus(string Target, int Value, bool Percent = false, IReadOnlyList<string>? Options = null);

/// <summary>Trick, Ausbildungsaufsatz, Vertrautentrick, Sonderfertigkeit oder Vor-/Nachteil für Tiere.</summary>
/// <param name="Loyalty">Voraussetzung Loyalität mindestens auf diesem Wert.</param>
/// <param name="RequiredTricks">Voraussetzung: diese Tricks.</param>
/// <param name="Tricks">Ausbildungsaufsatz: enthaltene Tricks.</param>
/// <param name="Maneuver">Kampf-SF: „basis“ oder „spezial“.</param>
public sealed record AnimalAbilityDefinition(
    string Id,
    string Name,
    AnimalAbilityKind Kind,
    int? MaxLevel,
    string RuleText,
    string? PrerequisitesText,
    int? Loyalty,
    IReadOnlyList<string>? RequiredTricks,
    string? BonusesText,
    IReadOnlyList<AnimalBonus>? Bonuses,
    IReadOnlyList<string>? Tricks,
    string? AnimalsText,
    string? AstralCostText,
    string? DurationText,
    string? Property,
    string? Penalty,
    string? Maneuver,
    string ApValueText,
    TraitCost Cost,
    string? Note,
    string? Errata,
    IReadOnlyList<Publication> Publications);

/// <summary>Tierarten zur Auswahl (Schwesternschaft, Element); <c>Name</c> fehlt, wenn die Tradition nur eine Liste hat.</summary>
public sealed record FamiliarChoice(string? Name, IReadOnlyList<string> Animals);

/// <summary>Vertraute einer Tradition: mögliche Tierarten und Besonderheiten der Vertrautentricks.</summary>
/// <param name="TrickNames">Andere Namen von Vertrautentricks (Hexensinn → Geodensinn).</param>
public sealed record FamiliarTradition(
    string Tradition,
    IReadOnlyList<FamiliarChoice> Choices,
    IReadOnlyList<string>? ExcludedTricks,
    IReadOnlyDictionary<string, string>? TrickNames,
    string? Note);

/// <summary>Werteveränderungen durch die Vertrautenbindung.</summary>
/// <param name="Attributes">Auf jede Eigenschaft (körperliche und geistige).</param>
/// <param name="AstralEnergy">AsP des Vertrauten.</param>
public sealed record FamiliarBonuses(int Attributes, int LifePoints, int Protection, int Spirit, int Toughness, int AstralEnergy);

/// <summary>Grundregeln › Vertrautentiere.</summary>
/// <param name="Kinds">Tierart → Bezeichnung im Feld „Tierarten“ der Vertrautentricks („Katzen“, „Iltisse“).</param>
/// <param name="FreeTricks">Vertrautentricks, die jeder Vertraute kostenlos beherrscht.</param>
public sealed record FamiliarRules(
    IReadOnlyList<FamiliarTradition> Traditions,
    IReadOnlyDictionary<string, string> Kinds,
    FamiliarBonuses Bonuses,
    IReadOnlyList<string> FreeTricks,
    IReadOnlyList<Publication> Publications);
