using CommunityToolkit.Mvvm.ComponentModel;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.ViewModels;

/// <summary>Eigenschaften.</summary>
public sealed partial class AttributesPageViewModel(EditorViewModel editor) : ViewModelBase
{
    public IReadOnlyList<AttributeRowViewModel> Rows { get; } =
        editor.Rules.Attributes.Select(a => new AttributeRowViewModel(editor, a)).ToList();

    /// <summary>
    /// LeP/AsP/KaP: permanente Verluste, Rückkauf (auch bei der Erschaffung) und Zukauf (erst danach; Erfahrung).
    /// </summary>
    public IReadOnlyList<EnergyRowViewModel> Energies { get; } =
    [
        new(editor, Core.Characters.Energies.AstralLost, "Permanent verlorene AsP", EnergyKind.Lost),
        new(editor, Core.Characters.Energies.AstralRepurchased, "AsP zurückkaufen", EnergyKind.Repurchase),
        new(editor, Core.Characters.Energies.KarmaLost, "Permanent verlorene KaP", EnergyKind.Lost),
        new(editor, Core.Characters.Energies.KarmaRepurchased, "KaP zurückkaufen", EnergyKind.Repurchase),
        new(editor, Core.Characters.Energies.LifePoints, "Lebenspunkte zukaufen", EnergyKind.Bought),
        new(editor, Core.Characters.Energies.Astral, "Astralpunkte zukaufen", EnergyKind.Bought),
        new(editor, Core.Characters.Energies.Karma, "Karmapunkte zukaufen", EnergyKind.Bought),
    ];

    [ObservableProperty]
    public partial bool ShowEnergies { get; private set; }

    [ObservableProperty]
    public partial string TotalText { get; private set; } = "";

    public void Update(CharacterState state)
    {
        foreach (var row in Rows) row.Update(state);
        // Bei der Erschaffung nur Verluste und Rückkauf (z. B. Bindung des Stabes), Zukauf erst danach
        foreach (var row in Energies)
        {
            row.Update(state);
            row.IsVisible = row.Value > 0 || row.Available && (row.Kind switch
            {
                EnergyKind.Bought => !editor.IsCreation,
                EnergyKind.Lost => !editor.IsCreation,
                _ => true,
            });
        }
        ShowEnergies = Energies.Any(e => e.IsVisible);
        TotalText = $"Summe {state.Attributes.Sum(a => a.Value)} / {state.ExperienceLevel.MaxAttributeTotal}";
    }
}
