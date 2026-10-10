using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.Core.Tests;

/// <summary>Traditionen, AsP/KaP und Voraussetzungen der Professionen (Meilenstein 4c).</summary>
public class TraditionTests
{
    private static readonly CharacterCalculator Calculator = new(TestRules.Data);

    private static Character Hero(Action<CreationLayer> setup)
    {
        var c = new Character();
        SourceSelection.Default(TestRules.Data.Books).ApplyTo(c, TestRules.Data.Books);
        setup(c.Creation);
        return c;
    }

    private static TraitEntry Entry(string id) => new() { Id = id };

    [Fact]
    public void TraditionsAreLoaded_WithPrimaryAttribute()
    {
        var gildenmagier = TestRules.Data.FindSpecialAbility("sf.tradition-gildenmagier");
        Assert.NotNull(gildenmagier);
        Assert.Equal(new TraditionInfo(AbilityDomain.Magical, "KL"), gildenmagier.Tradition);
        Assert.Equal(155, gildenmagier.Cost.Ap);

        Assert.True(TestRules.Data.FindSpecialAbility("sf.tradition-animisten")!.Tradition!.HalfPrimaryAttribute);
        var praios = TestRules.Data.FindSpecialAbility("sf.tradition-praioskirche")!.Tradition!;
        Assert.Equal(AbilityDomain.Karmal, praios.Kind);
        Assert.Equal("KL", praios.PrimaryAttribute);
        Assert.Contains("Etikette", praios.FavouredSkillsText);
    }

    [Fact]
    public void Astral_IsBasePlusPrimaryAttributeOfTradition()
    {
        // Wiki, Heldenerschaffung Schritt 12: AE = Grundwert Zauberer (20) + Leiteigenschaft der Tradition ± Vor-/Nachteile
        var withoutTradition = Calculator.Calculate(Hero(c =>
        {
            c.Attributes["KL"] = 14;
            c.Traits = [Entry("adv.zauberer")];
        }));
        Assert.Equal(20, withoutTradition.Derived.Astral);
        Assert.Contains(withoutTradition.Issues, i => i.Code == "magic.noTradition");

        var mage = Calculator.Calculate(Hero(c =>
        {
            c.Attributes["KL"] = 14;
            c.Traits = [Entry("adv.zauberer")];
            c.SpecialAbilities = [Entry("sf.tradition-gildenmagier")];
        }));
        Assert.Equal(34, mage.Derived.Astral);
        Assert.DoesNotContain(mage.Issues, i => i.Code is "magic.noTradition" or "traits.prerequisite");
        Assert.Equal(155, mage.Ap.SpecialAbilities);

        // Animisten rechnen die Leiteigenschaft nur zur Hälfte (aufgerundet) ein: 20 + 13/2↑ = 27
        var animist = Calculator.Calculate(Hero(c =>
        {
            c.Attributes["IN"] = 13;
            c.Traits = [Entry("adv.zauberer")];
            c.SpecialAbilities = [Entry("sf.tradition-animisten")];
        }));
        Assert.Equal(27, animist.Derived.Astral);
    }

    [Fact]
    public void Karma_IsBasePlusPrimaryAttribute_AndOnlyOneTradition()
    {
        var priest = Calculator.Calculate(Hero(c =>
        {
            c.Attributes["KL"] = 14;
            c.Traits = [Entry("adv.geweihter")];
            c.SpecialAbilities = [Entry("sf.tradition-praioskirche")];
        }));
        Assert.Equal(34, priest.Derived.Karma);
        Assert.Null(priest.Derived.Astral);

        var two = Calculator.Calculate(Hero(c =>
        {
            c.Traits = [Entry("adv.geweihter")];
            c.SpecialAbilities = [Entry("sf.tradition-praioskirche"), Entry("sf.tradition-boronkirche")];
        }));
        Assert.Contains(two.Issues, i => i.Code == "karma.multipleTraditions" && i.TargetId == "sf:1");
    }

    [Fact]
    public void TraditionRequiresZauberer()
    {
        var state = Calculator.Calculate(Hero(c => c.SpecialAbilities = [Entry("sf.tradition-gildenmagier")]));
        Assert.Contains(state.Issues, i => i.Code == "traits.prerequisite" && i.Message.Contains("Zauberer"));
    }

    [Fact]
    public void PrimaryAttributePrerequisite_UsesTraditionAttribute()
    {
        // Abgeschwächter Zauber: Vorteil Zauberer, Leiteigenschaft der Tradition 13
        var low = Calculator.Calculate(Hero(c =>
        {
            c.Attributes["KL"] = 12;
            c.Traits = [Entry("adv.zauberer")];
            c.SpecialAbilities = [Entry("sf.tradition-gildenmagier"), Entry("sf.abgeschwaechter-zauber")];
        }));
        Assert.Contains(low.Issues, i => i.Code == "traits.prerequisite" && i.TargetId == "sf:1" && i.Message.Contains("KL"));

        var high = Calculator.Calculate(Hero(c =>
        {
            c.Attributes["KL"] = 13;
            c.Traits = [Entry("adv.zauberer")];
            c.SpecialAbilities = [Entry("sf.tradition-gildenmagier"), Entry("sf.abgeschwaechter-zauber")];
        }));
        Assert.DoesNotContain(high.Issues, i => i.Code == "traits.prerequisite");
    }

    [Fact]
    public void Profession_PreselectsPrerequisites_WithoutCountingThemInThePackage()
    {
        var c = new Character();
        Templates.ChangeProfession(c.Creation, "profession.gildenloser-magier-nach-agrimeton", TestRules.Data);
        Assert.Contains(c.Creation.Traits, t => t.Id == "adv.zauberer" && t.Source == Templates.ProfessionSource);
        Assert.Contains(c.Creation.SpecialAbilities, t => t.Id == "sf.tradition-gildenmagier" && t.Source == Templates.ProfessionSource);

        var profession = TestRules.Data.FindProfession("profession.gildenloser-magier-nach-agrimeton")!;
        var cost = new PackageCosts(TestRules.Data).ProfessionCost(profession, null, []);
        Assert.True(cost.SpecialAbilities < 155, "Die Tradition gehört nicht zum Paket.");

        // Wechsel entfernt die Vorauswahl wieder; selbst gewählte Einträge bleiben.
        c.Creation.Traits.Add(new TraitEntry { Id = "adv.glueck", Level = 1 });
        Templates.ChangeProfession(c.Creation, "profession.streuner", TestRules.Data);
        Assert.DoesNotContain(c.Creation.Traits, t => t.Id == "adv.zauberer");
        Assert.DoesNotContain(c.Creation.SpecialAbilities, t => t.Id == "sf.tradition-gildenmagier");
        Assert.Contains(c.Creation.Traits, t => t.Id == "adv.glueck");
    }
}
