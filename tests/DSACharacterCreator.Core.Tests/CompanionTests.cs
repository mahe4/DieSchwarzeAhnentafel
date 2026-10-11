using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Persistence;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.Core.Tests;

/// <summary>Vertrautentiere und tierische Begleiter (Kodex des Schwertes S. 153–158, Grundregeln › Vertrautentiere).</summary>
public class CompanionTests
{
    private static readonly RuleData Rules = TestRules.Data;
    private static readonly CharacterCalculator Calculator = new(Rules);
    private static readonly CompanionCalculator Companions = new(Rules);

    /// <summary>Abgeschlossene Hexe mit Vertrautenbindung (bzw. ohne Sonderfertigkeiten).</summary>
    private static Character Hero(string? tradition = "sf.tradition-hexen", bool binding = true)
    {
        var c = new Character();
        SourceSelection.Default(Rules.Books).ApplyTo(c, Rules.Books);
        if (tradition is not null) c.Creation.SpecialAbilities.Add(new TraitEntry { Id = tradition });
        if (binding) c.Creation.SpecialAbilities.Add(new TraitEntry { Id = "sf.vertrautenbindung" });
        Phases.FinishCreation(c, Calculator.Calculate(c), Rules);
        return c;
    }

    private static Companion Add(Character hero, string animal, CompanionKind kind = CompanionKind.Companion)
    {
        var companion = new Companion { Animal = animal, Kind = kind, Name = "Testtier" };
        hero.Companions.Add(companion);
        return companion;
    }

    private static CompanionState Calc(Character hero, Companion companion) =>
        Companions.Calculate(hero, companion, Calculator.Calculate(hero));

    private static void Award(Character hero, int amount) => hero.Log.Add(new ApAwardEntry { Amount = amount });

    private static void Raise(Companion c, RaiseTarget kind, string target, int from, int to) =>
        c.Log.Add(new RaiseEntry { Kind = kind, Target = target, From = from, To = to });

    private static void Acquire(Companion c, string id, string? option = null) =>
        c.Log.Add(new AcquireEntry { Kind = EntryKind.AnimalAbility, Entry = new TraitEntry { Id = id, Option = option } });

    private static bool Has(CompanionState s, string code) => s.Issues.Any(i => i.Code == code);

    [Fact]
    public void Ap_QuarterOfHeroAwards_RoundedOverTheSum()
    {
        var hero = Hero();
        var cat = Add(hero, "animal.bornlaender");
        Award(hero, 25);
        Award(hero, 30);
        Award(hero, 15);

        var ap = Calc(hero, cat).Ap;

        Assert.Equal(70, ap.HeroAwards);
        Assert.Equal(17, ap.Share); // 70 / 4 = 17,5 – je Gutschrift abgerundet wären es nur 6 + 7 + 3 = 16
        Assert.Equal(17, ap.Remaining);
    }

    [Fact]
    public void Ap_CountsOnlyAwardsAfterBinding_PlusBonusAp()
    {
        var hero = Hero();
        Award(hero, 100);
        var dog = Add(hero, "animal.bornlaender");
        dog.BoundAfter = hero.Log[^1].Id;
        Award(hero, 40);
        dog.Log.Add(new ApAwardEntry { Amount = 12, Note = "Meisterentscheid" });

        var state = Calc(hero, dog);

        Assert.Equal(40, state.Ap.HeroAwards);
        Assert.Equal(10, state.Ap.Share);
        Assert.Equal(12, state.Ap.Bonus);
        Assert.Equal(22, state.Ap.Total);
        Assert.Contains("Bonus-AP +12 (Meisterentscheid)", state.Entries[0].Text);
    }

    [Fact]
    public void Ap_MissingBindingEntry_CountsAllAwardsWithWarning()
    {
        var hero = Hero();
        Award(hero, 40);
        var dog = Add(hero, "animal.bornlaender");
        dog.BoundAfter = Guid.NewGuid();

        var state = Calc(hero, dog);

        Assert.Equal(40, state.Ap.HeroAwards);
        Assert.True(Has(state, "companion.boundAfterMissing"));
    }

    [Fact]
    public void Familiar_GetsBonusesAndFreeTricks()
    {
        var hero = Hero();
        var cat = Add(hero, "animal.scheunenkatze", CompanionKind.Familiar);

        var s = Calc(hero, cat);

        Assert.Equal(13, s.Attribute("KL")); // 12 + 1
        Assert.Equal(7, s.Attribute("KO"));  // 6 + 1
        Assert.Equal(20, s.Values.LifePoints); // 10 + 10
        Assert.Equal(15, s.Values.Astral);
        Assert.Equal(2, s.Values.Spirit);      // 1 + 1
        Assert.Equal(-1, s.Values.Toughness);  // −2 + 1
        Assert.Equal(1, s.Values.Protection);  // 0 + 1
        Assert.Equal(4, s.Loyalty.Value);
        Assert.Equal(["Hexensinn", "Zwiegespräch"], s.Abilities.Where(a => a.Source == CompanionAbilitySource.Familiar)
            .Select(a => a.Name).Order());
        Assert.DoesNotContain(s.Issues, i => i.Severity >= IssueSeverity.Warning);
    }

    [Fact]
    public void Familiar_TraditionRenamesAndExcludesTricks()
    {
        var hero = Hero("sf.tradition-geoden");
        var fox = Add(hero, "animal.vielfrass", CompanionKind.Familiar);
        Acquire(fox, "familiar-trick.fluchbote");

        var s = Calc(hero, fox);

        Assert.Contains(s.Abilities, a => a.Definition.Id == "familiar-trick.hexensinn" && a.Name == "Geodensinn");
        Assert.True(Has(s, "companion.trickExcluded"));
    }

    [Fact]
    public void Familiar_ChecksBindingTraditionAndTrickAnimals()
    {
        var hero = Hero(tradition: null, binding: false);
        var raven = Add(hero, "animal.rabe-kohlrabe", CompanionKind.Familiar);
        Acquire(raven, "familiar-trick.katzenplage");

        var s = Calc(hero, raven);

        Assert.True(Has(s, "companion.noBinding"));
        Assert.True(Has(s, "companion.noFamiliarTradition"));
        Assert.True(Has(s, "companion.trickAnimal"));
    }

    [Fact]
    public void Familiar_WithoutTradition_NamesTraditionAndExpression()
    {
        var hero = Hero(tradition: null);
        var s = Calc(hero, Add(hero, "animal.kroete-koschkroete", CompanionKind.Familiar));

        var message = s.Issues.Single(i => i.Code == "companion.noFamiliarTradition").Message;
        Assert.Contains("Tradition (Hexen) mit Schwesternschaft Töchter der Erde", message);
        Assert.Contains("Tradition (Geoden) mit Element Humus", message);
        Assert.Contains("Tradition (Goblinzauberinnen)", message);
        Assert.Equal(["Tradition (Zibilja)"], Companions.FamiliarRequirements("animal.iltis").Where(r => r.Contains("Zibilja")));
        Assert.Empty(Companions.FamiliarRequirements("animal.bornlaender"));
    }

    [Fact]
    public void Familiar_OtherAnimalIsSpecialFamiliar()
    {
        var hero = Hero();
        var s = Calc(hero, Add(hero, "animal.bornlaender", CompanionKind.Familiar));

        Assert.True(Has(s, "companion.specialFamiliar"));
        Assert.Contains("bei keiner Tradition", s.Issues.Single(i => i.Code == "companion.specialFamiliar").Message);
    }

    [Fact]
    public void OnlyOneLivingFamiliar()
    {
        var hero = Hero();
        Add(hero, "animal.scheunenkatze", CompanionKind.Familiar);
        var second = Add(hero, "animal.rabe-kohlrabe", CompanionKind.Familiar);

        Assert.All(Companions.CalculateAll(hero, Calculator.Calculate(hero)), s => Assert.True(Has(s, "companion.familiarCount")));

        second.Deceased = true;
        Assert.All(Companions.CalculateAll(hero, Calculator.Calculate(hero)), s => Assert.False(Has(s, "companion.familiarCount")));
    }

    [Fact]
    public void Raises_CostLikeHeroes_AttacksAndVwInColumnC()
    {
        var hero = Hero();
        Award(hero, 400);
        var dog = Add(hero, "animal.bornlaender");
        Raise(dog, RaiseTarget.Attribute, "MU", 13, 14);           // E: 15
        Raise(dog, RaiseTarget.Skill, "skill.klettern", 0, 2);     // Klettern B: 2 + 2 (von 0)
        Raise(dog, RaiseTarget.Attack, "Biss", 13, 14);            // C: 9
        Raise(dog, RaiseTarget.Defense, "VW", 7, 8);               // C: 3
        Raise(dog, RaiseTarget.Energy, Energies.LifePoints, 0, 2); // D: 4 + 4

        var s = Calc(hero, dog);

        Assert.Equal(14, s.Attribute("MU"));
        Assert.Equal(14, s.Attacks.First(a => a.Name == "Biss").Attack);
        Assert.Equal(8, s.Values.Defense);
        Assert.Equal(19, s.Values.LifePoints);
        var klettern = Rules.FindSkill("skill.klettern")!;
        var skillAp = Rules.Costs.Cost(klettern.ImprovementCost, 0, 2);
        Assert.Equal(15 + skillAp + 9 + 3 + 8, s.Ap.Spent);
    }

    [Fact]
    public void Raise_IsAppliedAsStepsOnTheCurrentValue()
    {
        var hero = Hero();
        var dog = Add(hero, "animal.bornlaender");
        Raise(dog, RaiseTarget.Attribute, "MU", 13, 14);
        Acquire(dog, "animal-advantage.tapfer"); // MU +2

        var s = Calc(hero, dog);

        Assert.Equal(16, s.Attribute("MU")); // 13 + 2 + 1
        Assert.Equal(15 + 15, s.Ap.Spent);
    }

    [Fact]
    public void Loyalty_IsPaidByTheHero()
    {
        var hero = Hero();
        Award(hero, 100);
        var before = Calculator.Calculate(hero).Ap.Remaining;
        var dog = Add(hero, "animal.bornlaender");
        Raise(dog, RaiseTarget.Loyalty, "", 4, 8); // B, 5–8: 4 × 2 AP

        var s = Calc(hero, dog);

        Assert.Equal(8, s.Loyalty.Value);
        Assert.Equal(8, s.Loyalty.HeroAp);
        Assert.Equal(0, s.Ap.Spent);
        Assert.True(s.Entries[0].HeroAp);
        Assert.Equal(8, Calculator.Calculate(hero).Ap.Companions);
        Assert.Equal(before - 8, Calculator.Calculate(hero).Ap.Remaining);
    }

    [Fact]
    public void WildAnimal_StartsWithLoyaltyZero()
    {
        var hero = Hero();
        var wolf = Rules.Animals.First(a => a.Domesticated == false);
        Assert.Equal(0, Calc(hero, Add(hero, wolf.Id)).Loyalty.Value);
    }

    [Fact]
    public void Training_AddsBonusesAndTricks()
    {
        var hero = Hero();
        var dog = Add(hero, "animal.bornlaender");
        Acquire(dog, "animal-training.wachtier"); // +2 MU, +1 IN, +1 AT, +2 VW, Einschüchtern +6, Tricks

        var s = Calc(hero, dog);

        Assert.Equal(15, s.Attribute("MU"));
        Assert.Equal(14, s.Attacks.First(a => a.Name == "Biss").Attack);
        Assert.Equal(9, s.Values.Defense);
        Assert.Equal(12, s.Skills.First(x => x.Definition.Id == "skill.einschuechtern").Value); // 6 + 6
        Assert.Contains(s.Abilities, a => a.Definition.Id == "animal-trick.wache" && a.Source == CompanionAbilitySource.Training);
        Assert.Equal(Rules.FindAnimalAbility("animal-training.wachtier")!.Cost.Ap, s.Ap.Spent);
    }

    [Fact]
    public void Training_OptionChoosesSkill_PercentAndDamageBonuses()
    {
        var hero = Hero();
        var horse = Add(hero, "animal.tulamide");
        Acquire(horse, "animal-training.kampftier"); // +1 TP, +20 % LeP
        var dog = Add(hero, "animal.bornlaender");
        Acquire(dog, "animal-training.huetetier");

        var h = Calc(hero, horse);
        Assert.Equal(78, h.Values.LifePoints); // 65 + 13
        Assert.Equal("1W6+6", h.Attacks.First(a => a.Name == "Tritt").Damage);

        // Jagdtier: „Fährtensuchen- oder Sinnesschärfe-FW +8“ – gewählt Sinnesschärfe
        var hound = Add(hero, "animal.bornlaender");
        Acquire(hound, "animal-training.jagdtier", "skill.sinnesschaerfe");
        var s = Calc(hero, hound);
        Assert.Equal(16, s.Skills.First(x => x.Definition.Id == "skill.sinnesschaerfe").Value); // 8 + 8
    }

    [Fact]
    public void Training_CountAndAnimalAreChecked()
    {
        var hero = Hero();
        var dog = Add(hero, "animal.bornlaender");
        Acquire(dog, "animal-training.wachtier");
        Acquire(dog, "animal-training.huetetier");
        Acquire(dog, "animal-training.kampftier");

        var s = Calc(hero, dog);
        Assert.True(Has(s, "companion.trainingCount"));
        Assert.True(Has(s, "companion.trainingAnimal")); // Kampftier ist beim Bornländer nicht vorgesehen

        // Reittier lässt sich zusätzlich kombinieren
        var horse = Add(hero, "animal.tulamide");
        Acquire(horse, "animal-training.reittier");
        Acquire(horse, "animal-training.kampftier");
        Assert.False(Has(Calc(hero, horse), "companion.trainingCount"));

        // Vertraute dürfen zwei Aufsätze haben
        var cat = Add(hero, "animal.scheunenkatze", CompanionKind.Familiar);
        Acquire(cat, "animal-training.kampftier");
        Acquire(cat, "animal-training.zirkustier");
        Assert.False(Has(Calc(hero, cat), "companion.trainingCount"));
    }

    [Fact]
    public void Trick_PrerequisitesAreChecked()
    {
        var hero = Hero();
        var dog = Add(hero, "animal.bornlaender");
        Acquire(dog, "animal-trick.fass-ii"); // Loyalität 8, Trick Fass I

        var s = Calc(hero, dog);
        Assert.True(Has(s, "companion.trickLoyalty"));
        Assert.True(Has(s, "companion.trickRequires"));

        Raise(dog, RaiseTarget.Loyalty, "", 4, 8);
        Acquire(dog, "animal-trick.fass-i");
        s = Calc(hero, dog);
        Assert.False(Has(s, "companion.trickLoyalty"));
        Assert.False(Has(s, "companion.trickRequires"));
    }

    [Fact]
    public void Remove_RefundsTheAbility_AndManualApOverrides()
    {
        var hero = Hero();
        var dog = Add(hero, "animal.bornlaender");
        Acquire(dog, "animal-trick.sitz");
        dog.Log.Add(new AcquireEntry { Kind = EntryKind.AnimalAbility, Entry = new TraitEntry { Id = "animal-trick.komm", ManualAp = 7 } });
        dog.Log.Add(new RemoveEntry { Kind = EntryKind.AnimalAbility, EntryId = "animal-trick.sitz" });

        var s = Calc(hero, dog);

        Assert.Equal(7, s.Ap.Spent);
        Assert.DoesNotContain(s.Abilities, a => a.Definition.Id == "animal-trick.sitz");
    }

    [Fact]
    public void Overspent_IsAWarning()
    {
        var hero = Hero();
        var dog = Add(hero, "animal.bornlaender");
        Acquire(dog, "animal-trick.sitz");

        Assert.True(Has(Calc(hero, dog), "companion.overspent"));
    }

    [Fact]
    public void Companions_SurviveSaving()
    {
        var hero = Hero();
        var cat = Add(hero, "animal.scheunenkatze", CompanionKind.Familiar);
        cat.FamiliarChoice = "Schöne der Nacht";
        cat.Log.Add(new ApAwardEntry { Amount = 5, Note = "Startausbildung" });
        Raise(cat, RaiseTarget.Attack, "Krallen", 15, 16);
        Acquire(cat, "familiar-trick.katzenplage");

        var loaded = CharacterSerializer.Deserialize(CharacterSerializer.Serialize(hero)).Companions.Single();

        Assert.Equal(CompanionKind.Familiar, loaded.Kind);
        Assert.Equal("Schöne der Nacht", loaded.FamiliarChoice);
        Assert.Equal(3, loaded.Log.Count);
        Assert.Equal(Calc(hero, cat).Ap, Calc(hero, loaded).Ap);
    }
}
