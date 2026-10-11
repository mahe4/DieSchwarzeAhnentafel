using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.Core.Tests;

public class PersonalRollsTests
{
    private static RuleData Rules => TestRules.Data;

    [Theory]
    [InlineData("160+2W20", 0, 162, 200)]
    [InlineData("158+1W20+4W6", 0, 163, 202)]
    [InlineData("G-110-2W6", 180, 58, 68)]
    [InlineData("G-80-1W6+2W6", 135, 51, 66)]
    [InlineData("150+3W6-1W6", 0, 147, 167)]
    public void DiceExpression_MinMax(string text, int reference, int min, int max)
    {
        var e = DiceExpression.Parse(text);
        Assert.Equal(min, e.Min(reference));
        Assert.Equal(max, e.Max(reference));
        var random = new Random(1);
        for (var i = 0; i < 200; i++) Assert.InRange(e.Roll(random, reference), min, max);
    }

    [Fact]
    public void DiceExpression_EvenAddsOddSubtracts()
    {
        var e = DiceExpression.Parse("G-100±2W6");
        var random = new Random(3);
        var results = Enumerable.Range(0, 500).Select(_ => e.Roll(random, 180) - 80).ToHashSet();
        // 2W6: gerade 2…12 werden addiert, ungerade 3…11 abgezogen
        Assert.Equal([-11, -9, -7, -5, -3, 2, 4, 6, 8, 10, 12], results.Order());
        Assert.Equal(69, e.Min(180));
        Assert.Equal(92, e.Max(180));
    }

    [Theory]
    [InlineData("")]
    [InlineData("2W")]
    [InlineData("10 20")]
    [InlineData("±5")]
    [InlineData("160+2W20x")]
    public void DiceExpression_RejectsInvalid(string text) => Assert.False(DiceExpression.TryParse(text, out _));

    [Fact]
    public void StartAge_BySpeciesAndLevel()
    {
        // Wiki-Beispiel: Mensch, Erfahren = 16+1W3; Elf = 26+2W6
        Assert.Equal("16+1W3", Rules.StartAge("species.mensch", "erfahren"));
        Assert.Equal("26+2W6", Rules.StartAge("species.elf", "erfahren"));
        var rolls = new PersonalRolls(Rules, new Random(7));
        for (var i = 0; i < 50; i++) Assert.InRange(rolls.Age("species.zwerg", "legendaer")!.Value, 149, 184);
        Assert.Null(rolls.Age(null, "erfahren"));
        Assert.All(Rules.Species, s => Assert.All(Rules.ExperienceLevels, l => Assert.NotNull(Rules.StartAge(s.Id, l.Id))));
    }

    [Fact]
    public void Appearance_EverySpeciesHasTables()
    {
        Assert.All(Rules.Species, s => Assert.NotNull(Rules.FindAppearance(s.Id)));
    }

    [Fact]
    public void Appearance_VariantsInheritAndFollowCulture()
    {
        var human = Rules.FindAppearance("species.mensch")!;
        Assert.Equal("Thorwaler", human.DefaultChoice("culture.thorwaler"));
        Assert.Equal("Mittelländer", human.DefaultChoice("culture.bornlaender"));
        Assert.Equal("Mittelländer", human.DefaultChoice(null));

        var gjalsker = human.Resolve("Gjalsker");
        Assert.Equal("168+2W20", gjalsker.Height); // von den Thorwalern
        Assert.Equal("schwarz", gjalsker.Hair![0].Name);
        Assert.Equal("G-100±2W6", gjalsker.Weight); // von der Spezies
        Assert.Equal("215+1W20", human.Resolve("Rochshaz").Height);
        Assert.Equal("schwarz", human.Resolve("Rochshaz").Hair![0].Name); // von den Trollzackern

        var dwarf = Rules.FindAppearance("species.zwerg")!;
        Assert.Single(dwarf.Choices);
        Assert.Equal("128+2W6", dwarf.Resolve(null).Height);
    }

    [Fact]
    public void Appearance_RollsWithinTables()
    {
        var rolls = new PersonalRolls(Rules, new Random(11));
        var achaz = Rules.FindAppearance("species.achaz")!;
        var table = achaz.Resolve("Waldinsel-Achaz");
        for (var i = 0; i < 50; i++)
        {
            var height = rolls.Height(table)!.Value;
            Assert.InRange(height, 210, 225);
            Assert.InRange(rolls.Weight(table, height)!.Value, height - 132, height - 108);
            Assert.NotNull(rolls.EyeColor(table));
        }
        // Zholochai: Fellfarbe +8 → nie dunkelbraun (1–5)
        var orc = Rules.FindAppearance("species.ork")!;
        Assert.All(Enumerable.Range(0, 50), _ => Assert.NotEqual("dunkelbraun", rolls.HairColor(orc, orc.Resolve("Zholochai"))));
        Assert.Equal("grau", rolls.HairColor(orc, orc.Resolve("Olochtai")));
    }

    [Fact]
    public void Birthdate_DayAndMonth()
    {
        var rolls = new PersonalRolls(Rules, new Random(5));
        var months = Rules.Birthdates.Months.Select(m => m.Name).ToHashSet();
        for (var i = 0; i < 100; i++)
        {
            var parts = rolls.Birthdate().Split(". ");
            Assert.InRange(int.Parse(parts[0]), 1, 30);
            Assert.Contains(parts[1], months);
        }
    }

    [Fact]
    public void Name_FollowsCultureFormat()
    {
        var rolls = new PersonalRolls(Rules, new Random(2));
        var dwarves = Rules.FindCulture("culture.ambosszwerge")!;
        Assert.Matches(@"^\S+ Sohn des \S+$", rolls.Name(dwarves, NameGender.Male));
        Assert.Matches(@"^\S+ Tochter der \S+$", rolls.Name(dwarves, NameGender.Female));

        var middle = Rules.FindCulture("culture.mittelreicher")!;
        var name = rolls.Name(middle, NameGender.Female)!;
        Assert.Contains(name.Split(' ')[0], middle.Names!.Female!);
        Assert.Contains(name.Split(' ', 2)[1], middle.Names!.Family!);

        var thorwal = Rules.FindCulture("culture.thorwaler")!;
        Assert.Matches(@"^\S+ \S+sdottir$", rolls.Name(thorwal, NameGender.Female));

        // Ohne eigene Listen: Namen einer anderen Kultur
        Assert.NotNull(rolls.Name(Rules.FindCulture("culture.nordaventurier"), null));
        // Geschlechtsunabhängige Namen
        Assert.Contains(rolls.Name(Rules.FindCulture("culture.utulus"), NameGender.Female),
            Rules.FindCulture("culture.utulus")!.Names!.Any!);
        Assert.Null(rolls.Name(null, null));
    }

    [Theory]
    [InlineData("weiblich", NameGender.Female)]
    [InlineData("W", NameGender.Female)]
    [InlineData("männlich", NameGender.Male)]
    [InlineData("divers", null)]
    [InlineData(null, null)]
    public void GenderOf_Text(string? text, NameGender? expected) => Assert.Equal(expected, PersonalRolls.GenderOf(text));

    [Theory]
    [InlineData("1,78 Schritt", 178)]
    [InlineData("178 cm", 178)]
    [InlineData("1.9", 190)]
    [InlineData("groß", null)]
    public void ParseHeight_Text(string text, int? expected) => Assert.Equal(expected, PersonalRolls.ParseHeight(text));

    [Fact]
    public void Format_HeightAndWeight()
    {
        Assert.Equal("1,78 Schritt", PersonalRolls.FormatHeight(178));
        Assert.Equal("72 Stein", PersonalRolls.FormatWeight(72));
    }
}
