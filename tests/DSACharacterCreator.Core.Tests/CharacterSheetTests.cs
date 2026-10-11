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
    public void Sheet_HeroDocumentFields()
    {
        var sheet = Sheet(Mage());
        Assert.Equal(["LeP", "AsP", "SK", "ZK", "AW", "INI", "GS"], sheet.Energies.Select(e => e.Short));
        Assert.Equal(2, sheet.Energies.Single(e => e.Short == "AsP").Lost); // Bindung des Stabes
        var dagger = Assert.Single(sheet.MeleeWeapons);
        Assert.Equal(("GE 14", "0/0", "kurz"), (dagger.DamageThreshold, dagger.Modifiers, dagger.Reach));
        Assert.Empty(sheet.RangedWeapons);
        Assert.Equal("KL", sheet.MagicTradition?.PrimaryAttribute);
        Assert.Null(sheet.KarmaTradition);
        var ignifaxius = sheet.Spells.SelectMany(g => g.Items).Single(s => s.Name == "Ignifaxius");
        Assert.Equal(("16 Schritt", "sofort"), (ignifaxius.Range, ignifaxius.Duration));
        Assert.Equal(5, sheet.PainLevels.Count);
        Assert.Equal("≤ 0", sheet.PainLevels[^1].Label);
    }

    [Theory]
    [InlineData(596, "59", "6", "0", "0")]
    [InlineData(12.34, "1", "2", "3", "4")]
    public void Coins_SplitSilverIntoDucatsTalersHellersKreuzers(double silver, string d, string s, string h, string k) =>
        Assert.Equal([d, s, h, k], SheetBuilder.Coins((decimal)silver).Select(c => c.Value));

    [Fact]
    public void Coins_EmptyForDebt() => Assert.Empty(SheetBuilder.Coins(-1));

    [Fact]
    public void SpellDetails_ShortensWikiFields()
    {
        Assert.Equal("8 AsP (Kosten sind nicht modifizierbar) · 2 Aktion(en) · RW 16 Schritt · WD sofort",
            SheetBuilder.SpellDetails("Zauberdauer: 2 Aktion(en)\nAsP-Kosten: 8 AsP (Kosten sind nicht modifizierbar)\n" +
                                      "Reichweite: 16 Schritt\nWirkungsdauer: sofort\nZielkategorie: alle"));
        Assert.Equal("4 KaP · 1 Aktion", SheetBuilder.SpellDetails("Liturgiedauer: 1 Aktion\nKaP-Kosten: 4"));
    }

    [Theory]
    [InlineData(32, 24, 16, 8)]
    [InlineData(30, 22, 15, 7)] // abgerundet: „auf drei Viertel gesunken“
    public void PainThresholds_ThreeQuartersHalfQuarterAndFive(int lep, int i, int ii, int iii)
    {
        Assert.Equal([i, ii, iii, 5], PainRules.Thresholds(lep));
        Assert.Equal(0, PainRules.Level(lep, lep));
        Assert.Equal(1, PainRules.Level(i, lep));
        Assert.Equal(0, PainRules.Level(i + 1, lep));
        Assert.Equal(3, PainRules.Level(iii, lep));
        Assert.Equal(4, PainRules.Level(5, lep));
        Assert.Equal(4, PainRules.Level(0, lep));
    }

    [Fact]
    public void Sheet_PersonalDataSocialStatusPainAndNotes()
    {
        var c = Mage();
        c.Personal.Age = "21";
        c.Personal.HairColor = "  dunkelbraun ";
        c.Personal.Notes = "Sucht seinen Lehrmeister.";
        var sheet = Sheet(c);
        Assert.Equal(["Alter", "Haarfarbe", "Sozialstatus"], sheet.Personal.Select(f => f.Label));
        Assert.Equal("dunkelbraun", sheet.Personal[1].Value);
        Assert.Equal("Frei", sheet.Personal[2].Value); // weder Adel noch Unfrei
        Assert.Equal("Sucht seinen Lehrmeister.", sheet.Notes);
        var lep = Calculator.Calculate(c).Derived.LifePoints!.Value;
        Assert.StartsWith($"Schmerz I bei {lep * 3 / 4} LeP oder weniger", sheet.PainText);

        c.Creation.Traits.Add(new TraitEntry { Id = "adv.adel", Level = 2 });
        Assert.Equal("Adel II", Sheet(c).Personal.Single(f => f.Label == "Sozialstatus").Value);
    }

    [Fact]
    public void PersonalData_RoundTrip()
    {
        var c = Mage();
        c.Personal.Birthplace = "Gareth";
        var loaded = Persistence.CharacterSerializer.Deserialize(Persistence.CharacterSerializer.Serialize(c));
        Assert.Equal("Gareth", loaded.Personal.Birthplace);
    }
}
