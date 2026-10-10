using CommunityToolkit.Mvvm.ComponentModel;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.ViewModels;

/// <summary>Eigenschaften.</summary>
public sealed partial class AttributesPageViewModel(EditorViewModel editor) : ViewModelBase
{
    public IReadOnlyList<AttributeRowViewModel> Rows { get; } =
        editor.Rules.Attributes.Select(a => new AttributeRowViewModel(editor, a)).ToList();

    /// <summary>Zukauf von LeP/AsP/KaP, erst nach der Erschaffung (Heldenerschaffung, Schritt 8; Erfahrung).</summary>
    public IReadOnlyList<EnergyRowViewModel> Energies { get; } =
    [
        new(editor, Core.Characters.Energies.LifePoints, "Lebenspunkte"),
        new(editor, Core.Characters.Energies.Astral, "Astralpunkte"),
        new(editor, Core.Characters.Energies.Karma, "Karmapunkte"),
    ];

    [ObservableProperty]
    public partial bool ShowEnergies { get; private set; }

    [ObservableProperty]
    public partial string TotalText { get; private set; } = "";

    public void Update(CharacterState state)
    {
        foreach (var row in Rows) row.Update(state);
        foreach (var row in Energies)
        {
            row.Update(state);
            row.IsVisible = row.Available || row.Value > 0;
        }
        ShowEnergies = !editor.IsCreation || Energies.Any(e => e.Value > 0);
        TotalText = $"Summe {state.Attributes.Sum(a => a.Value)} / {state.ExperienceLevel.MaxAttributeTotal}";
    }
}
