using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.Core.Tests;

public class SpecialAbilityTests
{
    private static readonly CharacterCalculator Calculator = new(TestRules.Data);

    private static Character Hero(Action<CreationLayer> setup)
    {
        var c = new Character();
        SourceSelection.Default(TestRules.Data.Books).ApplyTo(c, TestRules.Data.Books);
        setup(c.Creation);
        return c;
    }

    private static TraitEntry Sf(string id, int? level = null, string? option = null) => new() { Id = id, Level = level, Option = option };

    [Fact]
    public void LevelCosts_AndLevelPrerequisites()
    {
        var state = Calculator.Calculate(Hero(c =>
        {
            c.Attributes["GE"] = 14;
            c.SpecialAbilities = [Sf("sf.finte", 2)]; // 20 AP; Stufe II braucht GE 15
        }));

        Assert.Equal(20, state.Ap.SpecialAbilities);
        Assert.Contains(state.Issues, i => i.Code == "traits.prerequisite" && i.TargetId == "sf:0" && i.Message.Contains("GE 15"));
        Assert.DoesNotContain(state.Issues, i => i.TargetId == "sf:0" && i.Message.Contains("Sonderfertigkeit Finte"));

        var level1 = Calculator.Calculate(Hero(c =>
        {
            c.Attributes["GE"] = 14;
            c.SpecialAbilities = [Sf("sf.finte", 1)];
        }));
        Assert.DoesNotContain(level1.Issues, i => i.Code == "traits.prerequisite");
    }

    [Fact]
    public void SkillValuePrerequisite_UsesFinalSkillValue()
    {
        var low = Calculator.Calculate(Hero(c => c.SpecialAbilities = [Sf("sf.abrichter")]));
        Assert.Contains(low.Issues, i => i.Code == "traits.prerequisite" && i.Message.Contains("Tierkunde 8"));

        var high = Calculator.Calculate(Hero(c =>
        {
            c.Attributes["MU"] = 10; c.Attributes["IN"] = 10; c.Attributes["CH"] = 10;
            c.SkillAdjustments["skill.tierkunde"] = 8;
            c.SpecialAbilities = [Sf("sf.abrichter")];
        }));
        Assert.DoesNotContain(high.Issues, i => i.Code == "traits.prerequisite");
    }

    [Fact]
    public void Language_PerLevel_NeedsLanguageChoice()
    {
        var open = Calculator.Calculate(Hero(c => c.SpecialAbilities = [Sf("sf.sprache", 3)]));
        Assert.Contains(open.Issues, i => i.Code == "traits.costOpen" && i.Message.Contains("Sprache wählen"));

        var chosen = Calculator.Calculate(Hero(c => c.SpecialAbilities = [Sf("sf.sprache", 2, "language.garethi")]));
        Assert.Equal(4, chosen.Ap.SpecialAbilities);
    }

    [Fact]
    public void Language_RespectsMaximumLevel()
    {
        var ogrisch = TestRules.Data.Languages.Single(l => l.Name == "Ogrisch");
        Assert.Equal(2, ogrisch.MaxLevel);

        var state = Calculator.Calculate(Hero(c => c.SpecialAbilities = [Sf("sf.sprache", 3, ogrisch.Id)]));
        Assert.Contains(state.Issues, i => i.Code == "traits.prerequisite" && i.Message.Contains("höchstens bis Stufe II"));
    }

    [Fact]
    public void Script_CostsByComplexity()
    {
        var script = TestRules.Data.Scripts.Single(s => s.Name == "Kusliker Zeichen");
        var state = Calculator.Calculate(Hero(c => c.SpecialAbilities = [Sf("sf.schrift", option: script.Id)]));

        var expected = script.ImprovementCost switch { ImprovementCost.A => 2, ImprovementCost.B => 4, ImprovementCost.C => 6, _ => 8 };
        Assert.Equal(expected, state.Ap.SpecialAbilities);
    }

    [Fact]
    public void SkillSpecialization_SecondCostsDouble()
    {
        var state = Calculator.Calculate(Hero(c => c.SpecialAbilities =
        [
            Sf("sf.fertigkeitsspezialisierung-talente", option: "skill.klettern"),   // B: 2
            Sf("sf.fertigkeitsspezialisierung-talente", option: "skill.klettern"),   // zweite: 4
            Sf("sf.fertigkeitsspezialisierung-talente", option: "skill.rechnen"),    // A: 1
        ]));

        Assert.Equal(2 + 4 + 1, state.Ap.SpecialAbilities);
        Assert.DoesNotContain(state.Issues, i => i.Message.Contains("bereits vorhanden"));
    }

    [Fact]
    public void ExtendedCombatAbility_NeedsFittingStyle()
    {
        var without = Calculator.Calculate(Hero(c =>
        {
            c.SkillAdjustments["skill.fahrzeuge"] = 10;
            c.SpecialAbilities = [Sf("sf.fahrender-schuetze")];
        }));
        Assert.Contains(without.Issues, i => i.Code == "traits.prerequisite" && i.Message.Contains("Baburin-Stil"));

        var with = Calculator.Calculate(Hero(c =>
        {
            c.Attributes["IN"] = 13;
            c.SkillAdjustments["skill.fahrzeuge"] = 10;
            c.SpecialAbilities = [Sf("sf.baburin-stil"), Sf("sf.fahrender-schuetze")];
        }));
        Assert.DoesNotContain(with.Issues, i => i.Code == "traits.prerequisite" && i.TargetId == "sf:1");
    }

    [Fact]
    public void RequiredSpecialAbility_IsChecked()
    {
        var state = Calculator.Calculate(Hero(c =>
        {
            c.Attributes["MU"] = 13;
            c.SpecialAbilities = [Sf("sf.baliho-stil")];
        }));
        Assert.Contains(state.Issues, i => i.Code == "traits.prerequisite" && i.Message.Contains("Berittener Kampf"));
    }

    [Fact]
    public void AdvantagePrerequisite_ForbidsSpecialAbility()
    {
        var kampftrinker = TestRules.Data.SpecialAbilities.Single(s => s.Name == "Kampftrinker");
        var state = Calculator.Calculate(Hero(c =>
        {
            c.Traits = [new TraitEntry { Id = "disadv.unvertraeglichkeit-gegenueber-alkohol" }];
            c.SpecialAbilities = [Sf(kampftrinker.Id)];
        }));

        Assert.Contains(state.Issues, i => i.Code == "traits.prerequisite" && i.TargetId == "trait:0" && i.Message.Contains("Kampftrinker"));
    }

    [Fact]
    public void SkillSpecialization_NeedsSkillValue6PerSpecialization()
    {
        var state = Calculator.Calculate(Hero(c =>
        {
            c.SkillAdjustments["skill.rechnen"] = 6;
            c.SpecialAbilities =
            [
                Sf("sf.fertigkeitsspezialisierung-talente", option: "skill.rechnen"),   // FW 6 genügt
                Sf("sf.fertigkeitsspezialisierung-talente", option: "skill.rechnen"),   // zweite braucht FW 12
            ];
        }));

        Assert.DoesNotContain(state.Issues, i => i.Code == "traits.prerequisite" && i.TargetId == "sf:0");
        Assert.Contains(state.Issues, i => i.Code == "traits.prerequisite" && i.TargetId == "sf:1" && i.Message.Contains("Rechnen 12"));
    }

    [Fact]
    public void FreeCultureEntries_AreNotChecked()
    {
        var c = Hero(_ => { });
        Templates.ChangeCulture(c.Creation, "culture.mittelreicher", TestRules.Data);

        var state = Calculator.Calculate(c);

        Assert.DoesNotContain(state.Issues, i => i.TargetId != null && i.TargetId.StartsWith("sf:"));
    }
}
