using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Core.Rules;

/// <summary>Berechneter Stand eines Vertrautentiers bzw. tierischen Begleiters.</summary>
/// <param name="Tradition">Tradition des Helden mit Vertrauten (für Tierarten und Trick-Namen); <c>null</c> ohne.</param>
public sealed record CompanionState(
    Companion Companion,
    AnimalDefinition? Animal,
    FamiliarTradition? Tradition,
    IReadOnlyList<CompanionAttribute> Attributes,
    CompanionValues Values,
    IReadOnlyList<CompanionAttack> Attacks,
    IReadOnlyList<CompanionSkill> Skills,
    CompanionLoyalty Loyalty,
    IReadOnlyList<CompanionAbility> Abilities,
    CompanionAp Ap,
    IReadOnlyList<CompanionEntryState> Entries,
    IReadOnlyList<Issue> Issues)
{
    public int Attribute(string id) => Attributes.First(a => a.Definition.Id == id).Value;
}

/// <param name="Base">Wert der Tierart.</param>
/// <param name="Bonus">Vertrautenbonus, Ausbildungsaufsätze und Tiervorteile.</param>
public sealed record CompanionAttribute(AttributeDefinition Definition, int Value, int Base, int Bonus);

/// <param name="NoCheck">Die Tierart kann das Talent nicht (z. B. „Klettern (keine Probe erlaubt …)“).</param>
public sealed record CompanionSkill(SkillDefinition Definition, int Value, int Base, int Bonus, string? Note, bool NoCheck);

/// <param name="Attack">AT inkl. Boni und Steigerungen; <c>null</c>, wenn die Tierart keinen Zahlenwert hat.</param>
public sealed record CompanionAttack(string Name, int? Attack, int? Ranged, string? Damage, string? Reach, string Text);

/// <param name="Astral">AsP (Vertraute 15 + Zukauf); <c>null</c> ohne Astralenergie.</param>
public sealed record CompanionValues(
    int? LifePoints,
    int? Astral,
    string? Initiative,
    int? Defense,
    int? Dodge,
    int? Parry,
    int? Spirit,
    int? Toughness,
    int? Speed,
    string? SpeedText,
    int? Protection,
    int? Encumbrance);

/// <param name="HeroAp">AP, die der Held für die Steigerungen bezahlt hat.</param>
public sealed record CompanionLoyalty(int Value, int Start, int HeroAp);

public enum CompanionAbilitySource
{
    /// <summary>Selbst erworben (kostet AP des Tiers).</summary>
    Acquired,
    /// <summary>Hat jeder Vertraute (Zwiegespräch, Hexensinn).</summary>
    Familiar,
    /// <summary>Teil eines Ausbildungsaufsatzes (<see cref="CompanionAbility.From"/>).</summary>
    Training,
}

/// <param name="Name">Anzeigename (je Tradition, z. B. Geodensinn statt Hexensinn).</param>
/// <param name="Ap">Kosten; <c>null</c>, solange ein AP-Wert fehlt.</param>
public sealed record CompanionAbility(AnimalAbilityDefinition Definition, TraitEntry Entry, string Name, int Level, int? Ap,
    CompanionAbilitySource Source)
{
    /// <summary>Ausbildungsaufsatz, aus dem der Trick stammt.</summary>
    public AnimalAbilityDefinition? From { get; init; }
}

/// <param name="HeroAwards">Summe der AP-Gutschriften des Helden seit der Bindung bzw. Aufnahme.</param>
/// <param name="Share">Anteil des Tiers (ein Viertel, über die Summe abgerundet).</param>
/// <param name="Bonus">Bonus-AP nach Meisterentscheid und Startausbildung (Protokoll des Tiers).</param>
/// <param name="Spent">Ausgegebene AP des Tiers (ohne Loyalität, die der Held bezahlt).</param>
public sealed record CompanionAp(int HeroAwards, int Share, int Bonus, int Spent)
{
    public int Total => Share + Bonus;

    public int Remaining => Total - Spent;
}

/// <param name="Ap">Kosten beim Abspielen.</param>
/// <param name="HeroAp">Kosten trägt der Held (Loyalität).</param>
public sealed record CompanionEntryState(LogEntry Entry, string Text, int Ap, bool HeroAp);
