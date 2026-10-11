using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSACharacterCreator.Core.Characters;

namespace DSACharacterCreator.ViewModels;

/// <summary>Ein Feld der persönlichen Angaben (Alter, Haarfarbe …); wird beim Verlassen übernommen (rückgängig machbar).</summary>
public sealed partial class PersonalFieldViewModel(EditorViewModel editor, string label, string? watermark,
    Func<PersonalData, string?> get, Action<PersonalData, string?> set, PersonalDiceViewModel? dice = null) : ObservableObject
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

    /// <summary>Gibt es für Spezies/Kultur eine Würfeltabelle zu diesem Feld?</summary>
    public bool CanRoll => dice?.IsAvailable(Label) ?? false;

    public string RollTip => Label == "Größe" ? "Größe und Gewicht auswürfeln" : $"{Label} auswürfeln";

    [RelayCommand]
    private void Roll() => dice?.RollFor(Label)?.Invoke();

    public void Refresh()
    {
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(CanRoll));
    }

    /// <summary>Felder in der Reihenfolge des Bogens; Notizen gesondert.</summary>
    public static IReadOnlyList<PersonalFieldViewModel> Create(EditorViewModel editor, PersonalDiceViewModel? dice = null) =>
    [
        new(editor, "Familie", null, p => p.Family, (p, v) => p.Family = v),
        new(editor, "Geburtsort", null, p => p.Birthplace, (p, v) => p.Birthplace = v),
        new(editor, "Geburtsdatum", "z. B. 3. Praios 1020 BF", p => p.Birthdate, (p, v) => p.Birthdate = v, dice),
        new(editor, "Alter", null, p => p.Age, (p, v) => p.Age = v, dice),
        new(editor, "Geschlecht", null, p => p.Gender, (p, v) => p.Gender = v),
        new(editor, "Größe", "z. B. 1,78 Schritt", p => p.Height, (p, v) => p.Height = v, dice),
        new(editor, "Gewicht", "z. B. 75 Stein", p => p.Weight, (p, v) => p.Weight = v, dice),
        new(editor, "Haarfarbe", null, p => p.HairColor, (p, v) => p.HairColor = v, dice),
        new(editor, "Augenfarbe", null, p => p.EyeColor, (p, v) => p.EyeColor = v, dice),
        new(editor, "Titel", null, p => p.Title, (p, v) => p.Title = v),
        new(editor, "Charakteristika", null, p => p.Characteristics, (p, v) => p.Characteristics = v),
    ];
}
