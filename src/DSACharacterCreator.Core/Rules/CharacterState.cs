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

/// <param name="PackageValue">Wert aus Kultur- und Professionspaket.</param>
/// <param name="ApSpent">AP für die Abweichung vom Paketwert (negativ = Ersparnis durch Senken).</param>
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

/// <param name="Origin">Spezies, Kulturpaket und Professionspaket.</param>
public sealed record ApAccount(int Total, int Origin, int Attributes, int Skills, int CombatTechniques)
{
    public int Spent => Origin + Attributes + Skills + CombatTechniques;

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
