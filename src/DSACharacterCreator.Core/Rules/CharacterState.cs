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

    /// <summary>Zugekaufte LeP/AsP/KaP.</summary>
    public IReadOnlyList<EnergyState> Energies { get; init; } = [];

    /// <summary>Waffen, Rüstung, Belastung und Kampfwerte mit Ausrüstung.</summary>
    public EquipmentState Equipment { get; init; } = EquipmentState.Empty;

    /// <summary>Zauber, Rituale, Liturgien … in der Reihenfolge von <c>CreationLayer.Spells</c>.</summary>
    public IReadOnlyList<SpellState> Spells { get; init; } = [];

    /// <summary>Protokoll mit Kosten und Konflikten; <c>null</c>, solange es keine Einträge gibt.</summary>
    public AdvancementInfo? Advancement { get; init; }
}

/// <param name="Available">Der Held verfügt über diese Energie (AsP nur mit Zauberer, KaP nur mit Geweihter).</param>
/// <param name="Max">Höchstzahl zugekaufter Punkte (KO bzw. Leiteigenschaft); <c>null</c> = nicht bestimmbar.</param>
/// <param name="Index">Position in <c>CreationLayer.Spells</c>.</param>
/// <param name="Value">Fertigkeitswert; <c>null</c> bei Zaubertricks und Segnungen.</param>
/// <param name="Min">Untergrenze beim Senken (in der Steigerungsphase der Wert am Ende der Erschaffung).</param>
/// <param name="Foreign">Fremdzauber (nicht in der Verbreitung der eigenen Tradition).</param>
public sealed record SpellState(SpellDefinition Definition, SpellEntry Entry, int Index, int? Value, int Min, int? Max, int Ap,
    bool Foreign, bool Available);

public sealed record EnergyState(string Id, string Name, int Value, bool Available, int? Max, int ApSpent,
    EnergyKind Kind = EnergyKind.Bought)
{
    /// <summary>Bei Verlusten: Anteil, der sich aus Sonderfertigkeiten ergibt (Bindungen) und nicht von Hand änderbar ist.</summary>
    public int Fixed { get; init; }
}

public enum EnergyKind
{
    /// <summary>Zukauf (Kostentabelle D).</summary>
    Bought,
    /// <summary>Rückkauf permanent verlorener Punkte (2 AP je Punkt).</summary>
    Repurchase,
    /// <summary>Permanent verlorene Punkte (ohne AP).</summary>
    Lost,
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
/// <param name="Min">Untergrenze beim Senken (Startwert; in der Steigerungsphase der Wert am Ende der Erschaffung).</param>
/// <param name="Purchased">Gleich <paramref name="Value"/> (die Spezies verändert nicht den Wert).</param>
/// <param name="Modifier">Eigenschaftsänderung der Spezies – verschiebt nur den Höchstwert bei der Erschaffung.</param>
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
public sealed record SkillState(SkillDefinition Definition, int Value, int PackageValue, int Max, int ApSpent, bool Available)
{
    /// <summary>Untergrenze beim Senken: in der Steigerungsphase der Wert am Ende der Erschaffung.</summary>
    public int Min { get; init; }
}

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
    /// <summary>Zugekaufte LeP/AsP/KaP.</summary>
    public int Energies { get; init; }

    /// <summary>Zauber, Rituale, Zaubertricks, magische Handlungen.</summary>
    public int Spells { get; init; }

    /// <summary>Liturgien, Zeremonien, Segnungen.</summary>
    public int Liturgies { get; init; }

    public int Spent => Species + Attributes + Skills + CombatTechniques + Unmodeled + Advantages + Disadvantages + SpecialAbilities +
                        Energies + Spells + Liturgies;

    public int Remaining => Total - Spent;
}

/// <summary>Basiswerte. Werte mit Spezies-Grundwert sind <c>null</c>, solange keine Spezies gewählt ist.</summary>
/// <param name="Automatic">Automatischer Vor-/Nachteil der Spezies (nicht entfernbar, Kosten im Spezies-AP-Wert).</param>
/// <param name="Index">Position in <c>CreationLayer.Traits</c> bzw. <c>SpecialAbilities</c>; bei automatischen −1.</param>
/// <param name="Ap">Kosten; <c>null</c>, solange eine Angabe (Fertigkeit, Ausprägung, AP-Wert) fehlt.</param>
public sealed record TraitState(TraitDefinition Definition, TraitEntry Entry, int Index, int Level, int? Ap, bool Automatic,
    bool Available);

/// <param name="Astral">AsP-Basiswert (Grundwert Zauberer + Leiteigenschaft der Tradition ± Vor-/Nachteile); <c>null</c> ohne Zauberer.</param>
/// <param name="Karma">KaP-Basiswert (Grundwert Geweihter + Leiteigenschaft der Tradition ± Vor-/Nachteile); <c>null</c> ohne Geweihter.</param>
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

/// <param name="Index">Position in <c>Character.Equipment</c>.</param>
public sealed record ArmorState(ArmorDefinition Definition, EquipmentEntry Entry, int Index);

/// <param name="Attack">AT mit Waffe (Kampftechnik + Waffenmodifikator − Belastung); <c>null</c> im Fernkampf.</param>
/// <param name="Parry">PA mit Waffe; <c>null</c>, wenn die Kampftechnik keine Parade kennt.</param>
/// <param name="RangedAttack">FK der Kampftechnik (Fernkampfwaffen).</param>
/// <param name="DamageText">TP mit Schadensbonus, z. B. „1W6+5“.</param>
/// <param name="DamageBonus">Schadensbonus: Punkte der Leiteigenschaft über der Schadensschwelle.</param>
public sealed record WeaponState(WeaponDefinition Definition, EquipmentEntry Entry, int Index, int? Attack, int? Parry,
    int? RangedAttack, string DamageText, int DamageBonus)
{
    /// <summary>Geführter Schild bzw. Parierwaffe, deren passiver Parade-Bonus in <see cref="Parry"/> steckt.</summary>
    public WeaponDefinition? ParryBonusFrom { get; init; }

    /// <summary>AT-Abzug durch einen großen Schild in der anderen Hand (in <see cref="Attack"/> enthalten).</summary>
    public int ShieldAttackModifier { get; init; }
}

/// <summary>Eine Zeile der Ausrüstungsliste mit Gewicht und Preis je Stück (eigener Wert vor Regelwert).</summary>
/// <param name="Definition">Waffe, Rüstung oder Gegenstand; <c>null</c> bei eigenen Gegenständen.</param>
public sealed record EquipmentLine(IPricedItem? Definition, EquipmentEntry Entry, int Index, string Name, int Quantity,
    decimal? Weight, decimal? Price);

/// <summary>Ausrüstung und ihre Auswirkungen.</summary>
/// <param name="WornArmor">Angelegte Rüstung; <c>null</c> ohne Rüstung.</param>
/// <param name="ArmorEncumbrance">Belastungsstufe der Rüstung laut Regeldaten (Ausgangswert).</param>
/// <param name="Habituation">Stufe der SF Belastungsgewöhnung.</param>
/// <param name="Encumbrance">Belastung, die tatsächlich wirkt (abgeleiteter Wert): Rüstung nach Belastungsgewöhnung
/// plus Traglast.</param>
/// <param name="Initiative">INI mit Belastung und zusätzlichen Abzügen.</param>
/// <param name="Speed">GS mit Belastung und zusätzlichen Abzügen; <c>null</c> ohne Spezies.</param>
/// <param name="Dodge">Ausweichen mit Belastung.</param>
public sealed record EquipmentState(
    IReadOnlyList<ArmorState> Armors,
    IReadOnlyList<WeaponState> Weapons,
    ArmorDefinition? WornArmor,
    int Protection,
    int ArmorEncumbrance,
    int Habituation,
    int Encumbrance,
    int SpeedPenalty,
    int InitiativePenalty,
    int Initiative,
    int? Speed,
    int Dodge)
{
    public static EquipmentState Empty { get; } = new([], [], null, 0, 0, 0, 0, 0, 0, 0, null, 0);

    /// <summary>Alle Einträge von <c>Character.Equipment</c> in gleicher Reihenfolge.</summary>
    public IReadOnlyList<EquipmentLine> Lines { get; init; } = [];

    /// <summary>Belastung durch die Rüstung nach Belastungsgewöhnung.</summary>
    public int ArmorLevel { get; init; }

    /// <summary>Getragenes Gewicht in Stein (ohne angelegte Rüstung).</summary>
    public decimal CarriedWeight { get; init; }

    /// <summary>Traglast ohne Belastung: KK × 2 Stein.</summary>
    public int CarryCapacity { get; init; }

    /// <summary>Belastung durch Traglast: je 4 volle Stein über der Traglast eine Stufe.</summary>
    public int LoadLevel { get; init; }

    /// <summary>Startkapital in Silbertalern (mit Reich/Arm).</summary>
    public decimal StartingMoney { get; init; }

    /// <summary>Stufen Reich minus Stufen Arm.</summary>
    public int WealthLevel { get; init; }

    /// <summary>Summe der gekauften Ausrüstung.</summary>
    public decimal Spent { get; init; }

    /// <summary>Summe der Einnahmen und Ausgaben (<c>Character.Money</c>).</summary>
    public decimal Booked { get; init; }

    /// <summary>Verfügbares Geld: Startkapital + Buchungen − Gekauftes.</summary>
    public decimal Money { get; init; }
}
