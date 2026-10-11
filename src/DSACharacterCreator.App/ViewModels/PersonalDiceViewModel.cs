using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.ViewModels;

/// <summary>
/// Würfelhelfer der persönlichen Angaben: Startalter, Geburtsdatum, Größe/Gewicht, Haar- und Augenfarbe nach Spezies
/// (Tabelle je Untergruppe wählbar) und Name nach Kultur. Die Ergebnisse landen in den freien Textfeldern.
/// </summary>
public sealed partial class PersonalDiceViewModel(EditorViewModel editor) : ObservableObject
{
    private PersonalRolls Rolls => new(editor.Rules, Random.Shared);

    private CharacterState State => editor.State;

    private SpeciesAppearance? Species => editor.Rules.FindAppearance(State.Origin.Species?.Id);

    private string TableName =>
        Species is { } s
            ? s.Choices.FirstOrDefault(c => c.Name == editor.Character.Personal.AppearanceTable)?.Name
              ?? s.DefaultChoice(State.Origin.Culture?.Id)
            : "";

    private AppearanceVariant? Table => Species?.Resolve(TableName);

    /// <summary>Untergruppen der Spezies (Mittelländer, Thorwaler …); leer, wenn es nur eine Tabelle gibt.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> Tables { get; private set; } = [];

    public string? SelectedTable
    {
        get => Tables.Count > 0 ? TableName : null;
        set
        {
            if (value is null || value == TableName) return;
            editor.Mutate(() => editor.Character.Personal.AppearanceTable = value);
        }
    }

    [ObservableProperty]
    public partial bool HasTables { get; private set; }

    [ObservableProperty]
    public partial bool CanRollName { get; private set; }

    [ObservableProperty]
    public partial bool CanRollEmpty { get; private set; }

    public void Update()
    {
        var tables = Species?.Variants is { Count: > 1 } v ? v.Select(x => x.Name).ToList() : [];
        if (!tables.SequenceEqual(Tables)) Tables = tables;
        HasTables = tables.Count > 0;
        OnPropertyChanged(nameof(SelectedTable));
        CanRollName = Rolls.Name(State.Origin.Culture, NameGender.Female) is not null
                      || Rolls.Name(State.Origin.Culture, NameGender.Male) is not null;
        CanRollEmpty = Fields.Any(f => f.Available() && f.Get(editor.Character.Personal) is null);
    }

    /// <summary>Würfelbare Felder: Bezeichnung wie in <see cref="PersonalFieldViewModel.Create"/>.</summary>
    internal IReadOnlyList<(string Label, Func<bool> Available, Func<PersonalData, string?> Get, Action<PersonalData> Roll)> Fields =>
    [
        ("Geburtsdatum", () => editor.Rules.Birthdates.Months.Count > 0, p => p.Birthdate,
            p => p.Birthdate = Rolls.Birthdate()),
        ("Alter", () => editor.Rules.StartAge(State.Origin.Species?.Id, State.ExperienceLevel.Id) is not null, p => p.Age,
            p => p.Age = Rolls.Age(State.Origin.Species?.Id, State.ExperienceLevel.Id)?.ToString()),
        // Größe würfelt das Gewicht mit, weil es von der Größe abhängt
        ("Größe", () => Table?.Height is not null, p => p.Height, RollHeightAndWeight),
        ("Gewicht", () => Table?.Weight is not null, p => p.Weight, RollWeight),
        ("Haarfarbe", () => Table?.Hair is not null, p => p.HairColor,
            p => p.HairColor = Rolls.HairColor(Species!, Table!)),
        ("Augenfarbe", () => Table?.Eyes is not null, p => p.EyeColor, p => p.EyeColor = Rolls.EyeColor(Table!)),
    ];

    private void RollHeightAndWeight(PersonalData p)
    {
        var rolls = Rolls;
        if (rolls.Height(Table!) is not { } height) return;
        p.Height = PersonalRolls.FormatHeight(height);
        if (rolls.Weight(Table!, height) is { } weight) p.Weight = PersonalRolls.FormatWeight(weight);
    }

    private void RollWeight(PersonalData p)
    {
        var rolls = Rolls;
        var height = PersonalRolls.ParseHeight(p.Height);
        if (height is null && rolls.Height(Table!) is { } rolled)
        {
            height = rolled;
            p.Height = PersonalRolls.FormatHeight(rolled);
        }
        if (height is { } h && rolls.Weight(Table!, h) is { } weight) p.Weight = PersonalRolls.FormatWeight(weight);
    }

    /// <summary>Würfelt ein Feld aus (ein Rückgängig-Schritt); <c>null</c>, wenn es dafür keine Tabelle gibt.</summary>
    internal Action? RollFor(string label) =>
        Fields.FirstOrDefault(f => f.Label == label) is { Roll: not null } field
            ? () => editor.Mutate(() => field.Roll(editor.Character.Personal))
            : null;

    internal bool IsAvailable(string label) => Fields.FirstOrDefault(f => f.Label == label) is { Available: not null } f && f.Available();

    [RelayCommand]
    private void RollName()
    {
        var gender = PersonalRolls.GenderOf(editor.Character.Personal.Gender);
        if (Rolls.Name(State.Origin.Culture, gender) is { } name) editor.SetName(name);
    }

    /// <summary>Alle leeren würfelbaren Felder auf einmal (ein Rückgängig-Schritt).</summary>
    [RelayCommand]
    private void RollEmpty() =>
        editor.Mutate(() =>
        {
            var p = editor.Character.Personal;
            var weight = p.Weight;
            foreach (var f in Fields.Where(f => f.Available() && f.Get(p) is null)) f.Roll(p);
            // Größe würfelt das Gewicht mit – ein schon eingetragenes Gewicht bleibt
            if (weight is not null) p.Weight = weight;
        });
}
