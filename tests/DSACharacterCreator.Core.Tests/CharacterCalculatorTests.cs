using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.Core.Tests;

public class CharacterCalculatorTests
{
    private static readonly CharacterCalculator Calculator = new(TestRules.Data);

    private static Character NewCharacter(Action<CreationLayer>? setup = null)
    {
        var c = new Character { EnabledBooks = SourceSelection.Default(TestRules.Data.Books).EnabledBookIds.ToList() };
        setup?.Invoke(c.Creation);
        return c;
    }

    [Fact]
    public void NewCharacter_StartsWithDefaults()
    {
        var state = Calculator.Calculate(NewCharacter());

        Assert.Equal("erfahren", state.ExperienceLevel.Id);
        Assert.Equal(1100, state.Ap.Total);
        Assert.Equal(0, state.Ap.Spent);
        Assert.All(state.Attributes, a => Assert.Equal(8, a.Value));
        Assert.All(state.Skills, s => Assert.Equal(0, s.Value));
        Assert.All(state.CombatTechniques, c => Assert.Equal(6, c.Value));
        Assert.Equal(59, state.Skills.Count);
        Assert.Equal(3, state.Derived.FatePoints);
    }

    [Fact]
    public void Costs_AreSummedPerCategory()
    {
        var state = Calculator.Calculate(NewCharacter(c =>
        {
            c.Attributes["MU"] = 14;            // 6 × 15
            c.Skills["skill.gassenwissen"] = 9; // C: 9 × 3
            c.CombatTechniques["ct.dolche"] = 8; // B: 2 × 2
        }));

        Assert.Equal(90, state.Ap.Attributes);
        Assert.Equal(27, state.Ap.Skills);
        Assert.Equal(4, state.Ap.CombatTechniques);
        Assert.Equal(1100 - 121, state.Ap.Remaining);
    }

    [Fact]
    public void CombatValues_MatchWikiExamples()
    {
        // Louisa: Raufen 6, MU 14, GE 14 → AT 8, PA 5
        var louisa = Calculator.Calculate(NewCharacter(c =>
        {
            c.Attributes["MU"] = 14;
            c.Attributes["GE"] = 14;
        }));
        var raufen = louisa.CombatTechniques.Single(t => t.Definition.Id == "ct.raufen");
        Assert.Equal(8, raufen.Attack);
        Assert.Equal(5, raufen.Parry);

        // Chris: Schwerter 12, MU 15, GE/KK 12 → AT 14, PA 7 (Kompetent, damit KtW 12 erlaubt bleibt)
        var chris = Calculator.Calculate(NewCharacter(c =>
        {
            c.Attributes["MU"] = 15;
            c.Attributes["GE"] = 12;
            c.Attributes["KK"] = 12;
            c.CombatTechniques["ct.schwerter"] = 12;
        }));
        var schwerter = chris.CombatTechniques.Single(t => t.Definition.Id == "ct.schwerter");
        Assert.Equal(14, schwerter.Attack);
        Assert.Equal(7, schwerter.Parry);

        // Sarah: Bögen 12, FF 14 → FK 14
        var sarah = Calculator.Calculate(NewCharacter(c =>
        {
            c.Attributes["FF"] = 14;
            c.CombatTechniques["ct.boegen"] = 12;
        }));
        var boegen = sarah.CombatTechniques.Single(t => t.Definition.Id == "ct.boegen");
        Assert.Equal(14, boegen.RangedAttack);
        Assert.Null(boegen.Attack);
        Assert.Null(boegen.Parry);
    }

    [Fact]
    public void DerivedValues_RoundHalfUp()
    {
        // Sarah: GE 15 → AW 8; MU 12 + GE 15 → INI 14
        var state = Calculator.Calculate(NewCharacter(c =>
        {
            c.Attributes["MU"] = 12;
            c.Attributes["GE"] = 15;
        }));

        Assert.Equal(8, state.Derived.Dodge);
        Assert.Equal(14, state.Derived.Initiative);
    }

    [Fact]
    public void CreationLimits_DependOnExperienceLevel()
    {
        var state = Calculator.Calculate(NewCharacter(c =>
        {
            c.ExperienceLevel = "unerfahren";
            c.Attributes["KL"] = 13;
            c.CombatTechniques["ct.dolche"] = 9;
        }));

        Assert.Equal(900, state.Ap.Total);
        Assert.Contains(state.Issues, i => i.Code == "attribute.creationMax" && i.TargetId == "KL");
        Assert.Contains(state.Issues, i => i.Code == "ct.creationMax" && i.TargetId == "ct.dolche");
    }

    [Fact]
    public void SkillAboveCheckAttributes_IsError_EvenWhenLevelAllowsIt()
    {
        // Kompetent erlaubt FW 13, Klettern 11 liegt aber über MU/GE/KK 8 + 2.
        var state = Calculator.Calculate(NewCharacter(c =>
        {
            c.ExperienceLevel = "kompetent";
            c.Skills["skill.klettern"] = 11;
        }));

        Assert.DoesNotContain(state.Issues, i => i.Code == "skill.creationMax");
        Assert.Contains(state.Issues, i => i.Code == "skill.attributeMax" && i.TargetId == "skill.klettern");
    }

    [Fact]
    public void SkillMax_IsHighestCheckAttributePlusTwo_CappedByLevel()
    {
        var state = Calculator.Calculate(NewCharacter(c => c.Attributes["KK"] = 9));

        var klettern = state.Skills.Single(s => s.Definition.Id == "skill.klettern"); // MU/GE/KK
        Assert.Equal(10, klettern.Max); // min(9 + 2 = 11, Erfahren 10)
    }

    [Fact]
    public void AttributeTotal_IsLimited()
    {
        var state = Calculator.Calculate(NewCharacter(c =>
        {
            foreach (var a in new[] { "MU", "KL", "IN", "CH", "FF", "GE", "KO" }) c.Attributes[a] = 13;
            c.Attributes["KK"] = 12;
        }));

        Assert.Equal(103, state.Attributes.Sum(a => a.Value));
        Assert.Contains(state.Issues, i => i.Code == "attribute.creationTotal");
    }

    [Fact]
    public void Overspending_IsError_LeftoverIsInfo()
    {
        var overspent = Calculator.Calculate(NewCharacter(c =>
        {
            c.ExperienceLevel = "unerfahren";
            foreach (var a in new[] { "MU", "KL", "IN", "CH", "FF", "GE", "KO", "KK" }) c.Attributes[a] = 12;
            foreach (var skill in TestRules.Data.Skills) c.Skills[skill.Id] = 10;
        }));
        Assert.True(overspent.Ap.Remaining < 0);
        Assert.Contains(overspent.Issues, i => i is { Code: "ap.overspent", Severity: IssueSeverity.Error });

        var fresh = Calculator.Calculate(NewCharacter());
        Assert.Contains(fresh.Issues, i => i is { Code: "ap.leftover", Severity: IssueSeverity.Info });
    }

    [Fact]
    public void AfterCreation_LevelLimitsNoLongerApply()
    {
        var character = NewCharacter(c =>
        {
            c.Attributes["MU"] = 16;
            c.Attributes["GE"] = 16;
            c.Skills["skill.klettern"] = 13;
        });
        character.Phase = CharacterPhase.Advancement;

        var state = Calculator.Calculate(character);

        Assert.DoesNotContain(state.Issues, i => i.Scope == RuleScope.CreationOnly);
        Assert.Null(state.Attributes.Single(a => a.Definition.Id == "MU").Max);
        Assert.Equal(18, state.Skills.Single(s => s.Definition.Id == "skill.klettern").Max);
    }

    [Fact]
    public void ValuesFromDisabledSources_AreWarned()
    {
        var character = NewCharacter(c => c.CombatTechniques["ct.peitschen"] = 8);
        character.EnabledBooks = ["grw"];

        var state = Calculator.Calculate(character);

        var peitschen = state.CombatTechniques.Single(t => t.Definition.Id == "ct.peitschen");
        Assert.False(peitschen.Available);
        Assert.Contains(state.Issues, i => i.Code == "source.disabled" && i.TargetId == "ct.peitschen");
    }

    [Fact]
    public void Whips_AttackWithDexterity_AndCannotParry()
    {
        var state = Calculator.Calculate(NewCharacter(c =>
        {
            c.Attributes["FF"] = 14;
            c.Attributes["MU"] = 8;
        }));

        var peitschen = state.CombatTechniques.Single(t => t.Definition.Id == "ct.peitschen");
        Assert.Equal(8, peitschen.Attack); // 6 + (14 − 8) / 3
        Assert.Null(peitschen.Parry);
    }
}
