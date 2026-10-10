using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Core.Rules;

/// <summary>Aus den Entscheidungen berechneter Stand eines Helden.</summary>
public sealed record CharacterState(
    ExperienceLevel ExperienceLevel,
    OriginState Origin,
    IReadOnlyList<AttributeState> Attributes,
    IReadOnlyList<SkillState> Skills,
    IReadOnlyList<CombatTechniqueState> CombatTechniques,
    ApAccount Ap,
    DerivedValues Derived,
    IReadOnlyList<Issue> Issues)
{
    public int Attribute(string id) => Attributes.First(a => a.Definition.Id == id).Value;

    public IEnumerable<Issue> IssuesFor(string targetId) => Issues.Where(i => i.TargetId == targetId);

    /// <summary>Berechnete Kosten des Kulturpakets (0 ohne Paket).</summary>
    public int CulturePackageAp { get; init; }

    /// <summary>Berechnete Kosten des Professionspakets; <c>null</c> ohne Profession.</summary>
    public ProfessionCost? ProfessionCost { get; init; }
}

/// <summary>Gewählte Herkunft; jeweils <c>null</c>, solange nichts gewählt ist.</summary>
/// <param name="AttributeModifiers">Eigenschaftsänderungen der Spezies (nur gewählte Auswahlen).</param>
public sealed record OriginState(
    SpeciesDefinition? Species,
    CultureDefinition? Culture,
    bool CulturePackage,
    ProfessionDefinition? Profession,
    ProfessionVariant? Variant,
    IReadOnlyDictionary<string, int> AttributeModifiers);

/// <param name="Value">Endwert inklusive Eigenschaftsänderung der Spezies.</param>
/// <param name="Purchased">Gekaufter Wert ohne Spezies-Änderung (Grundlage für Kosten und Eigenschaftssumme).</param>
/// <param name="Min">Mindestwert (Endwert).</param>
/// <param name="Max">Höchstwert (Endwert), der gerade gilt; <c>null</c> = unbegrenzt.</param>
public sealed record AttributeState(
    AttributeDefinition Definition,
    int Value,
    int Purchased,
    int Modifier,
    int Min,
    int? Max,
    int ApSpent);

/// <param name="PackageValue">Vorgabe aus Kultur- und Professionsvorlage (zur Anzeige; Kosten hängen nur vom Wert ab).</param>
/// <param name="ApSpent">AP vom Startwert bis zum aktuellen Wert.</param>
/// <param name="Available">Mindestens eine Publikation ist für den Helden aktiviert.</param>
public sealed record SkillState(SkillDefinition Definition, int Value, int PackageValue, int Max, int ApSpent, bool Available);

/// <param name="Parry"><c>null</c> bei Fernkampftechniken und Techniken ohne Parade.</param>
/// <param name="RangedAttack">Fernkampfwert, nur bei Fernkampftechniken.</param>
public sealed record CombatTechniqueState(
    CombatTechniqueDefinition Definition,
    int Value,
    int PackageValue,
    int Min,
    int Max,
    int ApSpent,
    bool Available,
    int? Attack,
    int? Parry,
    int? RangedAttack);

/// <summary>
/// AP-Konto nach Kategorien. Kultur und Profession sind nur Vorlagen: ihre Talente und Kampftechniken stecken in
/// <see cref="Skills"/> und <see cref="CombatTechniques"/> (Kosten vom Startwert bis zum aktuellen Wert).
/// </summary>
/// <param name="Unmodeled">Vorläufiger Anteil der Profession für noch nicht strukturierte Sonderfertigkeiten, Sprachen,
/// Zauber und Liturgien (Wiki-Rest, entfällt mit Meilenstein 4/6).</param>
public sealed record ApAccount(int Total, int Species, int Attributes, int Skills, int CombatTechniques, int Unmodeled)
{
    public int Spent => Species + Attributes + Skills + CombatTechniques + Unmodeled;

    public int Remaining => Total - Spent;
}

/// <summary>Basiswerte. Werte mit Spezies-Grundwert sind <c>null</c>, solange keine Spezies gewählt ist.</summary>
public sealed record DerivedValues(
    int? LifePoints,
    int? Spirit,
    int? Toughness,
    int? Speed,
    int Dodge,
    int Initiative,
    int FatePoints);
