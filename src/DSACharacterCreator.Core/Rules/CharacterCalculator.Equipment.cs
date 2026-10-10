using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Core.Rules;

/// <summary>Ausrüstung: Kampfwerte mit Waffe, Rüstungsschutz, Belastung durch Rüstung und Traglast, Geld.</summary>
public sealed partial class CharacterCalculator
{
    /// <summary>Traglast ohne Belastung: KK × 2 Stein; je 4 volle weitere Stein eine Stufe Belastung
    /// (Wiki: Tragkraft und Stemmen).</summary>
    public const int CarryPerStrength = 2;

    public const int StonesPerLoadLevel = 4;

    /// <summary>
    /// Belastung durch die angelegte Rüstung (Wiki: Zustand Belastung; SF Belastungsgewöhnung): Stufe I–III gibt je
    /// −1 auf AT, Verteidigung (Parade, Ausweichen), INI und GS sowie auf Talente mit „Belastung: ja“; Stufe IV ist
    /// handlungsunfähig. Belastungsgewöhnung lässt die Auswirkungen in der Rüstungstabelle um zwei Zeilen je Stufe nach
    /// oben wandern (BE-Spalte 0,0,1,1,2,2,3; zusätzliche Abzüge in jeder zweiten Zeile): das ist BE −1 je Stufe, und
    /// zusätzliche Abzüge entfallen erst, wenn die Stufe die BE der Rüstung übersteigt.
    /// Dazu kommt Belastung durch Traglast; das Gewicht der angelegten Rüstung zählt dabei nicht (Wiki: Belastung).
    /// </summary>
    private EquipmentState Equipment(Character character, IReadOnlyDictionary<string, int> attr, List<TraitState> traits,
        List<TraitState> specialAbilities, IReadOnlyList<CombatTechniqueState> combatTechniques, DerivedValues derived,
        SourceSelection sources, List<Issue> issues)
    {
        var armors = new List<ArmorState>();
        var weapons = new List<WeaponState>();
        var lines = new List<EquipmentLine>();
        for (var i = 0; i < character.Equipment.Count; i++)
        {
            var entry = character.Equipment[i];
            var target = EquipmentTarget(i);
            var def = entry.Id.Length > 0 ? rules.FindAnyItem(entry.Id) : null;
            if (def is not null && !sources.IsAvailable(def.Publications)) issues.Add(SourceDisabled(target, def.Name));
            if (def is ArmorDefinition armor) armors.Add(new ArmorState(armor, entry, i));
            else if (def is WeaponDefinition weapon) weapons.Add(new WeaponState(weapon, entry, i, null, null, null, weapon.DamageText, 0));
            else if (def is null && entry.Id.Length > 0)
                issues.Add(new Issue(IssueSeverity.Error, "equipment.unknown", RuleScope.Always,
                    $"Ausrüstung '{entry.Id}' ist in den Regeldaten nicht vorhanden.", target));
            var line = new EquipmentLine(def, entry, i, entry.Name ?? def?.Name ?? entry.Id, Math.Max(0, entry.Quantity),
                entry.Weight ?? def?.WeightStone, entry.Price ?? def?.PriceSilver);
            lines.Add(line);
            if (entry.Paid && line.Price is null)
                issues.Add(new Issue(IssueSeverity.Info, "equipment.noPrice", RuleScope.Always,
                    "Kein Preis bekannt – trage einen Preis ein, damit er vom Geld abgeht.", target));
        }

        var worn = armors.Where(a => a.Entry.Equipped).ToList();
        if (worn.Count > 1)
            issues.Add(new Issue(IssueSeverity.Warning, "equipment.armorTwice", RuleScope.Always,
                "Es kann nur eine Rüstung angelegt sein; gerechnet wird mit der ersten.", EquipmentTarget(worn[1].Index)));
        var armorDef = worn.FirstOrDefault()?.Definition;
        var original = armorDef?.Encumbrance ?? 0;
        var habituation = specialAbilities.Where(t => t.Definition.Name == "Belastungsgewöhnung").Select(t => t.Level).DefaultIfEmpty(0).Max();
        var armorLevel = Math.Max(0, original - habituation);
        var extras = armorDef is not null && habituation <= original;
        var speedExtra = extras ? armorDef!.SpeedPenalty ?? 0 : 0;
        var initiativeExtra = extras ? armorDef!.InitiativePenalty ?? 0 : 0;

        // Traglast: alles, was der Held bei sich trägt, außer der angelegten Rüstung
        var wornIndex = worn.FirstOrDefault()?.Index;
        var carried = lines.Where(l => l.Entry.Carried && l.Index != wornIndex).Sum(l => (l.Weight ?? 0) * l.Quantity);
        var capacity = CarryPerStrength * attr.GetValueOrDefault("KK");
        var loadLevel = carried > capacity ? (int)Math.Floor((carried - capacity) / StonesPerLoadLevel) : 0;

        var encumbrance = armorLevel + loadLevel;
        if (encumbrance >= 4)
            issues.Add(new Issue(IssueSeverity.Warning, "equipment.encumbranceIV", RuleScope.Always,
                $"Belastung Stufe {TraitRules.Roman(encumbrance)}: handlungsunfähig (bis auf Last fallen lassen)."));

        // Waffen: AT/PA der Kampftechnik + Waffenmodifikator − Belastung; TP + Schadensbonus über der Schadensschwelle
        for (var w = 0; w < weapons.Count; w++)
        {
            var def = weapons[w].Definition;
            var ct = combatTechniques.FirstOrDefault(c => c.Definition.Id == def.CombatTechnique);
            // Bei mehreren Leiteigenschaften zählt die günstigere (Wiki: Kampftechniken, Leiteigenschaft)
            var bonus = def.DamageThresholds is { Count: > 0 } thresholds
                ? Math.Max(0, thresholds.Max(t => attr.GetValueOrDefault(t.Key) - t.Value))
                : 0;
            weapons[w] = weapons[w] with
            {
                Attack = ct?.Attack is { } at ? at + (def.AttackModifier ?? 0) - encumbrance : null,
                Parry = ct?.Parry is { } pa && !def.NoParry ? pa + (def.ParryModifier ?? 0) - encumbrance : null,
                RangedAttack = ct?.RangedAttack,
                DamageText = def.Damage?.Format(bonus) ?? def.DamageText,
                DamageBonus = bonus,
            };
        }

        // Geld: Startkapital (Reich +250 S, Arm −250 S je Stufe; Wiki: Schritt 13) + Buchungen − Gekauftes
        var cr = rules.Creation;
        int Level(string id) => traits.Where(t => t.Definition.Id == id).Sum(t => t.Level);
        var wealth = Level("adv.reich") - Level("disadv.arm");
        var starting = Math.Max(0, cr.StartingMoney + cr.MoneyPerWealthLevel * wealth);
        var spent = lines.Where(l => l.Entry.Paid).Sum(l => (l.Price ?? 0) * l.Quantity);
        var booked = character.Money.Sum(m => m.Amount);
        var money = starting + booked - spent;
        if (money < 0)
            issues.Add(new Issue(IssueSeverity.Warning, "equipment.money", RuleScope.Always,
                $"Es wurde mehr ausgegeben als vorhanden: {FormatMoney(money)}."));

        return new EquipmentState(armors, weapons, armorDef, armorDef?.Protection ?? 0, original, habituation, encumbrance,
            speedExtra, initiativeExtra,
            Initiative: derived.Initiative - encumbrance + initiativeExtra,
            Speed: derived.Speed is { } gs ? gs - encumbrance + speedExtra : null,
            Dodge: derived.Dodge - encumbrance)
        {
            Lines = lines,
            ArmorLevel = armorLevel,
            CarriedWeight = carried,
            CarryCapacity = capacity,
            LoadLevel = loadLevel,
            StartingMoney = starting,
            WealthLevel = wealth,
            Spent = spent,
            Booked = booked,
            Money = money,
        };
    }

    public static string EquipmentTarget(int index) => "equipment:" + index;

    /// <summary>„575,5 S“; Beträge unter 1 S mit Hellern/Kreuzern als Nachkommastellen.</summary>
    public static string FormatMoney(decimal silver) =>
        (silver < 0 ? "−" : "") + Math.Abs(silver).ToString("#,##0.##", System.Globalization.CultureInfo.GetCultureInfo("de-DE")) + " S";

    /// <summary>„23,6 Stein“.</summary>
    public static string FormatWeight(decimal stone) =>
        stone.ToString("#,##0.###", System.Globalization.CultureInfo.GetCultureInfo("de-DE")) + " Stein";
}
