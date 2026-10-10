using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.Core.Tests;

public class TraitTests
{
    private static readonly CharacterCalculator Calculator = new(TestRules.Data);

    private static Character Hero(Action<CreationLayer> setup)
    {
        var c = new Character();
        SourceSelection.Default(TestRules.Data.Books).ApplyTo(c, TestRules.Data.Books);
        c.Creation.Species = "species.mensch";
        c.Creation.SpeciesAttributeChoices = ["MU"];
        setup(c.Creation);
        return c;
    }

    private static TraitEntry T(string id, int? level = null, string? option = null) =>
        new() { Id = id, Level = level, Option = option };

    [Fact]
    public void Costs_FixedPerLevelLevels()
    {
        var state = Calculator.Calculate(Hero(c => c.Traits =
        [
            T("adv.zauberer"),                  // 25
            T("adv.hohe-lebenskraft", 3),       // 3 × 6
            T("adv.einkommen", 4),              // Stufe IV: 20
            T("disadv.pech", 2),                // 2 × −20
        ]));

        Assert.Equal(25 + 18 + 20, state.Ap.Advantages);
        Assert.Equal(-40, state.Ap.Disadvantages);
    }

    [Fact]
    public void Cost_DependsOnChosenSkillOrOption()
    {
        var state = Calculator.Calculate(Hero(c => c.Traits =
        [
            T("adv.begabung", option: "skill.klettern"),          // B → 12
            T("adv.waffenbegabung", option: "ct.schwerter"),      // C → 10
            T("adv.herausragender-sinn", option: "Tastsinn"),     // 2
            T("adv.begabung"),                                    // noch offen
        ]));

        Assert.Equal(12 + 10 + 2, state.Ap.Advantages);
        Assert.Contains(state.Issues, i => i.Code == "traits.costOpen" && i.TargetId == "trait:3");
    }

    [Fact]
    public void Effects_ChangeDerivedValues()
    {
        var plain = Calculator.Calculate(Hero(_ => { }));
        var state = Calculator.Calculate(Hero(c => c.Traits =
        [
            T("adv.hohe-lebenskraft", 2), T("adv.flink"), T("adv.glueck", 1),
            T("disadv.niedrige-seelenkraft"), T("adv.zauberer"), T("adv.hohe-astralkraft", 3),
        ]));

        Assert.Equal(plain.Derived.LifePoints + 2, state.Derived.LifePoints);
        Assert.Equal(plain.Derived.Speed + 1, state.Derived.Speed);
        Assert.Equal(4, state.Derived.FatePoints);
        Assert.Equal(plain.Derived.Spirit - 1, state.Derived.Spirit);
        Assert.Equal(23, state.Derived.Astral);
        Assert.Null(state.Derived.Karma);
    }

    [Fact]
    public void SpeciesAutomaticTraits_CountTowardLimit_ButAreFree()
    {
        var state = Calculator.Calculate(Hero(c =>
        {
            c.Species = "species.elf";
            c.SpeciesAttributeChoices = ["KK"];
        }));

        Assert.Contains(state.Traits, t => t.Automatic && t.Definition.Id == "adv.zauberer");
        Assert.Equal(0, state.Ap.Advantages);
        Assert.Equal(30, state.AdvantageApTotal);
        Assert.Equal(20, state.Derived.Astral); // automatischer Zauberer
    }

    [Fact]
    public void AdvantageLimit_IncludesSpeciesShare()
    {
        var state = Calculator.Calculate(Hero(c =>
        {
            c.Species = "species.elf";
            c.SpeciesAttributeChoices = ["KK"];
            c.Traits = [T("adv.glueck", 2)]; // 60 + 30 automatisch = 90
        }));

        Assert.Contains(state.Issues, i => i.Code == "traits.advantageMax");
    }

    [Fact]
    public void DisadvantageLimit_IsChecked()
    {
        var state = Calculator.Calculate(Hero(c => c.Traits = [T("disadv.pech", 3), T("disadv.niedrige-seelenkraft")]));

        Assert.Equal(-85, state.DisadvantageApTotal);
        Assert.Contains(state.Issues, i => i.Code == "traits.disadvantageMax");
    }

    [Fact]
    public void Prerequisites_ForbiddenCombinationAndSpecies()
    {
        var state = Calculator.Calculate(Hero(c =>
        {
            c.Species = "species.elf";
            c.SpeciesAttributeChoices = ["KK"];
            c.Traits = [T("adv.glueck"), T("disadv.pech"), T("adv.allerweltsname")];
        }));

        Assert.Contains(state.Issues, i => i.Code == "traits.prerequisite" && i.TargetId == "trait:0" && i.Message.Contains("Pech"));
        Assert.Contains(state.Issues, i => i.Code == "traits.prerequisite" && i.TargetId == "trait:2" && i.Message.Contains("Elfen"));
    }

    [Fact]
    public void Prerequisites_ExclusiveGroupAndOriginRecommendation()
    {
        var state = Calculator.Calculate(Hero(c => c.Traits =
        [
            T("adv.feenblut-dryade"), T("adv.drachenblut-gletscherwurm"), T("adv.nichtschlaefer"),
        ]));

        Assert.Contains(state.Issues, i => i.Code == "traits.prerequisite" && i.TargetId == "trait:0");
        Assert.Contains(state.Issues, i => i.Code == "traits.prerequisite" && i.TargetId == "trait:2" &&
                                           i.Severity == IssueSeverity.Warning);

        // Elfen haben Nichtschläfer als dringend empfohlenen Vorteil
        var elf = Calculator.Calculate(Hero(c =>
        {
            c.Species = "species.elf";
            c.SpeciesAttributeChoices = ["KK"];
            c.Traits = [T("adv.nichtschlaefer")];
        }));
        Assert.DoesNotContain(elf.Issues, i => i.Code == "traits.prerequisite");
    }

    [Fact]
    public void ManualCost_IsEnteredByPlayer()
    {
        var open = Calculator.Calculate(Hero(c => c.Traits = [T("disadv.schlechte-eigenschaft")]));
        Assert.Contains(open.Issues, i => i.Code == "traits.costOpen");

        var entered = Calculator.Calculate(Hero(c =>
            c.Traits = [new TraitEntry { Id = "disadv.schlechte-eigenschaft", ManualAp = -5, Specification = "Neugier" }]));
        Assert.Equal(-5, entered.Ap.Disadvantages);
    }
}
