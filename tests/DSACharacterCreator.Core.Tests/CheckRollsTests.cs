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
    }
}
