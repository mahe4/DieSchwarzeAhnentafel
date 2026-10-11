using DSACharacterCreator.ViewModels;

namespace DSACharacterCreator.UiTests;

public sealed class TraitPageTests : IDisposable
{
    private readonly TestShell _app = new();

    public TraitPageTests()
    {
        _app.List.NewCommand.Execute(null);
    }

    public void Dispose() => _app.Dispose();

    private EditorViewModel Editor => _app.Editor;

    private TraitsPageViewModel Page => Editor.Traits;

    private void Pick(string name) =>
        Editor.Picker!.Groups.SelectMany(g => g.Items).Single(i => i.Name == name).AddCommand!.Execute(null);

    [Fact]
    public void Picker_ExpandsDescription_AddsSeveral_StaysOpen()
    {
        Page.AddAdvantageCommand.Execute(null);
        var picker = Editor.Picker!;
        Assert.True(picker.IsMultiple);
        var items = picker.Groups.SelectMany(g => g.Items).ToList();

        // Klick klappt nur auf und zeigt den Regeltext
        var luck = items.Single(i => i.Name == "Glück I–III");
        luck.PickCommand!.Execute(null);
        Assert.True(luck.IsExpanded);
        Assert.Contains("Schicksalspunkt", luck.Description);
        Assert.Empty(Page.Advantages);

        // Hinzufügen: Liste bleibt offen, mehrere Einträge nacheinander
        luck.AddCommand!.Execute(null);
        items.Single(i => i.Name == "Hohe Lebenskraft I–VII").AddCommand!.Execute(null);
        Assert.Same(picker, Editor.Picker);
        Assert.Equal(2, Page.Advantages.Count);
        Assert.Equal("✓", luck.AddedText);
        Assert.Equal("2 hinzugefügt", picker.AddedText);
        // ohne Auswahl nur einmal; mit Fertigkeitsauswahl (Begabung) mehrfach
        Assert.False(luck.AddCommand.CanExecute(null));
        var talent = items.Single(i => i.Name == "Begabung");
        talent.AddCommand!.Execute(null);
        talent.AddCommand.Execute(null);
        Assert.Equal("✓ 2×", talent.AddedText);

        // Schließen bzw. Zurück-Taste
        Assert.True(Editor.ClosePicker());
        Assert.Null(Editor.Picker);
    }

    [Fact]
    public void AddAdvantage_WithLevels()
    {
        Page.AddAdvantageCommand.Execute(null);
        Pick("Hohe Lebenskraft I–VII");

        var row = Assert.Single(Page.Advantages);
        Assert.Equal("Hohe Lebenskraft I", row.Title);
        row.IncreaseLevelCommand.Execute(null);
        row.IncreaseLevelCommand.Execute(null);

        Assert.Equal("Hohe Lebenskraft III", Page.Advantages[0].Title);
        Assert.Equal("18 AP", Page.Advantages[0].ApText);
        Assert.Equal(18, Editor.State.Ap.Advantages);
    }

    [Fact]
    public void Begabung_NeedsSkillChoice()
    {
        Page.AddAdvantageCommand.Execute(null);
        Pick("Begabung");
        var row = Page.Advantages[0];
        Assert.Equal("AP offen", row.ApText);
        Assert.NotNull(row.IssueText);

        row.SelectedOption = row.Options.Single(o => o.Id == "skill.klettern");

        Assert.Equal("12 AP", Page.Advantages[0].ApText);
        Assert.DoesNotContain("wählen", Page.Advantages[0].IssueText ?? "");
    }

    [Fact]
    public void Disadvantage_GivesApAndRowsStayStableWhileEditing()
    {
        Page.AddDisadvantageCommand.Execute(null);
        Pick("Schlechte Eigenschaft");
        var row = Page.Disadvantages[0];

        row.Specification = "Neugier";
        row.ManualApText = "5"; // wird als Nachteil negativ

        Assert.Same(row, Page.Disadvantages[0]);
        Assert.Equal(-5, Editor.State.Ap.Disadvantages);
        Assert.Equal("Neugier", Editor.Character.Creation.Traits[0].Specification);
    }

    [Fact]
    public void Remove_DeletesEntry()
    {
        Page.AddAdvantageCommand.Execute(null);
        Pick("Flink");
        Page.Advantages[0].RemoveCommand.Execute(null);

        Assert.Empty(Page.Advantages);
        Assert.Empty(Editor.Character.Creation.Traits);
    }

    [Fact]
    public void SpeciesAutomaticAdvantages_AreShownReadOnly()
    {
        Editor.Basics.SpeciesOptions.Single(s => s.Name == "Elfen").IsSelected = true;

        var zauberer = Page.Advantages.Single(r => r.DefinitionId == "adv.zauberer");
        Assert.True(zauberer.IsAutomatic);
        Assert.False(zauberer.IsEditable);
        Assert.Contains("Vorteile 30 / 80", Page.LimitsText);
        Assert.Contains("AsP 20", Editor.Summary.DerivedText);
    }
}
