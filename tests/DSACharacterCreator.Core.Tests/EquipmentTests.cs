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

    [Fact]
    public void ItemsAndPackagesAreLoaded()
    {
        var backpack = Rules.FindItem("item.lederrucksack")!;
        Assert.Equal((2m, 34m), (backpack.WeightStone, backpack.PriceSilver));
        Assert.Equal(200m, Rules.FindWeapon("weapon.langschwert")!.PriceSilver);
        Assert.Equal(12m, Rules.FindArmor("armor.kettenruestung")!.WeightStone);
        // Paketpreis aus den Zeilen; „–“ beim Waffenpflegeset übernimmt dessen Preis (1 S) → Wiki-Summe 575,5 S
        var adventurer = Rules.FindEquipmentPackage("package.abenteurerpaket")!;
        Assert.Equal(575.5m, adventurer.Price);
        Assert.Equal(adventurer.WikiPrice, adventurer.Price);
        Assert.Contains(adventurer.Items, i => i.Ref == "weapon.kurzschwert");
    }

    [Fact]
    public void Load_KkTimesTwoFree_ThenOneLevelPerFourFullStones()
    {
        // KK 17: 34 Stein ohne Belastung
        var c = Fighter();
        c.Equipment.Add(new EquipmentEntry { Name = "Steine", Weight = 1, Quantity = 37 });
        var eq = Calculator.Calculate(c).Equipment;
        Assert.Equal((37m, 34, 0), (eq.CarriedWeight, eq.CarryCapacity, eq.LoadLevel));

        c.Equipment[0].Quantity = 38; // 4 volle Stein darüber
        eq = Calculator.Calculate(c).Equipment;
        Assert.Equal((1, 1), (eq.LoadLevel, eq.Encumbrance));

        // nicht dabei: zählt nicht
        c.Equipment[0].Carried = false;
        Assert.Equal(0, Calculator.Calculate(c).Equipment.CarriedWeight);
    }

    [Fact]
    public void Load_WornArmorWeightIgnored_AddsToArmorEncumbrance()
    {
        var c = Fighter("armor.kettenruestung");
        c.Equipment.Add(new EquipmentEntry { Name = "Gepäck", Weight = 42 }); // 8 über 34: Stufe II
        var eq = Calculator.Calculate(c).Equipment;
        Assert.Equal(42m, eq.CarriedWeight); // Kettenrüstung (12 Stein) angelegt: zählt nicht
        Assert.Equal((2, 2, 4), (eq.ArmorLevel, eq.LoadLevel, eq.Encumbrance));
        Assert.Contains(Calculator.Calculate(c).Issues, i => i.Code == "equipment.encumbranceIV");

        c.Equipment[0].Equipped = false; // abgelegt, aber dabei: zählt
        Assert.Equal(54m, Calculator.Calculate(c).Equipment.CarriedWeight);
    }

    [Fact]
    public void Money_StartingCapitalWealthAndPurchases()
    {
        var c = Fighter("weapon.langschwert");
        var eq = Calculator.Calculate(c).Equipment;
        Assert.Equal((750m, 200m, 550m), (eq.StartingMoney, eq.Spent, eq.Money));

        c.Creation.Traits.Add(new TraitEntry { Id = "adv.reich", Level = 2 });
        c.Equipment[0].Quantity = 2;
        c.Money.Add(new MoneyEntry { Amount = 30, Text = "Belohnung" });
        eq = Calculator.Calculate(c).Equipment;
        Assert.Equal((1250m, 400m, 880m), (eq.StartingMoney, eq.Spent, eq.Money));

        // geschenkt: kostet nichts; eigener Preis geht vor
        c.Equipment[0].Paid = false;
        c.Equipment.Add(new EquipmentEntry { Id = "item.lederrucksack", Price = 20 });
        Assert.Equal(1260m, Calculator.Calculate(c).Equipment.Money);
    }

    [Fact]
    public void Money_PoorHeroOverspends_Warns()
    {
        var c = Fighter("weapon.langschwert");
        c.Creation.Traits.Add(new TraitEntry { Id = "disadv.arm", Level = 3 });
        var state = Calculator.Calculate(c);
        Assert.Equal(0m, state.Equipment.StartingMoney);
        Assert.Equal(-200m, state.Equipment.Money);
        Assert.Contains(state.Issues, i => i.Code == "equipment.money");
    }

    [Fact]
    public void Items_RoundTripWithQuantityAndMoney()
    {
        var c = Fighter();
        c.Equipment.Add(new EquipmentEntry { Id = "item.lederrucksack", Quantity = 3, Carried = false, Paid = false, Weight = 1.5m });
        c.Money.Add(new MoneyEntry { Amount = -12.5m, Text = "Unterkunft" });
        var loaded = CharacterSerializer.Deserialize(CharacterSerializer.Serialize(c));
        var e = Assert.Single(loaded.Equipment);
        Assert.Equal((3, false, false, 1.5m), (e.Quantity, e.Carried, e.Paid, e.Weight));
        Assert.Equal(-12.5m, Assert.Single(loaded.Money).Amount);
        // ältere Dateien ohne die neuen Felder: dabei und gekauft
        var old = CharacterSerializer.Deserialize(CharacterSerializer.Serialize(Fighter("weapon.dolch"))
            .Replace("\"carried\": true,", "").Replace("\"paid\": true,", ""));
        Assert.True(old.Equipment[0].Carried && old.Equipment[0].Paid);
    }

    [Fact]
    public void ParryWeaponAndShield_PassiveBonusForMainWeapon()
    {
        int Pa(CharacterState s, string ct) => s.CombatTechniques.Single(c => c.Definition.Id == ct).Parry!.Value;
        int At(CharacterState s, string ct) => s.CombatTechniques.Single(c => c.Definition.Id == ct).Attack!.Value;

        // Langschwert (PA-Mod 0) mit Linkhand: passiver Bonus +1
        var state = Calculator.Calculate(Fighter("weapon.langschwert", "weapon.linkhand"));
        var sword = state.Equipment.Weapons[0];
        Assert.Equal(Pa(state, "ct.schwerter") + 1, sword.Parry);
        Assert.Equal("Linkhand", sword.ParryBonusFrom!.Name);

        // dazu Großschild: nur der höchste Bonus (+3) zählt, AT −1 auf die Hauptwaffe
        var c = Fighter("weapon.langschwert", "weapon.linkhand", "weapon.grossschild");
        state = Calculator.Calculate(c);
        sword = state.Equipment.Weapons[0];
        Assert.Equal(Pa(state, "ct.schwerter") + 3, sword.Parry);
        Assert.Equal(At(state, "ct.schwerter") - 1, sword.Attack);
        // aktive Parade mit dem Schild: doppelter Parade-Bonus, kein Bonus von der Parierwaffe
        var shield = state.Equipment.Weapons[2];
        Assert.Equal(Pa(state, "ct.schilde") + 6, shield.Parry);
        Assert.Null(shield.ParryBonusFrom);

        // Zweihandwaffe: keine Hand frei für Schild oder Parierwaffe
        var twoHanded = Calculator.Calculate(Fighter("weapon.zweihaender", "weapon.grossschild")).Equipment.Weapons[0];
        Assert.Null(twoHanded.ParryBonusFrom);
        Assert.Equal(0, twoHanded.ShieldAttackModifier);

        // nicht geführt: kein Bonus
        c.Equipment[1].Equipped = false;
        c.Equipment[2].Equipped = false;
        Assert.Equal(Pa(state, "ct.schwerter"), Calculator.Calculate(c).Equipment.Weapons[0].Parry);
    }

    [Fact]
    public void DualWield_PenaltiesSpecialAbilityAndAmbidextrous()
    {
        // Langschwert + Kurzschwert: −2 auf beide, falsche Hand weitere −4
        var c = Fighter("weapon.langschwert", "weapon.kurzschwert");
        var sword = Calculator.Calculate(c).Equipment.Weapons[0];
        var dual = sword.DualWield!;
        Assert.Equal((sword.Attack - 2, sword.Parry - 2), (dual.Attack, dual.Parry));
        Assert.Equal((sword.Attack - 6, sword.Parry - 6), (dual.OffHandAttack, dual.OffHandParry));
        Assert.Equal("Kurzschwert", dual.Partner.Name);

        // SF Beidhändiger Kampf II (Schwerter in der Liste) und Vorteil Beidhändig: keine Abzüge
        c.Creation.Attributes["GE"] = 15;
        c.Creation.SpecialAbilities.Add(new TraitEntry { Id = "sf.beidhaendiger-kampf", Level = 2 });
        c.Creation.Traits.Add(new TraitEntry { Id = "adv.beidhaendig" });
        sword = Calculator.Calculate(c).Equipment.Weapons[0];
        Assert.Equal((sword.Attack, sword.Parry, sword.Attack, 0), (sword.DualWield!.Attack, sword.DualWield.Parry,
            sword.DualWield.OffHandAttack, sword.DualWield.OffHandPenalty));

        // Schild: Angriff mit Abzug, Parade ohne
        var shield = Calculator.Calculate(Fighter("weapon.langschwert", "weapon.holzschild")).Equipment.Weapons[1];
        Assert.Equal((shield.Attack - 6, shield.Parry), (shield.DualWield!.OffHandAttack, shield.DualWield.OffHandParry));

        // nur eine geführte Waffe, Zweihandwaffe, Kettenwaffe ohne Schild: keine zweite Zeile
        c.Equipment[1].Equipped = false;
        Assert.Null(Calculator.Calculate(c).Equipment.Weapons[0].DualWield);
        Assert.All(Calculator.Calculate(Fighter("weapon.zweihaender", "weapon.kurzschwert")).Equipment.Weapons,
            w => Assert.Null(w.DualWield));
        Assert.All(Calculator.Calculate(Fighter("weapon.morgenstern", "weapon.kurzschwert")).Equipment.Weapons,
            w => Assert.Null(w.DualWield));
        Assert.NotNull(Calculator.Calculate(Fighter("weapon.morgenstern", "weapon.holzschild")).Equipment.Weapons[0].DualWield);
    }
}
