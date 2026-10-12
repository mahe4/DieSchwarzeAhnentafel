using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Core.Rules;

/// <summary>Probeneigenschaft mit Wert: „MU 14“.</summary>
public sealed record CheckAttribute(string Short, int Value);

/// <summary>
/// Fertigkeitsprobe (Talent, Zauber, Liturgie …): drei Eigenschaften und der Fertigkeitswert.
/// </summary>
/// <param name="DefaultModifier">Vorgabe für den Modifikator, z. B. −2 durch Belastung bei Talenten mit Belastung.</param>
/// <param name="Note">Hinweis zur Probe, z. B. „modifiziert durch SK“ oder die Belastung.</param>
public sealed record SkillCheck(string Name, IReadOnlyList<CheckAttribute> Attributes, int SkillValue, int DefaultModifier = 0,
    string? Note = null);

public enum CheckOutcome
{
    Success,
    Failure,
    /// <summary>Doppel-1 (Fertigkeitsprobe) bzw. bestätigte 1 (Kampf).</summary>
    Critical,
    /// <summary>Dreifach-1.</summary>
    SpectacularCritical,
    /// <summary>Kampf: 1 gewürfelt, Bestätigung misslungen (trotzdem gelungen, bei der Attacke Verteidigung halbiert).</summary>
    CriticalUnconfirmed,
    /// <summary>Doppel-20 (Fertigkeitsprobe) bzw. bestätigte 20 (Kampf).</summary>
    Botch,
    /// <summary>Dreifach-20.</summary>
    SpectacularBotch,
    /// <summary>Kampf: 20 gewürfelt, Bestätigung gelungen (gewöhnliches Misslingen).</summary>
    BotchUnconfirmed,
    /// <summary>Ein effektiver Wert liegt unter 1: keine Probe möglich.</summary>
    Impossible,
}

/// <param name="Dice">Die drei W20.</param>
/// <param name="Targets">Effektive Eigenschaftswerte (mit Modifikator).</param>
/// <param name="Points">Übrig behaltene Fertigkeitspunkte (negativ = gescheitert).</param>
/// <param name="Quality">Qualitätsstufe 1–6; 0 bei Misslingen.</param>
public sealed record SkillCheckResult(IReadOnlyList<int> Dice, IReadOnlyList<int> Targets, int Points, int Quality, CheckOutcome Outcome)
{
    public bool Succeeded => Outcome is CheckOutcome.Success or CheckOutcome.Critical or CheckOutcome.SpectacularCritical;
}

public enum CombatCheckKind { Attack, RangedAttack, Parry, Dodge }

/// <summary>Probe auf einen Kampfwert: „AT 14“, „PA 8“, „FK 13“.</summary>
/// <param name="Label">„AT“, „AT beidhändig“, „PA falsche Hand“ …</param>
/// <param name="Damage">TP des Angriffs mit Schadensbonus; <c>null</c> bei Verteidigung oder unbekanntem Schaden.</param>
/// <param name="Reach">Reichweite der Nahkampfwaffe („kurz“, „mittel“, „lang“, „überlang“); <c>null</c> bei Kampftechniken
/// ohne Waffe. Bestimmt die Umstände in <see cref="CombatSituations"/>.</param>
/// <param name="ShieldSize">Größe des Schilds, wenn mit ihm angegriffen oder pariert wird.</param>
/// <param name="Shield">Parade mit dem Schild: auch gegen Fernkampf möglich.</param>
/// <param name="Technique">Name der Kampftechnik („Fechtwaffen“) für Kampfstile, die nur mit ihr wirken; <c>null</c> bei AW.</param>
public sealed record CombatCheck(string Label, int Value, CombatCheckKind Kind, DamageRoll? Damage = null, string? Reach = null,
    string? ShieldSize = null, bool Shield = false, string? Technique = null)
{
    public bool IsAttack => Kind is CombatCheckKind.Attack or CombatCheckKind.RangedAttack;
}

/// <param name="Roll">Der W20 der Probe.</param>
/// <param name="Target">Effektiver Wert (mit Modifikator).</param>
/// <param name="Confirmation">Bestätigungswurf nach einer 1 oder 20; sonst <c>null</c>.</param>
public sealed record CombatCheckResult(int Roll, int Target, int? Confirmation, CheckOutcome Outcome)
{
    public bool Succeeded => Outcome is CheckOutcome.Success or CheckOutcome.Critical or CheckOutcome.CriticalUnconfirmed;
}

/// <param name="Dice">Gewürfelte Augen.</param>
/// <param name="Total">Summe mit Zuschlag, bei einem kritischen Treffer verdoppelt (mindestens 0).</param>
public sealed record DamageResult(DamageRoll Roll, IReadOnlyList<int> Dice, bool Doubled, int Total);

/// <summary>
/// Würfelproben nach den Grundregeln (Wiki: Fertigkeitsproben, Eigenschaftsprobe, Nahkampf, Fernkampf). Die Würfe werden
/// getrennt von der Auswertung erzeugt, damit ein geänderter Modifikator dieselben Würfel neu auswertet.
/// Modifikator: positiv = Erleichterung, negativ = Erschwernis.
/// </summary>
public static class CheckRolls
{
    /// <summary>Wiki (Kampf, Patzer bestätigt): „Der Held erleidet 1W6+2 SP.“</summary>
    public static readonly DamageRoll BotchDamage = new(1, 6, 2);

    public static int D20(Random random) => random.Next(1, 21);

    public static int[] Roll(Random random, int count, int sides) =>
        Enumerable.Range(0, count).Select(_ => random.Next(1, sides + 1)).ToArray();

    /// <summary>Wiki (Qualitätsstufen): FP 0–3 → QS 1, 4–6 → 2, 7–9 → 3, 10–12 → 4, 13–15 → 5, 16+ → 6.</summary>
    public static int Quality(int points) => points < 0 ? 0 : Math.Min(6, Math.Max(0, points - 1) / 3 + 1);

    /// <summary>
    /// Fertigkeitsprobe: Jeder Wurf über dem effektiven Eigenschaftswert kostet die Differenz an Fertigkeitspunkten. Mit 0 FP
    /// bestanden zählt als 1 FP. Doppel-1 gelingt immer (kritisch), Doppel-20 misslingt immer (Patzer), keine
    /// Bestätigungswürfe. Sinkt eine Eigenschaft durch Erschwernisse auf 0 oder weniger, ist keine Probe möglich.
    /// </summary>
    public static SkillCheckResult Evaluate(SkillCheck check, int modifier, IReadOnlyList<int> dice)
    {
        var targets = check.Attributes.Select(a => a.Value + modifier).ToList();
        if (targets.Any(t => t < 1)) return new SkillCheckResult(dice, targets, 0, 0, CheckOutcome.Impossible);
        var points = check.SkillValue - dice.Zip(targets, (d, t) => Math.Max(0, d - t)).Sum();
        var ones = dice.Count(d => d == 1);
        var twenties = dice.Count(d => d == 20);
        var outcome = ones >= 2 ? (ones == 3 ? CheckOutcome.SpectacularCritical : CheckOutcome.Critical)
            : twenties >= 2 ? (twenties == 3 ? CheckOutcome.SpectacularBotch : CheckOutcome.Botch)
            : points >= 0 ? CheckOutcome.Success : CheckOutcome.Failure;
        var succeeded = outcome is CheckOutcome.Success or CheckOutcome.Critical or CheckOutcome.SpectacularCritical;
        return new SkillCheckResult(dice, targets, points, succeeded ? Quality(Math.Max(0, points)) : 0, outcome);
    }

    /// <summary>
    /// Probe auf AT, PA, FK oder Ausweichen: höchstens den Wert würfeln. Eine 1 ist ein möglicher Kritischer Erfolg, eine 20
    /// ein möglicher Patzer; beide werden mit einem zweiten W20 gegen denselben Wert bestätigt (eine 20 bestätigt den Patzer
    /// immer). Die 1 gelingt auch unbestätigt, die 20 misslingt immer.
    /// </summary>
    /// <param name="confirmation">Vorab gewürfelter Bestätigungswurf; zählt nur nach einer 1 oder 20.</param>
    public static CombatCheckResult Evaluate(int value, int modifier, int roll, int confirmation)
    {
        var target = value + modifier;
        if (target < 1) return new CombatCheckResult(roll, target, null, CheckOutcome.Impossible);
        return roll switch
        {
            1 => new CombatCheckResult(roll, target, confirmation,
                confirmation <= target && confirmation != 20 ? CheckOutcome.Critical : CheckOutcome.CriticalUnconfirmed),
            20 => new CombatCheckResult(roll, target, confirmation,
                confirmation > target || confirmation == 20 ? CheckOutcome.Botch : CheckOutcome.BotchUnconfirmed),
            _ => new CombatCheckResult(roll, target, null, roll <= target ? CheckOutcome.Success : CheckOutcome.Failure),
        };
    }

    /// <summary>Trefferpunkte; ein bestätigter kritischer Treffer verdoppelt sie samt aller Modifikatoren.</summary>
    public static DamageResult Damage(DamageRoll roll, IReadOnlyList<int> dice, bool doubled)
    {
        var sum = dice.Take(roll.Dice).Sum() + roll.Bonus;
        return new DamageResult(roll, dice.Take(roll.Dice).ToList(), doubled, Math.Max(0, doubled ? sum * 2 : sum));
    }
}
