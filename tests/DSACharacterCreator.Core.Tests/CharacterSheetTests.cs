using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;
using DSACharacterCreator.Core.Sheet;

namespace DSACharacterCreator.Core.Tests;

/// <summary>Charakterbogen (Meilenstein 7c): Inhalt als Texte für Ansicht und PDF.</summary>
public class CharacterSheetTests
{
    private static readonly RuleData Rules = TestRules.Data;
    private static readonly CharacterCalculator Calculator = new(Rules);

    private static CharacterSheet Sheet(Character c) => CharacterSheet.Create(c, Calculator.Calculate(c), Rules);

    private static Character Mage()
    {
        var c = new Character { Name = "Alrik" };
        SourceSelection.Default(Rules.Books).ApplyTo(c, Rules.Books);
        c.Creation.Species = "species.mensch";
        Templates.ChangeCulture(c.Creation, "culture.mittelreicher", Rules);
        Templates.ChangeProfession(c.Creation, "profession.gildenloser-magier-nach-agrimeton", Rules);
        c.Creation.Traits.Add(new TraitEntry { Id = "adv.begabung", Option = "skill.klettern" });
        c.Equipment.Add(new EquipmentEntry { Id = "weapon.dolch", Equipped = true });
        c.Equipment.Add(new EquipmentEntry { Id = "item.lederrucksack", Quantity = 2 });
        return c;
    }

    [Fact]
    public void Sheet_ContainsHeaderValuesAndLists()
    {
        var sheet = Sheet(Mage());
        Assert.Equal("Alrik", sheet.Name);
        Assert.Contains(sheet.Header, f => f.Label == "Profession" && f.Value == "Gildenloser Magier nach Agrimeton");
        Assert.Equal(["MU", "KL", "IN", "CH", "FF", "GE", "KO", "KK"], sheet.Attributes.Select(a => a.Short));
        Assert.Contains(sheet.Derived, v => v.Short == "AsP");
        Assert.Contains("Begabung (Klettern)", sheet.Advantages);
        Assert.Contains("Garethi III", sheet.Languages);
        Assert.Contains(sheet.SpecialAbilities, g => g.Title == "Magische Sonderfertigkeiten" && g.Items.Contains("Tradition (Gildenmagier)"));
        Assert.Equal(5, sheet.Skills.Count);
        Assert.Equal(59, sheet.Skills.Sum(g => g.Items.Count));
        Assert.Equal("MU/GE/KK (8/8/8)", sheet.Skills.SelectMany(g => g.Items).Single(s => s.Name == "Klettern").Check);
    }

    [Fact]
    public void Sheet_CombatSpellsAndEquipment()
    {
        var sheet = Sheet(Mage());
        // Nahkampf vor Fernkampf
        var firstRanged = sheet.CombatTechniques.ToList().FindIndex(c => c.Parry.StartsWith("FK"));
        Assert.True(sheet.CombatTechniques.Skip(firstRanged).All(c => c.Parry.StartsWith("FK")));
        var dagger = Assert.Single(sheet.Weapons);
        Assert.StartsWith("AT ", dagger.Values);
        var spells = sheet.Spells.Single(g => g.Title == "Zauber").Items;
        var ignifaxius = spells.Single(s => s.Name == "Ignifaxius");
        Assert.Equal("C", ignifaxius.Factor);
        Assert.StartsWith("8 AsP", ignifaxius.Details);
        var backpack = Assert.Single(sheet.Items);
        Assert.Equal((2, "4 Stein"), (backpack.Quantity, backpack.Weight));
        Assert.Contains(sheet.Possessions, f => f.Label == "Geld");
    }

    [Fact]
    public void SpellDetails_ShortensWikiFields()
    {
        Assert.Equal("8 AsP (Kosten sind nicht modifizierbar) · 2 Aktion(en) · RW 16 Schritt · WD sofort",
            SheetBuilder.SpellDetails("Zauberdauer: 2 Aktion(en)\nAsP-Kosten: 8 AsP (Kosten sind nicht modifizierbar)\n" +
                                      "Reichweite: 16 Schritt\nWirkungsdauer: sofort\nZielkategorie: alle"));
        Assert.Equal("4 KaP · 1 Aktion", SheetBuilder.SpellDetails("Liturgiedauer: 1 Aktion\nKaP-Kosten: 4"));
    }
}
