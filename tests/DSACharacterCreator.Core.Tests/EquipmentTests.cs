using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Persistence;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.Core.Tests;

/// <summary>Waffen und Rüstungen (Meilenstein 7a).</summary>
public class EquipmentTests
{
    private static readonly RuleData Rules = TestRules.Data;
    private static readonly CharacterCalculator Calculator = new(Rules);

    private static Character Fighter(params string[] equipment)
    {
        var c = new Character();
        SourceSelection.Default(Rules.Books).ApplyTo(c, Rules.Books);
        c.Creation.Species = "species.mensch";
        c.Creation.Attributes["MU"] = 14;
        c.Creation.Attributes["GE"] = 14;
        c.Creation.Attributes["KK"] = 17;
        c.Creation.CombatTechniqueAdjustments["ct.schwerter"] = 6; // KtW 12
        c.Equipment = equipment.Select(id => new EquipmentEntry { Id = id, Equipped = true }).ToList();
        return c;
    }

    [Fact]
    public void WeaponsAndArmorAreLoaded()
    {
        var sword = Rules.FindWeapon("weapon.langschwert")!;
        Assert.Equal("ct.schwerter", sword.CombatTechnique);
        Assert.Equal(new DamageRoll(1, 6, 4), sword.Damage);
        Assert.Equal(15, sword.DamageThresholds!["KK"]);
        Assert.Equal("10/50/80", Rules.FindWeapon("weapon.kurzbogen")!.Ranges);
        var leather = Rules.FindArmor("armor.lederruestung")!;
        Assert.Equal((3, 1, -1, -1), (leather.Protection, leather.Encumbrance, leather.SpeedPenalty, leather.InitiativePenalty));
        Assert.All(Rules.Weapons, w => Assert.NotNull(w.CombatTechnique));
    }

    [Fact]
    public void Weapon_AttackParryAndDamageBonus()
    {
        var state = Calculator.Calculate(Fighter("weapon.langschwert"));
        var ct = state.CombatTechniques.Single(c => c.Definition.Id == "ct.schwerter");
        var sword = Assert.Single(state.Equipment.Weapons);
        Assert.Equal(ct.Attack, sword.Attack);
        Assert.Equal(ct.Parry, sword.Parry);
        // KK 17 über Schadensschwelle 15: +2 TP
        Assert.Equal(2, sword.DamageBonus);
        Assert.Equal("1W6+6", sword.DamageText);

        var rapier = Assert.Single(Calculator.Calculate(Fighter("weapon.rapier")).Equipment.Weapons);
        var fencing = state.CombatTechniques.Single(c => c.Definition.Id == "ct.fechtwaffen");
        Assert.Equal(fencing.Attack + 1, rapier.Attack); // AT-Mod +1
    }

    [Fact]
    public void Armor_EncumbranceReducesValues()
    {
        var state = Calculator.Calculate(Fighter("weapon.langschwert", "armor.plattenruestung"));
        var eq = state.Equipment;
        Assert.Equal(6, eq.Protection);
        Assert.Equal(3, eq.ArmorEncumbrance);
        Assert.Equal(3, eq.Encumbrance);
        var ct = state.CombatTechniques.Single(c => c.Definition.Id == "ct.schwerter");
        Assert.Equal(ct.Attack - 3, eq.Weapons[0].Attack);
        Assert.Equal(ct.Parry - 3, eq.Weapons[0].Parry);
        Assert.Equal(state.Derived.Initiative - 3, eq.Initiative);
        Assert.Equal(state.Derived.Speed - 3, eq.Speed);
        Assert.Equal(state.Derived.Dodge - 3, eq.Dodge);
    }

    [Fact]
    public void Armor_ExtraPenalties()
    {
        var eq = Calculator.Calculate(Fighter("armor.lederruestung")).Equipment;
        var d = Calculator.Calculate(Fighter()).Derived;
        Assert.Equal(d.Initiative - 1 - 1, eq.Initiative); // BE 1 und zusätzlich −1 INI
        Assert.Equal(d.Speed - 1 - 1, eq.Speed);
    }

    [Fact]
    public void Habituation_MovesTwoRowsUpPerLevel()
    {
        // Plattenrüstung bei Belastungsgewöhnung I wie Kettenrüstung (BE 2, keine Zusatzabzüge)
        var c = Fighter("armor.plattenruestung");
        c.Creation.SpecialAbilities.Add(new TraitEntry { Id = "sf.belastungsgewoehnung", Level = 1 });
        var eq = Calculator.Calculate(c).Equipment;
        Assert.Equal((3, 1, 2), (eq.ArmorEncumbrance, eq.Habituation, eq.Encumbrance));

        // Schuppenrüstung (BE 2, −1 GS/−1 INI): Stufe I wie Lederrüstung (BE 1, Abzüge bleiben), Stufe II wie Schwere
        // Kleidung (BE 0, Abzüge bleiben)
        var scale = Fighter("armor.schuppenruestung");
        scale.Creation.SpecialAbilities.Add(new TraitEntry { Id = "sf.belastungsgewoehnung", Level = 2 });
        var scaleEq = Calculator.Calculate(scale).Equipment;
        Assert.Equal((0, -1, -1), (scaleEq.Encumbrance, scaleEq.SpeedPenalty, scaleEq.InitiativePenalty));

        // Lederrüstung (BE 1) bei Stufe II: wie normale Kleidung, keine Abzüge mehr
        var leather = Fighter("armor.lederruestung");
        leather.Creation.SpecialAbilities.Add(new TraitEntry { Id = "sf.belastungsgewoehnung", Level = 2 });
        var leatherEq = Calculator.Calculate(leather).Equipment;
        Assert.Equal((0, 0, 0), (leatherEq.Encumbrance, leatherEq.SpeedPenalty, leatherEq.InitiativePenalty));
    }

    [Fact]
    public void OnlyOneArmorWorn_AndRangedHasFk()
    {
        var c = Fighter("armor.lederruestung", "armor.kettenruestung", "weapon.kurzbogen");
        var state = Calculator.Calculate(c);
        Assert.Contains(state.Issues, i => i.Code == "equipment.armorTwice");
        Assert.Equal("Lederrüstung", state.Equipment.WornArmor!.Name);
        var bow = state.Equipment.Weapons.Single();
        Assert.Null(bow.Attack);
        Assert.Equal(state.CombatTechniques.Single(t => t.Definition.Id == "ct.boegen").RangedAttack, bow.RangedAttack);
    }

    [Fact]
    public void Equipment_IsSavedAndNotLogged()
    {
        var c = Fighter("weapon.langschwert");
        Phases.FinishCreation(c, Calculator.Calculate(c), Rules);
        c.Equipment.Add(new EquipmentEntry { Id = "armor.kettenruestung", Equipped = true });
        Assert.Single(c.Log); // nur der Abschluss
        var loaded = CharacterSerializer.Deserialize(CharacterSerializer.Serialize(c));
        Assert.Equal(2, loaded.Equipment.Count);
        Assert.Equal(2, Calculator.Calculate(loaded).Equipment.Encumbrance);
    }
}
