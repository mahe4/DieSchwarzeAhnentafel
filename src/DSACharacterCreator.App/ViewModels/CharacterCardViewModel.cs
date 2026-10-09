using CommunityToolkit.Mvvm.Input;
using DSACharacterCreator.Core.Characters;

namespace DSACharacterCreator.ViewModels;

/// <summary>Ein Eintrag in der Charakterliste. Die Befehle liegen hier, weil Flyouts den DataContext der Liste nicht erreichen.</summary>
public partial class CharacterCardViewModel(Character character, CharacterListViewModel owner)
{
    public Character Character { get; } = character;

    public string Name => DisplayName(Character);

    public string Initial => Name[..1].ToUpperInvariant();

    public string PhaseText => Character.Phase switch
    {
        CharacterPhase.Creation => "In Erschaffung",
        CharacterPhase.Advancement => "Erschaffung abgeschlossen",
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
    private void Delete() => owner.RequestDelete(this);
}
