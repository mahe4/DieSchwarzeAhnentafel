using DSACharacterCreator.ViewModels;

namespace DSACharacterCreator.UiTests;

public sealed class OriginTests : IDisposable
{
    private readonly TestShell _app = new();

    public OriginTests()
    {
        _app.List.NewCommand.Execute(null);
    }

    public void Dispose() => _app.Dispose();

    private EditorViewModel Editor => _app.Editor;

    private BasicsPageViewModel Basics => Editor.Basics;

    private void PickFromPicker(string name)
    {
        var item = Editor.Picker!.Groups.SelectMany(g => g.Items).Single(i => i.Name == name);
        item.PickCommand!.Execute(null);
    }

    [Fact]
    public void ChoosingSpecies_AppliesModifiersAndBaseValues()
    {
        Basics.SpeciesOptions.Single(s => s.Name == "Zwerg").IsSelected = true;

        Assert.Equal(61, Editor.State.Ap.Species);
        Assert.Single(Basics.SpeciesChoices);
        Assert.True(Basics.SpeciesChoices[0].IsOpen);

        Basics.SpeciesChoices[0].Selected = Basics.SpeciesChoices[0].Options.Single(o => o.Id == "GE");

        Assert.Equal(6, Editor.State.Attribute("GE"));
        Assert.Equal(9, Editor.State.Attribute("KK"));
        Assert.Equal(8 + 2 * 9, Editor.State.Derived.LifePoints);
        Assert.Contains("LeP 26", Editor.Summary.DerivedText);
    }

    [Fact]
    public void CulturePicker_SearchesAndSelects()
    {
        Basics.ChooseCultureCommand.Execute(null);
        Assert.NotNull(Editor.Picker);

        Editor.Picker!.Search = "ander";
        Assert.Equal("1 von 43", Editor.Picker.CountText);
        PickFromPicker("Andergaster");

        Assert.Null(Editor.Picker);
        Assert.Equal("Andergaster", Basics.CultureName);
        Assert.Equal(20, Editor.State.Ap.Spent);
        Assert.Equal(2, Editor.Values.SkillGroups.SelectMany(g => g.Rows).Single(r => r.Id == "skill.holzbearbeitung").Value);

        Basics.CulturePackage = false;
        Assert.Equal(0, Editor.State.Ap.Spent);
    }

    [Fact]
    public void Profession_WithVariantAndCombatChoice()
    {
        Basics.ChooseProfessionCommand.Execute(null);
        Editor.Picker!.Search = "Streuner";
        PickFromPicker("Streuner");

        Assert.Equal("Streuner", Basics.ProfessionName);
        Assert.Single(Basics.CombatChoices);
        Basics.CombatChoices[0].Selected = Basics.CombatChoices[0].Options.Single(o => o.Id == "ct.wurfwaffen");
        Basics.VariantChoice!.Selected = Basics.VariantChoice.Options.Single(o => o.Id == "einbrecher-in");

        Assert.Equal(225, Editor.State.Ap.Spent); // berechnet; das Wiki nennt 221
        Assert.Contains("225 AP", Basics.ProfessionSummary);
        Assert.Equal(10, Editor.Values.CombatTechniques.Single(r => r.Id == "ct.wurfwaffen").Value);
        Assert.Equal(7, Editor.Values.SkillGroups.SelectMany(g => g.Rows).Single(r => r.Id == "skill.klettern").Value);
        Assert.Contains("Vorlage 7", Editor.Values.SkillGroups.SelectMany(g => g.Rows).Single(r => r.Id == "skill.klettern").Detail);
    }

    [Fact]
    public void RaisingAboveProfessionValue_IsStoredRelative()
    {
        Basics.ChooseProfessionCommand.Execute(null);
        PickFromPicker("Streuner");
        var gassenwissen = Editor.Values.SkillGroups.SelectMany(g => g.Rows).Single(r => r.Id == "skill.gassenwissen");

        gassenwissen.IncreaseCommand.Execute(null);

        Assert.Equal(8, gassenwissen.Value);
        Assert.Equal(1, Editor.Character.Creation.SkillAdjustments["skill.gassenwissen"]);
        Assert.Equal(212 - 8 + 3, Editor.State.Ap.Spent); // ohne gewählte Fernkampftechnik (−8), Gassenwissen +1 (+3)
    }

    [Fact]
    public void BackButton_ClosesPickerFirst()
    {
        Basics.ChooseCultureCommand.Execute(null);

        Assert.True(_app.Shell.GoBack());
        Assert.Null(Editor.Picker);
        Assert.IsType<EditorViewModel>(_app.Shell.CurrentPage);
    }

    [Fact]
    public void ChangingProfession_ResetsChoices()
    {
        Basics.ChooseProfessionCommand.Execute(null);
        PickFromPicker("Streuner");
        Basics.CombatChoices[0].Selected = Basics.CombatChoices[0].Options[0];

        Basics.ChooseProfessionCommand.Execute(null);
        PickFromPicker("Kriegerin");

        Assert.Empty(Editor.Character.Creation.ProfessionCombatTechniqueChoices);
        Assert.True(Basics.CombatChoices[0].IsOpen);
    }

    [Fact]
    public void ChangingProfessionWithAdjustments_AsksFirst()
    {
        Basics.ChooseProfessionCommand.Execute(null);
        PickFromPicker("Streuner");
        var gassenwissen = Editor.Values.SkillGroups.SelectMany(g => g.Rows).Single(r => r.Id == "skill.gassenwissen");
        gassenwissen.IncreaseCommand.Execute(null);

        Basics.ChooseProfessionCommand.Execute(null);
        PickFromPicker("Kriegerin");

        // Noch nichts geändert, Rückfrage offen
        Assert.NotNull(Editor.Confirmation);
        Assert.Equal("Streuner", Basics.ProfessionName);

        Editor.Confirmation!.CancelCommand.Execute(null);
        Assert.Null(Editor.Confirmation);
        Assert.Equal("Streuner", Basics.ProfessionName);
        Assert.Equal(8, gassenwissen.Value);

        Basics.ChooseProfessionCommand.Execute(null);
        PickFromPicker("Kriegerin");
        Editor.Confirmation!.ConfirmCommand.Execute(null);

        Assert.Equal("Kriegerin", Basics.ProfessionName);
        Assert.Empty(Editor.Character.Creation.SkillAdjustments);
    }

    [Fact]
    public void RemovingProfessionWithAdjustments_AsksFirst_BackButtonCancels()
    {
        Basics.ChooseProfessionCommand.Execute(null);
        PickFromPicker("Streuner");
        Editor.Values.SkillGroups.SelectMany(g => g.Rows).Single(r => r.Id == "skill.verbergen").DecreaseCommand.Execute(null);
        Assert.True(Editor.Character.Creation.HasValueAdjustments);

        Basics.ClearProfessionCommand.Execute(null);
        Assert.NotNull(Editor.Confirmation);

        Assert.True(_app.Shell.GoBack());
        Assert.Null(Editor.Confirmation);
        Assert.Equal("Streuner", Basics.ProfessionName);
    }
}
