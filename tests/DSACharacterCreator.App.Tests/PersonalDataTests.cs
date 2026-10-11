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
}
