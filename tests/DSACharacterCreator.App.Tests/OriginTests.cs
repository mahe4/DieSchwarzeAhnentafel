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

        // Die Spezies verschiebt nur Höchstwerte: GE bis 12, KK bis 15; die Werte bleiben 8.
        Assert.Equal(8, Editor.State.Attribute("GE"));
        Assert.Equal(12, Editor.State.Attributes.Single(a => a.Definition.Id == "GE").Max);
        Assert.Equal(15, Editor.State.Attributes.Single(a => a.Definition.Id == "KK").Max);
        Assert.Equal(8 + 2 * 8, Editor.State.Derived.LifePoints);
        Assert.Contains("LeP 24", Editor.Summary.DerivedText);
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

        // Vorlage 225 AP (das Wiki nennt 221); ausgegeben ohne das frei zu verteilende Sprachen-Budget (6 AP)
        Assert.Contains("225 AP", Basics.ProfessionSummary);
        Assert.Equal(225 - 6, Editor.State.Ap.Spent);
        Assert.Contains(Editor.Character.Creation.SpecialAbilities, e => e.Option == "skill.gassenwissen");
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
        // 212 ohne Fernkampf-Auswahl (−8) und Sprachen-Budget (−6), Gassenwissen +1 (+3)
        Assert.Equal(212 - 8 - 6 + 3, Editor.State.Ap.Spent);
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

    [Fact]
    public void ChangingCultureWithAdjustments_AsksFirst_ThenRecomputesPreselection()
    {
        Basics.ChooseCultureCommand.Execute(null);
        PickFromPicker("Horasier");
        Basics.ChooseProfessionCommand.Execute(null);
        PickFromPicker("Streuner");
        var rechnen = Editor.Values.SkillGroups.SelectMany(g => g.Rows).Single(r => r.Id == "skill.rechnen");
        rechnen.IncreaseCommand.Execute(null);
        Assert.Equal(7, rechnen.Value); // 4 Streuner + 2 Horasier + 1 eigene

        Basics.ChooseCultureCommand.Execute(null);
        PickFromPicker("Andergaster");
        Assert.NotNull(Editor.Confirmation);
        Editor.Confirmation!.ConfirmCommand.Execute(null);

        Assert.Equal(4, rechnen.Value); // Andergaster gibt kein Rechnen; eigene Anpassung verworfen
        Assert.False(Editor.Character.Creation.HasValueAdjustments);
    }

    [Fact]
    public void CancelledPackageToggle_KeepsCheckbox()
    {
        Basics.ChooseCultureCommand.Execute(null);
        PickFromPicker("Horasier");
        Editor.Values.SkillGroups.SelectMany(g => g.Rows).Single(r => r.Id == "skill.rechnen").IncreaseCommand.Execute(null);

        Basics.CulturePackage = false;
        Assert.NotNull(Editor.Confirmation);
        Editor.Confirmation!.CancelCommand.Execute(null);

        Assert.True(Basics.CulturePackage);
        Assert.True(Editor.Character.Creation.CulturePackage);
        Assert.True(Editor.Character.Creation.HasValueAdjustments);
    }
}
