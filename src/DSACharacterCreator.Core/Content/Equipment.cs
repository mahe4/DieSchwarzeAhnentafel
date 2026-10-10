namespace DSACharacterCreator.Core.Content;

/// <summary>„1W6+3“: Würfelanzahl, Seiten, fester Zuschlag.</summary>
public sealed record DamageRoll(int Dice, int Sides, int Bonus)
{
    public string Format(int extra = 0) =>
        $"{Dice}W{Sides}" + (Bonus + extra) switch { > 0 and var b => $"+{b}", < 0 and var b => $"−{-b}", _ => "" };
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
    IReadOnlyList<Publication> Publications);

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
    IReadOnlyList<Publication> Publications);
