using CommunityToolkit.Mvvm.ComponentModel;
using DSACharacterCreator.Core.Characters;

namespace DSACharacterCreator.ViewModels;

/// <summary>Ein Feld der persönlichen Angaben (Alter, Haarfarbe …); wird beim Verlassen übernommen (rückgängig machbar).</summary>
public sealed class PersonalFieldViewModel(EditorViewModel editor, string label, string? watermark,
    Func<PersonalData, string?> get, Action<PersonalData, string?> set) : ObservableObject
{
    public string Label { get; } = label;

    public string? Watermark { get; } = watermark;

    public string Value
    {
        get => get(editor.Character.Personal) ?? "";
        set
        {
            var text = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (text == get(editor.Character.Personal)) return;
            editor.Mutate(() => set(editor.Character.Personal, text));
        }
    }

    public void Refresh() => OnPropertyChanged(nameof(Value));

    /// <summary>Felder in der Reihenfolge des Bogens; Notizen gesondert.</summary>
    public static IReadOnlyList<PersonalFieldViewModel> Create(EditorViewModel editor) =>
    [
        new(editor, "Familie", null, p => p.Family, (p, v) => p.Family = v),
        new(editor, "Geburtsort", null, p => p.Birthplace, (p, v) => p.Birthplace = v),
        new(editor, "Geburtsdatum", "z. B. 3. Praios 1020 BF", p => p.Birthdate, (p, v) => p.Birthdate = v),
        new(editor, "Alter", null, p => p.Age, (p, v) => p.Age = v),
        new(editor, "Geschlecht", null, p => p.Gender, (p, v) => p.Gender = v),
        new(editor, "Größe", "z. B. 1,78 Schritt", p => p.Height, (p, v) => p.Height = v),
        new(editor, "Gewicht", "z. B. 75 Stein", p => p.Weight, (p, v) => p.Weight = v),
        new(editor, "Haarfarbe", null, p => p.HairColor, (p, v) => p.HairColor = v),
        new(editor, "Augenfarbe", null, p => p.EyeColor, (p, v) => p.EyeColor = v),
        new(editor, "Titel", null, p => p.Title, (p, v) => p.Title = v),
        new(editor, "Charakteristika", null, p => p.Characteristics, (p, v) => p.Characteristics = v),
    ];
}
