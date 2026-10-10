using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.ViewModels;

namespace DSACharacterCreator.UiTests;

/// <summary>Magie & Götterwirken (Meilenstein 6).</summary>
public sealed class MagicPageTests : IDisposable
{
    private readonly TestShell _app = new();

    public MagicPageTests()
    {
        _app.List.NewCommand.Execute(null);
    }

    public void Dispose() => _app.Dispose();

    private EditorViewModel Editor => _app.Editor;

    private MagicPageViewModel Page => Editor.Magic;

    private void Pick(string name) =>
        Editor.Picker!.Groups.SelectMany(g => g.Items).First(i => i.Name == name).PickCommand!.Execute(null);

    [Fact]
    public void MageProfession_FillsSpells_AndCantripChoice()
    {
        Assert.False(Page.ShowMagic);
        Editor.Basics.ChooseProfessionCommand.Execute(null);
        Pick("Gildenloser Magier nach Agrimeton");

        Assert.True(Page.ShowMagic);
        var ignifaxius = Page.Spells.Single(s => s.Id == "spell.ignifaxius");
        Assert.Equal(6, ignifaxius.Value);
        Assert.Contains("aus Profession", ignifaxius.Detail);
        Assert.Empty(Page.Cantrips);

        var choice = Assert.Single(Editor.Basics.SpellChoices);
        choice.Selected = choice.Options.Single(o => o.Label == "Feuerfinger");
        Assert.Equal("Feuerfinger", Assert.Single(Page.Cantrips).Name);
        Assert.Equal(0, Editor.State.Ap.Unmodeled); // Vorlage vollständig strukturiert
    }

    [Fact]
    public void AddSpell_OwnTraditionFirst_RaiseAndRemove()
    {
        Editor.Apply(c =>
        {
            c.Traits.Add(new TraitEntry { Id = "adv.zauberer" });
            c.SpecialAbilities.Add(new TraitEntry { Id = "sf.tradition-gildenmagier" });
            c.Attributes["KL"] = 12;
        });
        Page.AddSpellCommand.Execute(null);
        Assert.Equal("Zaubersprüche", Editor.Picker!.Groups[0].Title.Split(" (")[0]);
        Assert.DoesNotContain("fremd", Editor.Picker.Groups[0].Title);
        Pick("Ignifaxius");

        var row = Assert.Single(Page.Spells);
        Assert.Equal(0, row.Value);
        Assert.Equal(3, Editor.State.Ap.Spells); // Aktivierung C
        row.IncreaseCommand.Execute(null);
        Assert.Equal(6, Editor.State.Ap.Spells);
        row.RemoveCommand.Execute(null);
        Assert.Empty(Page.Spells);
    }

    [Fact]
    public void Begabung_OffersSpells()
    {
        Editor.Traits.AddAdvantageCommand.Execute(null);
        Pick("Begabung");
        var row = Assert.Single(Editor.Traits.Advantages);
        row.SelectedOption = row.Options.First(o => o.Id == "spell.ignifaxius");
        Assert.Equal("18 AP", row.ApText); // C-Fertigkeit
    }
}
