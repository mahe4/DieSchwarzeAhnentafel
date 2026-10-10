using DSACharacterCreator.Core.Persistence;
using DSACharacterCreator.ViewModels;

namespace DSACharacterCreator.UiTests;

public sealed class EditorTests : IDisposable
{
    private readonly TestShell _app = new();

    public EditorTests()
    {
        _app.List.NewCommand.Execute(null);
    }

    public void Dispose() => _app.Dispose();

    private EditorViewModel Editor => _app.Editor;

    private ValueRowViewModel Attribute(string id) => Editor.Values.Attributes.Single(a => a.Id == id);

    private SkillRowViewModel Skill(string id) => Editor.Values.SkillGroups.SelectMany(g => g.Rows).Single(r => r.Id == id);

    private CombatTechniqueRowViewModel Combat(string id) => Editor.Values.CombatTechniques.Single(r => r.Id == id);

    [Fact]
    public void RaisingAttribute_SpendsApAndIsSaved()
    {
        Attribute("MU").IncreaseCommand.Execute(null);
        Attribute("MU").IncreaseCommand.Execute(null);

        Assert.Equal(10, Attribute("MU").Value);
        Assert.Equal(30, Editor.State.Ap.Spent);
        Assert.Equal("30 / 1100 AP", Editor.Summary.ApText);

        var saved = new CharacterRepository(_app.Directory).LoadAll().Characters.Single();
        Assert.Equal(10, saved.Creation.Attributes["MU"]);
    }

    [Fact]
    public void LoweringToStart_RemovesStoredValue()
    {
        Attribute("MU").IncreaseCommand.Execute(null);
        Attribute("MU").DecreaseCommand.Execute(null);

        Assert.False(Editor.Character.Creation.Attributes.ContainsKey("MU"));
        Assert.False(Attribute("MU").DecreaseCommand.CanExecute(null));
    }

    [Fact]
    public void SkillIncrease_StopsAtMaximum()
    {
        var klettern = Skill("skill.klettern");
        for (var i = 0; i < 20; i++) klettern.IncreaseCommand.Execute(null);

        Assert.Equal(10, klettern.Value); // MU/GE/KK 8 + 2, Erfahren 10
        Assert.False(klettern.IncreaseCommand.CanExecute(null));
    }

    [Fact]
    public void CombatTechnique_ShowsAttackAndParry()
    {
        Attribute("MU").IncreaseCommand.Execute(null);
        Attribute("MU").IncreaseCommand.Execute(null);
        Attribute("MU").IncreaseCommand.Execute(null);

        Assert.Equal("AT 7 · PA 3", Combat("ct.raufen").CombatText);
        Assert.Equal("FK 6", Combat("ct.boegen").CombatText);
    }

    [Fact]
    public void ChangingExperienceLevel_ChangesBudgetAndLimits()
    {
        var unerfahren = Editor.Basics.ExperienceLevels.Single(l => l.Level.Id == "unerfahren");

        unerfahren.IsSelected = true;

        Assert.Equal(900, Editor.State.Ap.Total);
        Assert.True(unerfahren.IsSelected);
        Assert.False(Editor.Basics.ExperienceLevels.Single(l => l.Level.Id == "erfahren").IsSelected);
        Assert.Equal("unerfahren", Editor.Character.Creation.ExperienceLevel);
        Assert.Equal(8, Combat("ct.dolche").Max);
    }

    [Fact]
    public void LoweringLevel_FlagsValuesAboveNewLimit()
    {
        for (var i = 0; i < 5; i++) Attribute("KL").IncreaseCommand.Execute(null); // 13

        Editor.Basics.ExperienceLevels.Single(l => l.Level.Id == "unerfahren").IsSelected = true;

        Assert.NotNull(Attribute("KL").IssueText);
        Assert.Contains(Editor.Summary.Issues, i => i.IsError);
    }

    [Fact]
    public void Filter_HidesNonMatchingRows()
    {
        Editor.Values.Filter = "kletter";

        Assert.True(Skill("skill.klettern").IsVisible);
        Assert.False(Skill("skill.schwimmen").IsVisible);
        Assert.Single(Editor.Values.SkillGroups, g => g.IsVisible);
        Assert.False(Editor.Values.HasCombatTechniques);
    }

    [Fact]
    public void DisabledSources_HideTheirEntries()
    {
        new DSACharacterCreator.Core.Content.SourceSelection(["grw"]).ApplyTo(Editor.Character, TestShell.Rules.Books);
        new CharacterRepository(_app.Directory).Save(Editor.Character);
        _app.Shell.GoBack();
        _app.List.Characters[0].OpenCommand.Execute(null);

        Assert.True(Combat("ct.dolche").IsVisible);
        Assert.False(Combat("ct.peitschen").IsVisible);
    }

    [Fact]
    public void SkillDetails_ShowWikiText()
    {
        var klettern = Skill("skill.klettern");

        klettern.ToggleDetailsCommand.Execute(null);

        Assert.True(klettern.ShowDetails);
        Assert.Contains("Anwendungsgebiete: Baumklettern", klettern.Details);
    }
}
