using DSACharacterCreator.ViewModels;

namespace DSACharacterCreator.UiTests;

/// <summary>Persönliche Angaben auf der Seite Grundlagen und auf dem Charakterbogen.</summary>
public sealed class PersonalDataTests : IDisposable
{
    private readonly TestShell _app = new();

    public PersonalDataTests()
    {
        _app.List.NewCommand.Execute(null);
    }

    public void Dispose() => _app.Dispose();

    private EditorViewModel Editor => _app.Editor;

    [Fact]
    public void PersonalFields_ShowOnSheet_AndUndo()
    {
        var age = Editor.Basics.PersonalFields.Single(f => f.Label == "Alter");
        age.Value = "19";
        Editor.Basics.Notes.Value = "Hat Angst vor Spinnen.";
        Assert.Equal("19", Editor.Character.Personal.Age);

        Editor.SelectedSection = Editor.Sections.Single(s => s.Page == Editor.Sheet);
        var sheet = Editor.Sheet.Sheet!;
        Assert.Contains(sheet.Personal, f => f.Label == "Alter" && f.Value == "19");
        Assert.Equal("Hat Angst vor Spinnen.", sheet.Notes);

        Editor.UndoCommand.Execute(null);
        Editor.UndoCommand.Execute(null);
        Assert.Equal("", age.Value);
        Assert.DoesNotContain(Editor.Sheet.Sheet!.Personal, f => f.Label == "Alter");

        // Leere oder unveränderte Eingaben erzeugen keinen Rückgängig-Schritt
        age.Value = "  ";
        Assert.Null(Editor.Character.Personal.Age);
    }

    private PersonalFieldViewModel Field(string label) => Editor.Basics.PersonalFields.Single(f => f.Label == label);

    [Fact]
    public void Dice_RollsBySpeciesAndCulture_AndUndo()
    {
        var basics = Editor.Basics;
        var dice = basics.Dice;
        // Ohne Spezies und Kultur: nur das Geburtsdatum
        Assert.False(Field("Alter").CanRoll);
        Assert.False(Field("Größe").CanRoll);
        Assert.True(Field("Geburtsdatum").CanRoll);
        Assert.False(dice.CanRollName);

        basics.SpeciesOptions.Single(s => s.Name == "Mensch").IsSelected = true;
        basics.ChooseCultureCommand.Execute(null);
        Editor.Picker!.Groups.SelectMany(g => g.Items).Single(i => i.Name == "Thorwaler").PickCommand!.Execute(null);

        Assert.True(dice.HasTables);
        Assert.Equal("Thorwaler", dice.SelectedTable);
        Assert.True(Field("Alter").CanRoll);

        Field("Größe").RollCommand.Execute(null);
        var height = Core.Rules.PersonalRolls.ParseHeight(Editor.Character.Personal.Height)!.Value;
        Assert.InRange(height, 170, 208);
        Assert.NotNull(Editor.Character.Personal.Weight);
        Assert.Equal(Editor.Character.Personal.Height, Field("Größe").Value);

        Field("Geschlecht").Value = "weiblich";
        dice.RollNameCommand.Execute(null);
        Assert.EndsWith("sdottir", Editor.Character.Name);
        Assert.Equal(Editor.Character.Name, Editor.Name);

        Field("Haarfarbe").Value = "rot";
        dice.RollEmptyCommand.Execute(null);
        var p = Editor.Character.Personal;
        Assert.Equal("rot", p.HairColor);
        Assert.InRange(int.Parse(p.Age!), 17, 19); // Erfahren: 16+1W3
        Assert.NotNull(p.EyeColor);
        Assert.NotNull(p.Birthdate);
        Assert.False(dice.CanRollEmpty);

        // Ein Schritt zurück: nur „Leere Felder“ rückgängig
        Editor.UndoCommand.Execute(null);
        Assert.Null(Editor.Character.Personal.Age);
        Assert.Equal("rot", Editor.Character.Personal.HairColor);

        // Andere Tabelle wählen
        dice.SelectedTable = "Gjalsker";
        Assert.Equal("Gjalsker", Editor.Character.Personal.AppearanceTable);
        Assert.Equal("Gjalsker", dice.SelectedTable);
    }
}
