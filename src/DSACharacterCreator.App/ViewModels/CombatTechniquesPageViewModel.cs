using CommunityToolkit.Mvvm.ComponentModel;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.ViewModels;

/// <summary>Kampftechniken mit AT/PA/FK.</summary>
public sealed partial class CombatTechniquesPageViewModel(EditorViewModel editor) : PageViewModel
{
    public IReadOnlyList<CombatTechniqueRowViewModel> Rows { get; } = editor.Rules.CombatTechniques
        .OrderBy(c => c.Name, StringComparer.CurrentCulture)
        .Select(c => new CombatTechniqueRowViewModel(editor, c)).ToList();

    /// <summary>Sichtbare Zeilen für die virtualisierte Liste (nur bei Änderung neu gesetzt, sonst springt sie nach oben).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<CombatTechniqueRowViewModel> VisibleRows { get; private set; } = [];

    public void Update(CharacterState state)
    {
        foreach (var row in Rows)
        {
            row.Update(state);
            // Einträge aus deaktivierten Quellen nur, solange sie vom Startwert abweichen (dann mit Hinweis).
            row.IsVisible = row.Available || row.IssueText is not null;
        }
        var visible = Rows.Where(r => r.IsVisible).ToList();
        if (!visible.SequenceEqual(VisibleRows)) VisibleRows = visible;
    }
}
