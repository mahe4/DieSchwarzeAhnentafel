using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;
using DSACharacterCreator.Core.Sheet;

namespace DSACharacterCreator.Core.Tests;

/// <summary>Würfelproben nach den Grundregeln (Fertigkeitsprobe mit QS, Kampfproben mit Bestätigung).</summary>
public class CheckRollsTests
{
    private static readonly SkillCheck Climbing =
        new("Klettern", [new("MU", 14), new("GE", 12), new("KK", 11)], 8);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(6, 2)]
    [InlineData(7, 3)]
    [InlineData(12, 4)]
    [InlineData(15, 5)]
    [InlineData(16, 6)]
    [InlineData(25, 6)]
    public void Quality_FromSkillPoints(int points, int quality) => Assert.Equal(quality, CheckRolls.Quality(points));

    [Fact]
    public void SkillCheck_CompensatesWithSkillPoints()
    {
        // Wiki-Beispiel Fertigkeitsmodifikatoren: −1 → 13/11/10, Wurf 16 kostet 3 FP
        var r = CheckRolls.Evaluate(Climbing, -1, [16, 5, 10]);
        Assert.Equal([13, 11, 10], r.Targets);
        Assert.Equal((5, 2, CheckOutcome.Success), (r.Points, r.Quality, r.Outcome));

        var exact = CheckRolls.Evaluate(Climbing, 0, [20, 12, 11]);
        Assert.Equal((2, 1, CheckOutcome.Success), (exact.Points, exact.Quality, exact.Outcome));

        var failed = CheckRolls.Evaluate(Climbing, 0, [19, 17, 11]);
        Assert.Equal((-2, 0, CheckOutcome.Failure), (failed.Points, failed.Quality, failed.Outcome));
    }

    [Fact]
    public void SkillCheck_CriticalsBotchesAndImpossible()
    {
        // Doppel-1 gelingt trotz zu hoher 19 (Wiki-Beispiel Gassenwissen, FW 2)
        var weak = Climbing with { SkillValue = 2 };
        var crit = CheckRolls.Evaluate(weak, 0, [1, 19, 1]);
        Assert.Equal((CheckOutcome.Critical, 1), (crit.Outcome, crit.Quality));
        Assert.Equal(CheckOutcome.SpectacularCritical, CheckRolls.Evaluate(weak, 0, [1, 1, 1]).Outcome);
        // Doppel-20 misslingt trotz genug FP
        var botch = CheckRolls.Evaluate(Climbing with { SkillValue = 14 }, 0, [11, 20, 20]);
        Assert.Equal((CheckOutcome.Botch, false), (botch.Outcome, botch.Succeeded));
        Assert.Equal(CheckOutcome.SpectacularBotch, CheckRolls.Evaluate(Climbing, 0, [20, 20, 20]).Outcome);
        // KK 11 − 11 = 0: keine Probe
        Assert.Equal(CheckOutcome.Impossible, CheckRolls.Evaluate(Climbing, -11, [1, 1, 1]).Outcome);
    }

    [Fact]
    public void CombatCheck_ConfirmsCriticalsAndBotches()
    {
        Assert.Equal(CheckOutcome.Success, CheckRolls.Evaluate(11, 0, 11, 20).Outcome);
        Assert.Equal(CheckOutcome.Failure, CheckRolls.Evaluate(11, 0, 12, 1).Outcome);
        // Wiki-Beispiel: AT 11 mit −2 durch Belastung, 1 und Bestätigung 2
        Assert.Equal(CheckOutcome.Critical, CheckRolls.Evaluate(13, -2, 1, 2).Outcome);
        Assert.Equal(CheckOutcome.CriticalUnconfirmed, CheckRolls.Evaluate(11, 0, 1, 12).Outcome);
        // 20 und Bestätigung 12 gegen 11: Patzer; 20 misslingt auch bei Werten über 19
        Assert.Equal(CheckOutcome.Botch, CheckRolls.Evaluate(11, 0, 20, 12).Outcome);
        Assert.Equal(CheckOutcome.BotchUnconfirmed, CheckRolls.Evaluate(11, 0, 20, 11).Outcome);
        Assert.Equal(CheckOutcome.BotchUnconfirmed, CheckRolls.Evaluate(23, 0, 20, 19).Outcome);
        Assert.Equal(CheckOutcome.Botch, CheckRolls.Evaluate(23, 0, 20, 20).Outcome);
        Assert.Equal(CheckOutcome.Impossible, CheckRolls.Evaluate(3, -3, 1, 1).Outcome);
    }

    [Fact]
    public void Damage_DoubledOnCriticalHit()
    {
        // Wiki-Beispiel: 1W6+4, Wurf 5 → 9, kritisch 18
        var axe = new DamageRoll(1, 6, 4);
        Assert.Equal(9, CheckRolls.Damage(axe, [5], false).Total);
        Assert.Equal(18, CheckRolls.Damage(axe, [5], true).Total);
    }

    [Fact]
    public void Sheet_ProvidesRollsAndDualWieldLine()
    {
        var rules = TestRules.Data;
        var c = new Character();
        SourceSelection.Default(rules.Books).ApplyTo(c, rules.Books);
        c.Creation.Species = "species.mensch";
        c.Creation.Attributes["MU"] = 14;
        c.Creation.Attributes["GE"] = 14;
        c.Creation.Attributes["KK"] = 17;
        c.Creation.CombatTechniqueAdjustments["ct.schwerter"] = 6;
        c.Equipment = new[] { "weapon.langschwert", "weapon.kurzschwert", "armor.kettenhemd" }
            .Select(id => new EquipmentEntry { Id = id, Equipped = true }).ToList();
        var state = new CharacterCalculator(rules).Calculate(c);
        var sheet = CharacterSheet.Create(c, state, rules);

        var sword = sheet.Weapons[0];
        var w = state.Equipment.Weapons[0];
        Assert.Equal($"AT {w.DualWield!.Attack} · PA {w.DualWield.Parry} · falsche Hand AT {w.DualWield.OffHandAttack} · PA {w.DualWield.OffHandParry}"
                .Replace("−", "-"),
            sword.DualWield!.Replace('\u00a0', ' ').Replace("−", "-"));
        Assert.DoesNotContain("beidhändig", sword.Note ?? "");
        Assert.Equal(["AT", "PA", "AT beidhändig", "PA beidhändig", "AT falsche Hand", "PA falsche Hand"],
            sword.Rolls.Select(r => r.Label));
        Assert.Equal(w.DamageText, sword.Rolls[0].Damage!.Format());
        Assert.Null(sword.Rolls[1].Damage);

        // Talent mit Belastung: Vorgabe-Modifikator; Probeneigenschaften mit Werten
        var climbing = sheet.Skills.SelectMany(g => g.Items).Single(s => s.Name == "Klettern").Roll!;
        Assert.Equal(-state.Equipment.Encumbrance, climbing.DefaultModifier);
        Assert.True(state.Equipment.Encumbrance > 0);
        Assert.Equal([("MU", 14), ("GE", 14), ("KK", 17)], climbing.Attributes.Select(a => (a.Short, a.Value)));
        Assert.Contains(sheet.CombatTechniques.Single(t => t.Name == "Schwerter").Rolls, r => r.Label == "AT");
        Assert.Equal(("mittel", (string?)null), (sword.Rolls[0].Reach, sword.Rolls[0].ShieldSize));
    }

    private static SituationOption Option(CombatCheck check, string id) =>
        CombatSituations.For(check).SelectMany(g => g.Options).Single(o => o.Id == id);

    private static IEnumerable<string> Ids(CombatCheck check) => CombatSituations.For(check).SelectMany(g => g.Options).Select(o => o.Id);

    [Fact]
    public void Situations_MeleeReachAndCrampedSpace()
    {
        // Nahkampfreichweite: kurz gegen mittel −2, gegen lang −4; mittel gegen lang −2; lange Waffen ohne Abzug
        var dagger = new CombatCheck("AT", 12, CombatCheckKind.Attack, Reach: "kurz");
        Assert.Equal((-2, -4), (Option(dagger, "enemy-reach-medium").Modifier, Option(dagger, "enemy-reach-long").Modifier));
        Assert.DoesNotContain("cramped", Ids(dagger)); // kurze Waffen: keine Abzüge in beengter Umgebung
        var sword = new CombatCheck("AT", 12, CombatCheckKind.Attack, Reach: "mittel");
        Assert.DoesNotContain("enemy-reach-medium", Ids(sword));
        Assert.Equal((-2, -4), (Option(sword, "enemy-reach-long").Modifier, Option(sword, "cramped").Modifier));
        var spear = new CombatCheck("PA", 6, CombatCheckKind.Parry, Reach: "überlang");
        Assert.Equal(-8, Option(spear, "cramped").Modifier);
        Assert.DoesNotContain("enemy-reach-long", Ids(new CombatCheck("AT", 12, CombatCheckKind.Attack, Reach: "lang")));

        // Schilde: klein −2/−2, mittel −4/−3, groß −6/−4; nur die Schildparade gilt gegen Fernkampf
        var shieldAt = new CombatCheck("AT", 10, CombatCheckKind.Attack, Reach: "kurz", ShieldSize: "groß", Shield: true);
        var shieldPa = shieldAt with { Kind = CombatCheckKind.Parry };
        Assert.Equal((-6, -4), (Option(shieldAt, "cramped").Modifier, Option(shieldPa, "cramped").Modifier));
        Assert.Equal(-3, Option(shieldPa with { ShieldSize = "mittel" }, "cramped").Modifier);
        Assert.Equal(-4, Option(shieldPa, "ranged-shot").Modifier);
        Assert.DoesNotContain("ranged-shot", Ids(new CombatCheck("PA", 8, CombatCheckKind.Parry, Reach: "mittel")));

        // Kampftechnik ohne Waffe: keine Reichweitenumstände, die übrigen schon
        var technique = new CombatCheck("AT", 12, CombatCheckKind.Attack);
        Assert.DoesNotContain("cramped", Ids(technique));
        Assert.Equal(2, Option(technique, "advantage").Modifier);
        Assert.Equal(-9, Option(new CombatCheck("PA", 8, CombatCheckKind.Parry), "defense-4").Modifier);
    }

    private static (Character Character, CharacterState State) Dodger(params string[] equipment)
    {
        var rules = TestRules.Data;
        var c = new Character();
        SourceSelection.Default(rules.Books).ApplyTo(c, rules.Books);
        c.Creation.Species = "species.mensch";
        c.Creation.Attributes["GE"] = 14;
        foreach (var (id, level) in new[]
                 {
                     ("sf.verbessertes-ausweichen", 2), ("sf.fedorino-stil", 1), ("sf.verteidigungshaltung", 1),
                     ("sf.durchgezogener-tritt", 2),
                 })
            c.Creation.SpecialAbilities.Add(new TraitEntry { Id = id, Level = level });
        c.Equipment = equipment.Select(id => new EquipmentEntry { Id = id, Equipped = true }).ToList();
        return (c, new CharacterCalculator(rules).Calculate(c));
    }

    [Fact]
    public void Dodge_PermanentBonusesAndRoll()
    {
        // GE 14 → AW 7; Verbessertes Ausweichen II +2, nur ohne Belastung
        var (c, state) = Dodger();
        Assert.Equal((7, 9, 2), (state.Derived.Dodge, state.Equipment.Dodge, state.Equipment.DodgeBonus));
        var sheet = CharacterSheet.Create(c, state, TestRules.Data);
        Assert.Equal(("AW", 9, CombatCheckKind.Dodge), (sheet.DodgeRoll!.Label, sheet.DodgeRoll.Value, sheet.DodgeRoll.Kind));
        Assert.Equal("mit Boni 9", sheet.Derived.Single(v => v.Short == "AW").Note);

        var (_, armored) = Dodger("armor.kettenhemd");
        Assert.True(armored.Equipment.Encumbrance > 0);
        Assert.Equal((0, 7 - armored.Equipment.Encumbrance), (armored.Equipment.DodgeBonus, armored.Equipment.Dodge));
    }

    [Fact]
    public void Situations_HeroAbilitiesOnDefense()
    {
        var (_, state) = Dodger("weapon.langschwert");
        var dodge = new CombatCheck("AW", 9, CombatCheckKind.Dodge);
        var ids = CombatSituations.For(dodge, state).SelectMany(g => g.Options).ToDictionary(o => o.Id, o => o.Modifier);
        // VW-Boni gelten auch für AW; Durchgezogener Tritt −1 je Stufe
        Assert.Equal((2, 4, -2), (ids["fedorino"], ids["defensive-stance"], ids["after-kick"]));
        Assert.DoesNotContain("overlong", ids.Keys);
        Assert.DoesNotContain("formation", ids.Keys);

        // Parade: Kampfstile nur mit ihren Kampftechniken (Fedorino: Dolche, Fechtwaffen)
        Assert.DoesNotContain("fedorino", Ids(new CombatCheck("PA", 8, CombatCheckKind.Parry, Technique: "Schwerter"), state));
        Assert.Contains("defensive-stance", Ids(new CombatCheck("PA", 8, CombatCheckKind.Parry, Technique: "Schwerter"), state));
        Assert.Contains("fedorino", Ids(new CombatCheck("PA", 8, CombatCheckKind.Parry, Technique: "Fechtwaffen"), state));
        // Angriff: keine Verteidigungsboni
        Assert.DoesNotContain("fedorino", Ids(new CombatCheck("AT", 12, CombatCheckKind.Attack, Technique: "Fechtwaffen"), state));
    }

    private static IEnumerable<string> Ids(CombatCheck check, CharacterState state) =>
        CombatSituations.For(check, state).SelectMany(g => g.Options).Select(o => o.Id);

    [Fact]
    public void Situations_RangedWithDamageBonus()
    {
        var bow = new CombatCheck("FK", 14, CombatCheckKind.RangedAttack, new DamageRoll(1, 6, 4));
        var selected = new[] { "range-near", "target-small", "target-still", "aim-2" }.Select(id => Option(bow, id));
        // nah +2 (+1 TP), klein −4, steht still +2, zwei Aktionen zielen +4
        Assert.Equal((4, 1), CombatSituations.Sum(selected));
        Assert.Equal((-2, -1), (Option(bow, "range-far").Modifier, Option(bow, "range-far").DamageBonus));
        Assert.Equal(-8, Option(bow, "mount-gallop").Modifier);
        Assert.Equal(-6, Option(bow, "sight-3").Modifier);
    }
}
