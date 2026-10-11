using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.Core.Tests;

/// <summary>Zauber und Liturgien (Meilenstein 6).</summary>
public class SpellTests
{
    private static readonly RuleData Rules = TestRules.Data;
    private static readonly CharacterCalculator Calculator = new(Rules);

    private static Character Mage(Action<CreationLayer>? setup = null)
    {
        var c = new Character();
        SourceSelection.Default(Rules.Books).ApplyTo(c, Rules.Books);
        c.Creation.Attributes["MU"] = 14;
        c.Creation.Attributes["KL"] = 14;
        c.Creation.Attributes["CH"] = 13;
        c.Creation.Traits.Add(new TraitEntry { Id = "adv.zauberer" });
        c.Creation.SpecialAbilities.Add(new TraitEntry { Id = "sf.tradition-gildenmagier" });
        setup?.Invoke(c.Creation);
        return c;
    }

    private static SpellEntry Spell(string id, int? value = null) => new() { Id = id, Value = value };

    [Fact]
    public void SpellsAreLoaded()
    {
        var ignifaxius = Rules.FindSpell("spell.ignifaxius")!;
        Assert.Equal(SpellGroup.Spell, ignifaxius.Group);
        Assert.Equal(["MU", "KL", "CH"], ignifaxius.Check);
        Assert.Equal(ImprovementCost.C, ignifaxius.ImprovementCost);
        Assert.Contains("Gildenmagier", ignifaxius.Traditions!);
        Assert.Equal(SpellGroup.Cantrip, Rules.FindSpell("cantrip.feuerfinger")!.Group);
        Assert.Equal(12, Rules.Spells.Count(s => s.Group == SpellGroup.Blessing));
        var liturgy = Rules.FindSpell("liturgy.bann-der-dunkelheit")!;
        Assert.True(liturgy.IsKarmal);
        Assert.Contains("Praios", liturgy.Traditions!);
        Assert.Contains("Ordnung", liturgy.Aspects!);
        // Magische Handlungen: Hexenflüche, Herrschafts- und Geodenrituale laut Übersichtsseite Faktor B
        foreach (var category in new[] { "Hexenflüche", "Herrschaftsrituale", "Geodenrituale" })
            Assert.All(Rules.Spells.Where(s => s.Category == category), s => Assert.Equal(ImprovementCost.B, s.ImprovementCost));
        Assert.All(Rules.Spells.Where(s => s.Group == SpellGroup.MagicalAction), s => Assert.True(s.HasValue));
    }

    [Fact]
    public void Enhancements_AreLoadedFromWiki()
    {
        var ignifaxius = Rules.FindSpell("spell.ignifaxius")!;
        Assert.Equal(5, ignifaxius.Enhancements.Count);
        var first = ignifaxius.Enhancements[0];
        Assert.Equal(("Intensiver Strahl", 8, 3), (first.Name, first.MinValue, first.Ap));
        Assert.Contains("Brennend", first.RuleText);
        Assert.Equal(["zwei-gegenstaende"], Rules.FindSpell("spell.analys-arkanstruktur")!.FindEnhancement("drei-gegenstaende")!.Requires);
        Assert.True(Rules.Spells.Count(s => s.IsKarmal && s.Enhancements.Count > 0) > 300);
    }

    [Fact]
    public void Enhancements_CostTheirAp_AndNeedTheSkillValue()
    {
        var ok = Calculator.Calculate(Mage(c =>
            c.Spells = [new SpellEntry { Id = "spell.ignifaxius", Value = 10, Enhancements = ["intensiver-strahl", "groessere-reichweite"] }]));
        // Ignifaxius (C) auf 10: Aktivierung 3 + 30 = 33, dazu 3 + 3 AP für die Erweiterungen
        Assert.Equal(39, ok.Ap.Spells);
        Assert.Equal(6, ok.Spells[0].EnhancementAp);
        Assert.DoesNotContain(ok.Issues, i => i.Code.StartsWith("spell.enhancement", StringComparison.Ordinal));

        var low = Calculator.Calculate(Mage(c =>
            c.Spells = [new SpellEntry { Id = "spell.ignifaxius", Value = 9, Enhancements = ["groessere-reichweite"] }]));
        Assert.Contains(low.Issues, i => i.Code == "spell.enhancement.value" && i.Message.Contains("FW 10"));
    }

    [Fact]
    public void Enhancements_CheckRequiredEnhancement_AndForeignSpells()
    {
        var state = Calculator.Calculate(Mage(c =>
            c.Spells = [new SpellEntry { Id = "spell.analys-arkanstruktur", Value = 12, Enhancements = ["drei-gegenstaende"] }]));
        Assert.Contains(state.Issues, i => i.Code == "spell.enhancement.requires" && i.Message.Contains("Zwei Gegenstände"));

        var foreign = Rules.Spells.First(s => s.Group == SpellGroup.Spell && s.Enhancements.Count > 0 &&
                                              s.Traditions?.Contains("Hexen") == true && !s.Traditions.Contains("Gildenmagier") &&
                                              !s.Traditions.Contains("Allgemein"));
        var foreignState = Calculator.Calculate(Mage(c =>
            c.Spells = [new SpellEntry { Id = foreign.Id, Value = 8, Enhancements = [foreign.Enhancements[0].Id] }]));
        Assert.Contains(foreignState.Issues, i => i.Code == "spell.enhancement.foreign");
    }

    [Fact]
    public void Cost_IsActivationPlusTable_CantripOneAp()
    {
        // Ignifaxius (C): Aktivierung 3 + 6 × 3 = 21; Feuerfinger 1
        var state = Calculator.Calculate(Mage(c => c.Spells = [Spell("spell.ignifaxius", 6), Spell("cantrip.feuerfinger")]));
        Assert.Equal(22, state.Ap.Spells);
        Assert.DoesNotContain(state.Issues, i => i.TargetId?.StartsWith("spell:") == true && i.Severity != IssueSeverity.Info);
    }

    [Fact]
    public void Max_HighestCheckAttributePlusTwo_CreationLimit_And14WithoutPropertyKnowledge()
    {
        var state = Calculator.Calculate(Mage(c => c.Spells = [Spell("spell.ignifaxius", 11)]));
        Assert.Contains(state.Issues, i => i.Code == "spell.creationMax"); // erfahren: 10
        Assert.Equal(10, state.Spells[0].Max);

        var advanced = Mage(c => c.Spells = [Spell("spell.ignifaxius", 15)]);
        advanced.Creation.Attributes["MU"] = 16;
        advanced.Phase = CharacterPhase.Advancement;
        var later = Calculator.Calculate(advanced);
        Assert.Equal(14, later.Spells[0].Max); // ohne Merkmalskenntnis
        Assert.Contains(later.Issues, i => i.Code == "spell.max" && i.Message.Contains("Merkmalskenntnis"));
    }

    [Fact]
    public void PropertyKnowledge_LiftsLimit14_AndNeedsThreeSpellsOfProperty()
    {
        var c = Mage(cr =>
        {
            cr.Spells = [Spell("spell.ignifaxius", 15)];
            cr.SpecialAbilities.Add(new TraitEntry { Id = "sf.merkmalskenntnis", Option = "Elementar" });
        });
        c.Creation.Attributes["MU"] = 16;
        c.Phase = CharacterPhase.Advancement;
        var state = Calculator.Calculate(c);
        Assert.Equal(18, state.Spells[0].Max);
        Assert.DoesNotContain(state.Issues, i => i.Code == "spell.max");
        Assert.Equal(10, state.SpecialAbilities.Single(s => s.Definition.Id == "sf.merkmalskenntnis").Ap);
        Assert.Contains(state.Issues, i => i.Code == "traits.prerequisite" && i.Message.Contains("3 Zauber des Merkmals Elementar auf 10"));

        // zweite Merkmalskenntnis kostet 20 AP
        c.Creation.SpecialAbilities.Add(new TraitEntry { Id = "sf.merkmalskenntnis", Option = "Einfluss" });
        Assert.Equal(20, Calculator.Calculate(c).SpecialAbilities.Last().Ap);
    }

    [Fact]
    public void Binding_ReducesAstral_RepurchaseCostsTwoApPerPoint()
    {
        var c = Mage(cr => cr.SpecialAbilities.Add(new TraitEntry { Id = "sf.bindung-des-stabes" }));
        var state = Calculator.Calculate(c);
        Assert.Equal(20 + 14 - 2, state.Derived.Astral); // Bindungskosten 2 permanente AsP
        Assert.Equal(2, state.Energies.Single(e => e.Id == Energies.AstralLost).Value);

        c.Creation.BoughtEnergies[Energies.AstralRepurchased] = 2;
        state = Calculator.Calculate(c);
        Assert.Equal(34, state.Derived.Astral);
        Assert.Equal(4, state.Ap.Energies);
        Assert.DoesNotContain(state.Issues, i => i.TargetId?.StartsWith("energy:") == true);

        c.Creation.BoughtEnergies[Energies.AstralRepurchased] = 3;
        Assert.Contains(Calculator.Calculate(c).Issues, i => i.Code == "energy.repurchaseMax");
    }

    [Fact]
    public void Buying_NeedsLostPointsRepurchasedFirst()
    {
        var c = Mage(cr => cr.SpecialAbilities.Add(new TraitEntry { Id = "sf.bindung-des-stabes" }));
        c.Phase = CharacterPhase.Advancement;
        c.Creation.BoughtEnergies[Energies.Astral] = 1;
        Assert.Contains(Calculator.Calculate(c).Issues, i => i.Code == "energy.repurchaseFirst");
        c.Creation.BoughtEnergies[Energies.AstralRepurchased] = 2;
        Assert.DoesNotContain(Calculator.Calculate(c).Issues, i => i.Code == "energy.repurchaseFirst");
    }

    [Fact]
    public void Profession_BindingIsRepurchased_PackageMatchesWiki()
    {
        var c = new Character();
        Templates.ChangeProfession(c.Creation, "profession.gildenloser-magier-nach-agrimeton", Rules);
        Assert.Equal(2, c.Creation.BoughtEnergies[Energies.AstralRepurchased]);
        var profession = Rules.FindProfession("profession.gildenloser-magier-nach-agrimeton")!;
        var cost = new PackageCosts(Rules).ProfessionCost(profession, null, []);
        Assert.Null(cost.WikiMismatch);
        Assert.Equal(profession.WikiAp, cost.Total);
    }

    [Fact]
    public void ElementPlaceholder_BecomesChoice()
    {
        var elementarist = Rules.ProfessionForLevel(Rules.Professions.First(p => p.Name == "Elementarist"), "erfahren");
        var faxius = elementarist.SpellChoices.Single(c => c.Options.Contains("spell.ignifaxius"));
        Assert.Equal(10, faxius.Value);
        Assert.Contains("spell.orcanofaxius", faxius.Options);
    }

    private static SpellDefinition Action(string category) => Rules.Spells.First(s => s.Category == category);

    [Fact]
    public void MagicalActions_BoughtLikeSpells_TraditionAndPrerequisites()
    {
        // Hexenfluch (B): Aktivierung 2 + 4 × 2 = 10; zählt zur Höchstzahl, ist aber kein Fremdzauber
        var curse = Action("Hexenflüche");
        var hexe = new Character();
        hexe.Creation.Traits.Add(new TraitEntry { Id = "adv.zauberer" });
        hexe.Creation.SpecialAbilities.Add(new TraitEntry { Id = "sf.tradition-hexen" });
        hexe.Creation.Spells = [Spell(curse.Id, 4)];
        var state = Calculator.Calculate(hexe);
        Assert.Equal(10, state.Ap.Spells);
        Assert.False(state.Spells[0].Foreign);
        Assert.DoesNotContain(state.Issues, i => i.Code is "spell.tradition" or "spell.prerequisite");

        // Gildenmagier: Fluch gehört zu einer anderen Tradition; Elfenlied braucht Zweistimmigen Gesang
        var mage = Calculator.Calculate(Mage(c => c.Spells = [Spell(curse.Id, 0), Spell(Action("Elfenlieder").Id, 0)]));
        Assert.Contains(mage.Issues, i => i.Code == "spell.tradition" && i.TargetId == "spell:0");
        Assert.Contains(mage.Issues, i => i.Code == "spell.prerequisite" && i.Message.Contains("Zweistimmiger Gesang"));

        // Zauberrunen auch mit Zauberrunenkunde
        var rune = Action("Zauberrunen");
        var withSf = Calculator.Calculate(Mage(c =>
        {
            c.Spells = [Spell(rune.Id, 0)];
            c.SpecialAbilities.Add(new TraitEntry { Id = Rules.SpecialAbilities.First(s => s.Name == "Zauberrunenkunde").Id });
        }));
        Assert.DoesNotContain(withSf.Issues, i => i.Code == "spell.tradition" && i.TargetId == "spell:0");
    }

    [Fact]
    public void FactorOption_DeterminesCost()
    {
        var ward = Rules.Spells.Single(s => s.Name == "Bannzeichen wider (Wesenheit)");
        var open = Calculator.Calculate(Mage(c => c.Spells = [Spell(ward.Id, 2)]));
        Assert.Contains(open.Issues, i => i.Code == "spell.factorOption");
        var demons = Calculator.Calculate(Mage(c => c.Spells = [new SpellEntry { Id = ward.Id, Value = 2, Option = "Dämonen" }]));
        Assert.Equal(3 + 3 + 3, demons.Ap.Spells); // C: Aktivierung 3 + 2 × 3
    }

    [Fact]
    public void ForeignSpells_AndCountAreLimitedInCreation()
    {
        var foreign = Rules.Spells.Where(s => s.Group == SpellGroup.Spell && s.Traditions is { } t &&
                                              !t.Contains("Gildenmagier") && !t.Contains("Allgemein")).Take(3).ToList();
        var state = Calculator.Calculate(Mage(c => c.Spells = foreign.Select(f => Spell(f.Id, 0)).ToList()));
        Assert.All(state.Spells, s => Assert.True(s.Foreign));
        Assert.Contains(state.Issues, i => i.Code == "spell.creationForeign"); // erfahren: 2

        var many = Rules.Spells.Where(s => s.Group == SpellGroup.Spell && s.Traditions?.Contains("Gildenmagier") == true).Take(13).ToList();
        var full = Calculator.Calculate(Mage(c => c.Spells = many.Select(f => Spell(f.Id, 0)).ToList()));
        Assert.Contains(full.Issues, i => i.Code == "spell.creationCount"); // erfahren: 12
    }

    [Fact]
    public void SpellsNeedTheGift()
    {
        var c = new Character();
        c.Creation.Spells = [Spell("spell.ignifaxius", 0), Spell("liturgy.bann-der-dunkelheit", 0)];
        var state = Calculator.Calculate(c);
        Assert.Equal(2, state.Issues.Count(i => i.Code == "spell.noGift"));
    }

    [Fact]
    public void Liturgy_OfOtherGod_IsWarned()
    {
        var c = new Character();
        c.Creation.Traits.Add(new TraitEntry { Id = "adv.geweihter" });
        c.Creation.SpecialAbilities.Add(new TraitEntry { Id = "sf.tradition-boronkirche" });
        c.Creation.Spells = [Spell("liturgy.bann-der-dunkelheit", 0)];
        Assert.Contains(Calculator.Calculate(c).Issues, i => i.Code == "spell.tradition");
    }

    [Fact]
    public void Profession_TemplateSpells_AndCantripChoice()
    {
        var c = new Character();
        Templates.ChangeProfession(c.Creation, "profession.gildenloser-magier-nach-agrimeton", Rules);
        var profession = Rules.FindProfession("profession.gildenloser-magier-nach-agrimeton")!;
        Assert.Contains(c.Creation.Spells, s => s.Id == "spell.ignifaxius" && s.Value == 6 && s.Source == Templates.ProfessionSource);
        var choice = Assert.Single(profession.SpellChoices);
        Assert.Contains("cantrip.feuerfinger", choice.Options);

        Templates.ChooseProfessionSpell(c.Creation, 0, "cantrip.feuerfinger");
        Templates.ChooseProfessionSpell(c.Creation, 0, "cantrip.flammenhaar");
        Assert.DoesNotContain(c.Creation.Spells, s => s.Id == "cantrip.feuerfinger");
        Assert.Contains(c.Creation.Spells, s => s.Id == "cantrip.flammenhaar");

        Templates.ChangeProfession(c.Creation, "profession.streuner", Rules);
        Assert.Empty(c.Creation.Spells);
    }

    [Fact]
    public void Advancement_LearnAndRaiseSpell()
    {
        var c = Mage();
        Phases.FinishCreation(c, Calculator.Calculate(c), Rules);
        Phases.AwardAp(c, 100, null);
        var state = Calculator.Calculate(c);
        var working = state.Advancement!.Working.Clone();
        working.Spells.Add(Spell("spell.ignifaxius", 0));
        Phases.Record(c, state, working, Rules);
        state = Calculator.Calculate(c);
        working = state.Advancement!.Working.Clone();
        working.Spells[0].Value = 3;
        Phases.Record(c, state, working, Rules);

        state = Calculator.Calculate(c);
        Assert.IsType<LearnEntry>(c.Log[^2]);
        Assert.Equal(3, state.Advancement!.Entries[^2].Ap); // Aktivierung C
        Assert.Equal(9, state.Advancement.Entries[^1].Ap);
        Assert.Equal(3, state.Spells[0].Value);
        Assert.Equal(0, state.Spells[0].Min);
    }
}
