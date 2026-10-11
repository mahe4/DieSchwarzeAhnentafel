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
