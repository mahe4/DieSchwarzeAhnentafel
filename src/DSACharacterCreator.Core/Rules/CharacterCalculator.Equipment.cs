using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Core.Rules;

/// <summary>Waffen und Rüstung: Kampfwerte mit Waffe, Rüstungsschutz, Belastung und ihre Auswirkungen.</summary>
public sealed partial class CharacterCalculator
{
    /// <summary>
    /// Belastung durch die angelegte Rüstung (Wiki: Zustand Belastung; SF Belastungsgewöhnung): Stufe I–III gibt je
    /// −1 auf AT, Verteidigung (Parade, Ausweichen), INI und GS sowie auf Talente mit „Belastung: ja“; Stufe IV ist
    /// handlungsunfähig. Belastungsgewöhnung lässt die Auswirkungen in der Rüstungstabelle um zwei Zeilen je Stufe nach
    /// oben wandern (BE-Spalte 0,0,1,1,2,2,3; zusätzliche Abzüge in jeder zweiten Zeile): das ist BE −1 je Stufe, und
    /// zusätzliche Abzüge entfallen erst, wenn die Stufe die BE der Rüstung übersteigt.
    /// </summary>
    private EquipmentState Equipment(Character character, IReadOnlyDictionary<string, int> attr, List<TraitState> specialAbilities,
        IReadOnlyList<CombatTechniqueState> combatTechniques, DerivedValues derived, SourceSelection sources, List<Issue> issues)
    {
        var armors = new List<ArmorState>();
        var weapons = new List<WeaponState>();
        for (var i = 0; i < character.Equipment.Count; i++)
        {
            var entry = character.Equipment[i];
            var target = EquipmentTarget(i);
            if (rules.FindArmor(entry.Id) is { } armor)
            {
                if (!sources.IsAvailable(armor.Publications)) issues.Add(SourceDisabled(target, armor.Name));
                armors.Add(new ArmorState(armor, entry, i));
            }
            else if (rules.FindWeapon(entry.Id) is { } weapon)
            {
                if (!sources.IsAvailable(weapon.Publications)) issues.Add(SourceDisabled(target, weapon.Name));
                weapons.Add(new WeaponState(weapon, entry, i, null, null, null, weapon.DamageText, 0));
            }
            else
                issues.Add(new Issue(IssueSeverity.Error, "equipment.unknown", RuleScope.Always,
                    $"Ausrüstung '{entry.Id}' ist in den Regeldaten nicht vorhanden.", target));
        }

        var worn = armors.Where(a => a.Entry.Equipped).ToList();
        if (worn.Count > 1)
            issues.Add(new Issue(IssueSeverity.Warning, "equipment.armorTwice", RuleScope.Always,
                "Es kann nur eine Rüstung angelegt sein; gerechnet wird mit der ersten.", EquipmentTarget(worn[1].Index)));
        var armorDef = worn.FirstOrDefault()?.Definition;
        var original = armorDef?.Encumbrance ?? 0;
        var habituation = specialAbilities.Where(t => t.Definition.Name == "Belastungsgewöhnung").Select(t => t.Level).DefaultIfEmpty(0).Max();
        var encumbrance = Math.Max(0, original - habituation);
        var extras = armorDef is not null && habituation <= original;
        var speedExtra = extras ? armorDef!.SpeedPenalty ?? 0 : 0;
        var initiativeExtra = extras ? armorDef!.InitiativePenalty ?? 0 : 0;
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

        return new EquipmentState(armors, weapons, armorDef, armorDef?.Protection ?? 0, original, habituation, encumbrance,
            speedExtra, initiativeExtra,
            Initiative: derived.Initiative - encumbrance + initiativeExtra,
            Speed: derived.Speed is { } gs ? gs - encumbrance + speedExtra : null,
            Dodge: derived.Dodge - encumbrance);
    }

    public static string EquipmentTarget(int index) => "equipment:" + index;
}
