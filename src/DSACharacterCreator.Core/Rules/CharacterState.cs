using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Core.Rules;

/// <summary>Aus den Entscheidungen berechneter Stand eines Helden.</summary>
public sealed record CharacterState(
    ExperienceLevel ExperienceLevel,
    OriginState Origin,
    IReadOnlyList<AttributeState> Attributes,
    IReadOnlyList<TraitState> Traits,
    IReadOnlyList<TraitState> SpecialAbilities,
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

    /// <summary>AP aller Vorteile inkl. automatischer der Spezies (Grenze 80).</summary>
    public int AdvantageApTotal { get; init; }

    /// <summary>AP aller Nachteile inkl. automatischer der Spezies (negativ, Grenze −80).</summary>
    public int DisadvantageApTotal { get; init; }

    /// <summary>Berechnete Kosten des Professionspakets; <c>null</c> ohne Profession.</summary>
    public ProfessionCost? ProfessionCost { get; init; }
}

/// <summary>Gewählte Herkunft; jeweils <c>null</c>, solange nichts gewählt ist.</summary>
/// <param name="AttributeModifiers">Eigenschaftsänderungen der Spezies (verschieben die Höchstwerte; nur gewählte Auswahlen).</param>
public sealed record OriginState(
    SpeciesDefinition? Species,
    CultureDefinition? Culture,
    bool CulturePackage,
    ProfessionDefinition? Profession,
    ProfessionVariant? Variant,
    IReadOnlyDictionary<string, int> AttributeModifiers);

/// <param name="Value">Eigenschaftswert.</param>
/// <param name="Purchased">Gleich <paramref name="Value"/> (die Spezies verändert nicht den Wert).</param>
/// <param name="Modifier">Eigenschaftsänderung der Spezies – verschiebt nur den Höchstwert bei der Erschaffung.</param>
/// <param name="Min">Mindestwert.</param>
/// <param name="Max">Höchstwert, der gerade gilt (Erfahrungsgrad + Spezies-Änderung); <c>null</c> = unbegrenzt.</param>
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
/// <param name="Advantages">Gewählte Vorteile (ohne automatische der Spezies – die stecken im Spezies-AP-Wert).</param>
/// <param name="Disadvantages">Gewählte Nachteile (negativ: bringen AP).</param>
public sealed record ApAccount(int Total, int Species, int Attributes, int Skills, int CombatTechniques, int Unmodeled,
    int Advantages, int Disadvantages, int SpecialAbilities = 0)
{
    public int Spent => Species + Attributes + Skills + CombatTechniques + Unmodeled + Advantages + Disadvantages + SpecialAbilities;

    public int Remaining => Total - Spent;
}

/// <summary>Basiswerte. Werte mit Spezies-Grundwert sind <c>null</c>, solange keine Spezies gewählt ist.</summary>
/// <param name="Automatic">Automatischer Vor-/Nachteil der Spezies (nicht entfernbar, Kosten im Spezies-AP-Wert).</param>
/// <param name="Index">Position in <c>CreationLayer.Traits</c> bzw. <c>SpecialAbilities</c>; bei automatischen −1.</param>
/// <param name="Ap">Kosten; <c>null</c>, solange eine Angabe (Fertigkeit, Ausprägung, AP-Wert) fehlt.</param>
public sealed record TraitState(TraitDefinition Definition, TraitEntry Entry, int Index, int Level, int? Ap, bool Automatic,
    bool Available);

/// <param name="Astral">AsP-Grundwert ohne Leiteigenschaft der Tradition (folgt mit den Traditionen); <c>null</c> ohne Zauberer.</param>
/// <param name="Karma">KaP-Grundwert ohne Leiteigenschaft der Tradition; <c>null</c> ohne Geweihter.</param>
public sealed record DerivedValues(
    int? Astral,
    int? Karma,
    int? LifePoints,
    int? Spirit,
    int? Toughness,
    int? Speed,
    int Dodge,
    int Initiative,
    int FatePoints);
