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
        // Hexenflüche: Steigerungsfaktor B laut Übersichtsseite
        Assert.All(Rules.Spells.Where(s => s.Category == "Hexenflüche"), s => Assert.Equal(ImprovementCost.B, s.ImprovementCost));
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
