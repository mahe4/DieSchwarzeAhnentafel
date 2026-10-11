using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.Core.Tests;

public class SourceOverviewTests
{
    private static RuleData Rules => TestRules.Data;

    [Fact]
    public void Books_CountEntriesPerPublication()
    {
        var usage = SourceOverview.Books(Rules);

        Assert.Equal(Rules.Books.Books.Count, usage.Count);
        var core = usage.Single(u => u.Book.Id == "grw");
        Assert.True(core.Entries > 500);
        Assert.InRange(core.Exclusive, 1, core.Entries);
        Assert.All(usage, u => Assert.InRange(u.Exclusive, 0, u.Entries));
    }

    [Fact]
    public void DependentChoices_OnlyBookProvidingAChoice_AndDisabledOnes()
    {
        var advantage = Rules.Advantages.First(a =>
            a.Publications.Select(p => p.Book).Distinct().Count() == 1 && a.Publications[0].Book != "grw");
        var book = advantage.Publications[0].Book;
        var character = new Character();
        SourceSelection.Default(Rules.Books).ApplyTo(character, Rules.Books);
        character.Creation.Traits.Add(new TraitEntry { Id = advantage.Id });
        var state = new CharacterCalculator(Rules).Calculate(character);
        var sources = SourceSelection.ForCharacter(character, Rules.Books);

        Assert.Contains(advantage.Name, SourceOverview.DependentChoices(state, sources)[book]);
        Assert.False(SourceOverview.DependentChoices(state, sources).ContainsKey("grw")
                     && SourceOverview.DependentChoices(state, sources)["grw"].Contains(advantage.Name));

        var without = sources.With(book, false);
        Assert.Contains(advantage.Name, SourceOverview.DependentChoices(state, without)[book]);
    }
}
