using CommunityToolkit.Mvvm.Input;
using DSACharacterCreator.Core.Characters;

namespace DSACharacterCreator.ViewModels;

/// <summary>Ein Eintrag in der Charakterliste. Die Befehle liegen hier, weil Flyouts den DataContext der Liste nicht erreichen.</summary>
public partial class CharacterCardViewModel(Character character, CharacterListViewModel owner)
{
    public Character Character { get; } = character;

    public string Name => DisplayName(Character);

    public string Initial => Name[..1].ToUpperInvariant();

    public string PhaseText => Character.IsCorrectingCreation ? "Erschaffung wird korrigiert" : Character.Phase switch
    {
        CharacterPhase.Creation => "In Erschaffung",
        CharacterPhase.Advancement => "Erschaffung abgeschlossen" +
            (Character.Log.OfType<ApAwardEntry>().Sum(a => a.Amount) is var awarded and not 0 ? $" · {awarded:+0;−0} AP erhalten" : ""),
        _ => "",
    };

    public string ModifiedText => "Geändert " + Character.ModifiedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");

    public static string DisplayName(Character character) =>
        string.IsNullOrWhiteSpace(character.Name) ? "Unbenannter Held" : character.Name;

    [RelayCommand]
    private void Open() => owner.Open(this);

    [RelayCommand]
    private void Duplicate() => owner.Duplicate(this);

    [RelayCommand]
    private Task Export() => owner.Export(this);

    [RelayCommand]
    private Task ExportOptolith() => owner.ExportOptolith(this);

    [RelayCommand]
    private void Delete() => owner.RequestDelete(this);
}
