using CommunityToolkit.Mvvm.ComponentModel;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.ViewModels;

/// <summary>Eigenschaften.</summary>
public sealed partial class AttributesPageViewModel(EditorViewModel editor) : ViewModelBase
{
    public IReadOnlyList<AttributeRowViewModel> Rows { get; } =
        editor.Rules.Attributes.Select(a => new AttributeRowViewModel(editor, a)).ToList();

    [ObservableProperty]
    public partial string TotalText { get; private set; } = "";

    public void Update(CharacterState state)
    {
        foreach (var row in Rows) row.Update(state);
        TotalText = $"Summe {state.Attributes.Sum(a => a.Value)} / {state.ExperienceLevel.MaxAttributeTotal}";
    }
}
