namespace DSACharacterCreator.Core.Content;

/// <summary>„1W6+3“: Würfelanzahl, Seiten, fester Zuschlag.</summary>
public sealed record DamageRoll(int Dice, int Sides, int Bonus)
{
    public string Format(int extra = 0) =>
        $"{Dice}W{Sides}" + (Bonus + extra) switch { > 0 and var b => $"+{b}", < 0 and var b => $"−{-b}", _ => "" };
}

/// <summary>Gemeinsames aller Gegenstände: Name, Gewicht und Preis (Wiki-Text und als Zahl).</summary>
public interface IPricedItem
{
    string Id { get; }
    string Name { get; }
    string Category { get; }
    string? Weight { get; }
    string? Price { get; }

    /// <summary>Gewicht in Stein; <c>null</c>, wenn das Wiki keines nennt („je nach Waffe“).</summary>
    decimal? WeightStone { get; }

    /// <summary>Preis in Silbertalern (1 D = 10 S, 1 H = 0,1 S, 1 K = 0,01 S); <c>null</c> bei „unterschiedlich“.</summary>
    decimal? PriceSilver { get; }

    IReadOnlyList<Publication> Publications { get; }
}

/// <summary>Waffe aus der Rüstkammer des Regel-Wikis.</summary>
/// <param name="DamageThresholds">Leiteigenschaft(en) mit Schadensschwelle („GE/KK 15“, „GE 15/KK 14“): je Punkt der
/// Eigenschaft über der Schwelle +1 TP; <c>null</c> im Fernkampf.</param>
/// <param name="NoParry">Mit dieser Waffe ist keine Parade möglich (AT/PA-Mod „0/-“).</param>
public sealed record WeaponDefinition(
    string Id,
    string Name,
    string Category,
    string? CombatTechnique,
    string DamageText,
    DamageRoll? Damage,
    IReadOnlyDictionary<string, int>? DamageThresholds,
    int? AttackModifier,
    int? ParryModifier,
    bool NoParry,
    string? Reach,
    bool Ranged,
    string? LoadTime,
    string? Ranges,
    string? Ammunition,
    string? Weight,
    string? Length,
    string? Price,
    string? Complexity,
    string? AdvantageText,
    string? DisadvantageText,
    string? Note,
    string? Distribution,
    bool TwoHanded,
    bool Improvised,
    IReadOnlyList<Publication> Publications,
    decimal? WeightStone = null,
    decimal? PriceSilver = null) : IPricedItem;

/// <summary>Rüstung aus der Rüstkammer des Regel-Wikis.</summary>
/// <param name="Encumbrance">Belastungsstufe der Rüstung.</param>
/// <param name="SpeedPenalty">Zusätzlicher Abzug auf GS (negativ), z. B. Lederrüstung −1.</param>
/// <param name="InitiativePenalty">Zusätzlicher Abzug auf INI (negativ).</param>
public sealed record ArmorDefinition(
    string Id,
    string Name,
    string Category,
    int Protection,
    int Encumbrance,
    string? ExtraPenaltiesText,
    int? SpeedPenalty,
    int? InitiativePenalty,
    string? Weight,
    string? Price,
    string? Complexity,
    string? AdvantageText,
    string? DisadvantageText,
    string? Note,
    string? Distribution,
    IReadOnlyList<Publication> Publications,
    decimal? WeightStone = null,
    decimal? PriceSilver = null) : IPricedItem;

/// <summary>Sonstiger Gegenstand aus der Rüstkammer (Ausrüstung, Kleidung, Waffenausrüstung …).</summary>
/// <param name="RuleText">Regeltechnik bzw. Regel laut Wiki.</param>
public sealed record ItemDefinition(
    string Id,
    string Name,
    string Category,
    string? Weight,
    decimal? WeightStone,
    string? Price,
    decimal? PriceSilver,
    string? StructurePoints,
    string? Complexity,
    string? RuleText,
    string? Note,
    IReadOnlyList<Publication> Publications) : IPricedItem;

/// <summary>Zeile eines Ausrüstungspakets: ein Gegenstand mit Gewicht und Preis laut Paketliste.</summary>
/// <param name="Ref">Gegenstand, Waffe oder Rüstung mit gleichem Namen; <c>null</c>, wenn es keinen gibt.</param>
public sealed record PackageItem(string Name, string? Ref, decimal? WeightStone, decimal? PriceSilver);

/// <summary>Ausrüstungspaket. Der Preis ergibt sich aus den Zeilen; <see cref="WikiPrice"/> dient nur dem Vergleich.</summary>
public sealed record EquipmentPackageDefinition(
    string Id,
    string Name,
    IReadOnlyList<PackageItem> Items,
    string? WeightText,
    string? PriceText,
    decimal? WikiPrice,
    IReadOnlyList<Publication> Publications)
{
    public decimal Price => Items.Sum(i => i.PriceSilver ?? 0);

    public decimal Weight => Items.Sum(i => i.WeightStone ?? 0);
}
