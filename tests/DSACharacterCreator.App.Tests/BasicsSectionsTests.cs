namespace DSACharacterCreator.UiTests;

/// <summary>Einklappbare Kästen der Grundlagen: der Kopf zeigt die Auswahl.</summary>
public sealed class BasicsSectionsTests : IDisposable
{
    private readonly TestShell _app = new();

    public void Dispose() => _app.Dispose();

    [Fact]
    public void NewHero_OpenWhereNothingChosen_HeadersShowSelection()
    {
        _app.List.NewCommand.Execute(null);
        var basics = _app.Editor.Basics;
        Assert.True(basics.IsSpeciesExpanded);
        Assert.True(basics.IsCultureExpanded);
        Assert.True(basics.IsProfessionExpanded);
        Assert.False(basics.IsPersonalExpanded);
        Assert.Equal("Keine Spezies gewählt", basics.SpeciesName);
        Assert.Equal("Noch nichts eingetragen", basics.PersonalSummary);

        basics.SpeciesOptions.Single(s => s.Name == "Mensch").IsSelected = true;
        basics.PersonalFields.Single(f => f.Label == "Alter").Value = "21";
        basics.PersonalFields.Single(f => f.Label == "Geschlecht").Value = "weiblich";

        Assert.Equal("Mensch", basics.SpeciesName);
        Assert.Equal("Geschlecht: weiblich · Alter: 21".Split(" · ").Order(), basics.PersonalSummary.Split(" · ").Order());
    }

    [Fact]
    public void ReopenedHero_ChosenSectionsStartCollapsed()
    {
        _app.List.NewCommand.Execute(null);
        _app.Editor.Basics.SpeciesOptions.Single(s => s.Name == "Mensch").IsSelected = true;
        _app.Editor.BackCommand.Execute(null);

        _app.List.Characters.Single().OpenCommand.Execute(null);

        Assert.False(_app.Editor.Basics.IsSpeciesExpanded);
        Assert.True(_app.Editor.Basics.IsCultureExpanded);
    }
}

/// <summary>AP je Bereich neben der Seitenüberschrift.</summary>
public sealed class PageApTests : IDisposable
{
    private readonly TestShell _app = new();

    public void Dispose() => _app.Dispose();

    [Fact]
    public void Pages_ShowApSpentInTheirArea()
    {
        _app.List.NewCommand.Execute(null);
        var editor = _app.Editor;
        editor.Basics.ChooseProfessionCommand.Execute(null);
        editor.Picker!.Groups.SelectMany(g => g.Items).First(i => i.Name == "Gildenloser Magier nach Agrimeton").AddCommand!.Execute(null);
        var ap = editor.State.Ap;

        Assert.Equal($"{ap.Skills} AP", editor.Skills.PageApText);
        Assert.Equal($"{ap.CombatTechniques} AP", editor.CombatTechniques.PageApText);
        Assert.Equal($"{ap.SpecialAbilities} AP", editor.SpecialAbilities.PageApText);
        Assert.Equal($"{ap.Spells + ap.Liturgies} AP", editor.Magic.PageApText);
        Assert.Equal($"{ap.Advantages + ap.Disadvantages} AP", editor.Traits.PageApText);
        Assert.Equal($"{ap.Attributes + ap.Energies} AP", editor.Attributes.PageApText);
        Assert.True(ap.Skills > 0 && ap.Spells > 0);

        var klettern = editor.Skills.Groups.SelectMany(g => g.Rows).Single(r => r.Id == "skill.klettern");
        klettern.IncreaseCommand.Execute(null);
        Assert.Equal($"{editor.State.Ap.Skills} AP", editor.Skills.PageApText);
        Assert.NotEqual(ap.Skills, editor.State.Ap.Skills);
    }
}
