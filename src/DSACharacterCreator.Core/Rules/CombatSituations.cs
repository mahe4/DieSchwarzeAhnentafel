namespace DSACharacterCreator.Core.Rules;

/// <summary>Ankreuzbarer Umstand einer Kampfprobe: „lang −4“ (Waffe des Gegners), „nah +2“ (Distanz).</summary>
/// <param name="Id">Gleich für denselben Umstand bei AT und PA, damit die Auswahl beim Wechsel erhalten bleibt.</param>
/// <param name="Modifier">Positiv = Erleichterung, negativ = Erschwernis.</param>
/// <param name="DamageBonus">Zusätzliche TP (Fernkampf nah +1, weit −1).</param>
public sealed record SituationOption(string Id, string Label, int Modifier, int DamageBonus = 0);

/// <param name="Exclusive">Höchstens eine Option (Distanz, Sicht …); sonst einzeln ankreuzbar.</param>
/// <param name="Note">Was sich nicht als Modifikator rechnen lässt (Sicht Stufe 4 …).</param>
public sealed record SituationGroup(string Title, IReadOnlyList<SituationOption> Options, bool Exclusive = true,
    string? Note = null);

/// <summary>
/// Übliche Modifikatoren für AT, PA, AW und FK aus den Grundregeln (Wiki: Nahkampfreichweite, Beengte Umgebung,
/// Größenkategorie, Vorteilhafte Position, Angriff von hinten, Sichtstörungen, Fernkampf mit Distanz, Größe, Bewegung,
/// Sicht, Fernkampf zu Pferd, Zielen, Schüsse ins Kampfgetümmel, Ausweichen und Parade mit dem Schild im Fernkampf;
/// wiederholte Verteidigung aus „Nahkampf“, Überlange Waffen). Neutrale Stufen (mittlere Distanz, klare Sicht …) haben
/// keine Option. Mit dem Helden kommen bei Verteidigungen die situativen Boni seiner Sonderfertigkeiten und Vorteile
/// dazu; Boni auf die Verteidigung (VW) gelten für PA und AW. Dauerhafte AW-Boni stecken schon im Wert
/// (<see cref="EquipmentState.DodgeBonus"/>).
/// </summary>
public static class CombatSituations
{
    /// <param name="hero">Stand des Helden für Sonderfertigkeiten, Vorteile und geführte Waffen; <c>null</c> = ohne.</param>
    public static IReadOnlyList<SituationGroup> For(CombatCheck check, CharacterState? hero = null) => check.Kind switch
    {
        CombatCheckKind.Attack => MeleeAttack(check),
        CombatCheckKind.RangedAttack => RangedAttack(),
        _ => [.. Defense(check, hero), .. hero is null ? [] : HeroDefense(check, hero)],
    };

    public static (int Modifier, int DamageBonus) Sum(IEnumerable<SituationOption> selected)
    {
        var list = selected.ToList();
        return (list.Sum(o => o.Modifier), list.Sum(o => o.DamageBonus));
    }

    /// <summary>kurz 0, mittel 1, lang 2; überlange Waffen folgen den Regeln langer Waffen. Sonst <c>null</c>.</summary>
    private static int? ReachRank(string? reach) => reach switch
    {
        "kurz" => 0,
        "mittel" => 1,
        "lang" or "überlang" => 2,
        _ => null,
    };

    private static IReadOnlyList<SituationGroup> MeleeAttack(CombatCheck check)
    {
        var groups = new List<SituationGroup>();
        // Nahkampfreichweite: kurz gegen mittel −2, gegen lang −4; mittel gegen lang −2 (nur auf die AT der kürzeren Waffe)
        if (ReachRank(check.Reach) is { } rank and < 2)
            groups.Add(new SituationGroup("Waffe des Gegners",
            [
                .. rank < 1 ? [new SituationOption("enemy-reach-medium", "mittel", -2)] : Array.Empty<SituationOption>(),
                new SituationOption("enemy-reach-long", "lang", rank == 0 ? -4 : -2),
            ]));
        if (Cramped(check, attack: true) is { } cramped) groups.Add(cramped);
        groups.Add(new SituationGroup("Größe des Gegners", [new SituationOption("target-tiny", "winzig", -4)],
            Note: "Gegen große Gegner nur Parade mit dem Schild oder Ausweichen, gegen riesige nur Ausweichen."));
        groups.Add(MeleeSight(attack: true));
        groups.Add(new SituationGroup("Sonstiges", [Advantage], Exclusive: false));
        return groups;
    }

    private static IReadOnlyList<SituationGroup> Defense(CombatCheck check, CharacterState? hero)
    {
        var groups = new List<SituationGroup>
        {
            new("Verteidigung in dieser KR",
            [
                new SituationOption("defense-2", "2.", -3), new SituationOption("defense-3", "3.", -6),
                new SituationOption("defense-4", "4.", -9), new SituationOption("defense-5", "5.", -12),
            ]),
        };
        // Fernkampf: nur Ausweichen oder Parade mit dem Schild
        if (check.Kind == CombatCheckKind.Dodge || check.Shield)
            groups.Add(new SituationGroup("Fernkampfangriff",
                [new SituationOption("ranged-shot", "Schusswaffe", -4), new SituationOption("ranged-thrown", "Wurfwaffe", -2)]));
        if (check.Kind == CombatCheckKind.Parry && Cramped(check, attack: false) is { } cramped) groups.Add(cramped);
        groups.Add(MeleeSight(attack: false));
        // Überlange Waffen: Ausweichen nur nach Loslassen der Waffe oder mit −4
        var overlong = check.Kind == CombatCheckKind.Dodge &&
                       hero?.Equipment.Weapons.Any(w => w.Entry.Equipped && w.Definition.Reach == "überlang") == true;
        groups.Add(new SituationGroup("Sonstiges",
        [
            Advantage, new SituationOption("from-behind", "Angriff von hinten", -4),
            .. overlong ? [new SituationOption("overlong", "überlange Waffe in der Hand", -4)] : Array.Empty<SituationOption>(),
        ], Exclusive: false));
        return groups;
    }

    /// <summary>Situativer Bonus einer Sonderfertigkeit bzw. eines Vorteils (Werte aus dem Regeltext im Wiki).</summary>
    /// <param name="DodgeOnly">Nur auf AW („+2 auf AW“); sonst auf die Verteidigung (VW = PA und AW).</param>
    /// <param name="Style">Wirkt bei der Parade nur mit den Kampftechniken der SF (Kampfstile, Verteidigungshaltung).</param>
    /// <param name="PerLevel">Modifikator je Stufe der SF.</param>
    private sealed record AbilityBonus(string Ability, string Group, string Id, string Label, int Modifier, bool DodgeOnly = false,
        bool Style = false, bool PerLevel = false);

    private const string Abilities = "Sonderfertigkeiten und Vorteile";
    private const string Formations = "Formation";
    private const string OwnManeuver = "Nach eigenem Manöver (bis Ende der nächsten KR)";

    private static readonly AbilityBonus[] AbilityBoni =
    [
        new("sf.verteidigungshaltung", Abilities, "defensive-stance", "Verteidigungshaltung", 4, Style: true),
        new("sf.fedorino-stil", Abilities, "fedorino", "Fedorino-Stil (ohne Spezialmanöver)", 2, Style: true),
        new("sf.unauer-schule", Abilities, "unau", "Unauer Schule (nicht bewegt)", 2, Style: true),
        new("sf.blitzreflexe", Abilities, "lightning-reflexes", "Blitzreflexe (1. KR)", 2),
        new("sf.rhodenstein-stil-passiv", Abilities, "rhodenstein", "Rhodenstein-Stil (1. KR, statt +1 AT)", 2, Style: true),
        new("sf.odilmar-stil", Abilities, "odilmar", "Odilmar-Stil gegen Fernkampf", 2, DodgeOnly: true),
        new("adv.elementare-verteidigung", Abilities, "elemental-defense", "Elementare Verteidigung", 2, DodgeOnly: true),
        new("sf.schutz-der-muttersau", Abilities, "sow-protection", "Schutz der Muttersau", 1, DodgeOnly: true),
        new("sf.plaenkler-formation", Formations, "formation-skirmish", "Plänkler-Formation", 1),
        new("sf.formation", Formations, "formation", "Formation", 2),
        new("sf.gefechtsformation", Formations, "formation-battle", "Gefechtsformation", 2),
        new("sf.schildwall", Formations, "shield-wall", "Schildwall", 2),
        new("sf.schildwall", Formations, "shield-wall-ranged", "Schildwall gegen Fernkampf", 6),
        new("sf.kha-formation", Formations, "kha", "Kha-Formation", 3),
        new("sf.kha-formation", Formations, "kha-ranged", "Kha-Formation gegen Fernkampf", 9),
        new("sf.blutgraetsche", OwnManeuver, "after-slide", "Blutgrätsche", -2),
        new("sf.klinge-drehen", OwnManeuver, "after-twist", "Klinge drehen", -2),
        new("sf.machtvoller-sturmangriff", OwnManeuver, "after-charge", "Machtvoller Sturmangriff misslungen", -2),
        new("sf.durchgezogener-tritt", OwnManeuver, "after-kick", "Durchgezogener Tritt", -1, PerLevel: true),
    ];

    /// <summary>
    /// Boni und Abzüge aus Sonderfertigkeiten und Vorteilen des Helden. Formationen erscheinen nur, wenn der Held die SF
    /// selbst hat (Boni durch Mitstreiter über den freien Modifikator).
    /// </summary>
    private static IEnumerable<SituationGroup> HeroDefense(CombatCheck check, CharacterState hero)
    {
        var owned = hero.SpecialAbilities.Concat(hero.Traits).GroupBy(t => t.Definition.Id)
            .ToDictionary(g => g.Key, g => g.First());
        bool Fits(AbilityBonus b)
        {
            if (!owned.TryGetValue(b.Ability, out var t)) return false;
            if (b.DodgeOnly) return check.Kind == CombatCheckKind.Dodge;
            if (!b.Style || check.Kind != CombatCheckKind.Parry) return true;
            var techniques = (t.Definition.CombatTechniquesText ?? "")
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return techniques.Length == 0 || techniques.Contains("alle", StringComparer.OrdinalIgnoreCase) ||
                   techniques.Contains(check.Technique, StringComparer.OrdinalIgnoreCase);
        }
        return AbilityBoni.Where(Fits).GroupBy(b => b.Group).Select(g => new SituationGroup(g.Key,
            g.Select(b => new SituationOption(b.Id, b.Label, b.PerLevel ? b.Modifier * owned[b.Ability].Level : b.Modifier)).ToList(),
            Exclusive: g.Key == Formations));
    }

    private static IReadOnlyList<SituationGroup> RangedAttack() =>
    [
        new("Distanz", [new SituationOption("range-near", "nah", 2, 1), new SituationOption("range-far", "weit", -2, -1)]),
        new("Größe des Ziels",
        [
            new SituationOption("target-tiny", "winzig", -8), new SituationOption("target-small", "klein", -4),
            new SituationOption("target-large", "groß", 4), new SituationOption("target-huge", "riesig", 8),
        ]),
        new("Bewegung des Ziels",
            [new SituationOption("target-still", "steht still", 2), new SituationOption("target-fast", "bewegt sich schnell", -2)]),
        new("Schütze", [new SituationOption("shooter-walk", "geht", -2), new SituationOption("shooter-run", "rennt", -4)]),
        new("Zielen", [new SituationOption("aim-1", "1 Aktion", 2), new SituationOption("aim-2", "2 Aktionen", 4)]),
        new("Sicht",
        [
            new SituationOption("sight-1", "Stufe 1", -2), new SituationOption("sight-2", "Stufe 2", -4),
            new SituationOption("sight-3", "Stufe 3", -6),
        ], Note: "Stufe 4 (Ziel unsichtbar): Glückstreffer nur bei einer 1."),
        new("Vom Reittier",
            [new SituationOption("mount-walk", "Schritt", -4), new SituationOption("mount-gallop", "Galopp", -8)],
            Note: "Im Trab Glückstreffer nur bei einer 1; kein Langbogen vom Pferd."),
        new("Sonstiges",
            [new SituationOption("target-zigzag", "Ziel schlägt Haken", -4), new SituationOption("melee", "ins Kampfgetümmel", -2)],
            Exclusive: false),
    ];

    private static readonly SituationOption Advantage = new("advantage", "vorteilhafte Position", 2);

    /// <summary>Sichtstörungen Stufe 1–3: −1 bis −3 auf AT und Verteidigung.</summary>
    private static SituationGroup MeleeSight(bool attack) => new("Sicht",
    [
        new SituationOption("sight-1", "Stufe 1", -1), new SituationOption("sight-2", "Stufe 2", -2),
        new SituationOption("sight-3", "Stufe 3", -3),
    ], Note: attack ? "Stufe 4 (Ziel unsichtbar): AT halbiert." : "Stufe 4 (Ziel unsichtbar): nur bei einer 1.");

    /// <summary>
    /// Beengte Umgebung: kurze Waffen ±0, mittlere −4, lange −8 auf AT und PA; Schilde klein −2/−2, mittel −4/−3,
    /// groß −6/−4. <c>null</c>, wenn es keinen Abzug gibt oder die Waffe unbekannt ist.
    /// </summary>
    private static SituationGroup? Cramped(CombatCheck check, bool attack)
    {
        int? modifier = check.ShieldSize switch
        {
            "klein" => -2,
            "mittel" => attack ? -4 : -3,
            "groß" => attack ? -6 : -4,
            _ => ReachRank(check.Reach) switch { 1 => -4, 2 => -8, var r => r is null ? null : 0 },
        };
        return modifier is { } m and not 0
            ? new SituationGroup("Umgebung", [new SituationOption("cramped", "beengt", m)], Exclusive: false)
            : null;
    }
}
