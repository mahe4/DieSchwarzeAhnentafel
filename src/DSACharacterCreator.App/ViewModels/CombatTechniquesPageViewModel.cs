using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.ViewModels;

/// <summary>Kampftechniken mit AT/PA/FK.</summary>
public sealed class CombatTechniquesPageViewModel(EditorViewModel editor) : ViewModelBase
{
    public IReadOnlyList<CombatTechniqueRowViewModel> Rows { get; } = editor.Rules.CombatTechniques
        .OrderBy(c => c.Name, StringComparer.CurrentCulture)
        .Select(c => new CombatTechniqueRowViewModel(editor, c)).ToList();

    public void Update(CharacterState state)
    {
        foreach (var row in Rows)
        {
            row.Update(state);
            // Einträge aus deaktivierten Quellen nur, solange sie vom Startwert abweichen (dann mit Hinweis).
            row.IsVisible = row.Available || row.IssueText is not null;
        }
    }
}
