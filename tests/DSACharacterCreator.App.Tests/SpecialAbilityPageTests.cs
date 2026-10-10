using DSACharacterCreator.ViewModels;

namespace DSACharacterCreator.UiTests;

public sealed class SpecialAbilityPageTests : IDisposable
{
    private readonly TestShell _app = new();

    public SpecialAbilityPageTests()
    {
        _app.List.NewCommand.Execute(null);
    }

    public void Dispose() => _app.Dispose();

    private EditorViewModel Editor => _app.Editor;

    private TraitsPageViewModel Page => Editor.Traits;

    private void Pick(string name) =>
        Editor.Picker!.Groups.SelectMany(g => g.Items).Single(i => i.Name == name).PickCommand!.Execute(null);

    [Fact]
    public void AddLanguage_ChooseLanguageAndLevel()
    {
        Page.AddSpecialAbilityCommand.Execute(null);
        Assert.Contains(Editor.Picker!.Groups, g => g.Title == "Kampfsonderfertigkeiten");
        Pick("Sprache I–III");

        var row = Assert.Single(Page.SpecialAbilities);
        Assert.Equal("AP offen", row.ApText);
        row.SelectedOption = row.Options.Single(o => o.Label == "Garethi");
        Page.SpecialAbilities[0].IncreaseLevelCommand.Execute(null);

        Assert.Equal("Sprache II", Page.SpecialAbilities[0].Title);
        Assert.Equal("4 AP", Page.SpecialAbilities[0].ApText);
        Assert.Equal(4, Editor.State.Ap.SpecialAbilities);
    }

    [Fact]
    public void CultureAndProfession_FillTemplateEntries()
    {
        Editor.Basics.ChooseCultureCommand.Execute(null);
        Pick("Mittelreicher");
        Editor.Basics.ChooseProfessionCommand.Execute(null);
        Pick("Streuner");

        Assert.Contains(Page.SpecialAbilities, r => r.Title == "Sprache III" && r.ApText == "kostenlos · aus Kultur");
        Assert.Contains(Page.SpecialAbilities, r => r.Title == "Ortskenntnis" && r.ApText == "kostenlos · aus Kultur");
        Assert.Contains(Page.SpecialAbilities, r => r.Title.StartsWith("Fertigkeitsspezialisierung") && r.ApText == "3 AP · aus Profession");
        Assert.Contains("6 AP", Page.LanguageHint);
    }

    [Fact]
    public void TemplateEntry_CanBeRemoved()
    {
        Editor.Basics.ChooseProfessionCommand.Execute(null);
        Pick("Streuner");
        var row = Page.SpecialAbilities.Single(r => r.Title.StartsWith("Fertigkeitsspezialisierung"));

        row.RemoveCommand.Execute(null);

        Assert.Empty(Page.SpecialAbilities);
        Assert.Equal(0, Editor.State.Ap.SpecialAbilities);
    }
}
