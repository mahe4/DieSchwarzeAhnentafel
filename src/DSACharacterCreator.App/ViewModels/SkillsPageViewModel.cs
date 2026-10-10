using CommunityToolkit.Mvvm.ComponentModel;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.ViewModels;

/// <summary>Talente, nach Gruppen.</summary>
public sealed partial class SkillsPageViewModel : ViewModelBase
{
    private static readonly (SkillGroup Group, string Title)[] GroupTitles =
    [
        (SkillGroup.Body, "Körpertalente"),
        (SkillGroup.Social, "Gesellschaftstalente"),
        (SkillGroup.Nature, "Naturtalente"),
        (SkillGroup.Knowledge, "Wissenstalente"),
        (SkillGroup.Craft, "Handwerkstalente"),
    ];

    public SkillsPageViewModel(EditorViewModel editor)
    {
        var rules = editor.Rules;
        Groups = GroupTitles
            .Select(g => new SkillGroupViewModel(g.Title,
                rules.Skills.Where(s => s.Group == g.Group).OrderBy(s => s.Name, StringComparer.CurrentCulture)
                    .Select(s => new SkillRowViewModel(editor, s)).ToList()))
            .ToList();
    }

    public IReadOnlyList<SkillGroupViewModel> Groups { get; }

    /// <summary>
    /// Flache Liste aus Gruppen und sichtbaren Talenten für die virtualisierte Anzeige (nur sichtbare Zeilen werden
    /// aufgebaut; am Handy dauerte der Aufbau aller 59 Zeilen sonst bis zu 2 s).
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<object> Rows { get; private set; } = [];

    /// <summary>Filtert Talente nach Namen.</summary>
    [ObservableProperty]
    public partial string Filter { get; set; } = "";

    partial void OnFilterChanged(string value) => ApplyVisibility();

    public void Update(CharacterState state)
    {
        foreach (var row in Groups.SelectMany(g => g.Rows)) row.Update(state);
        ApplyVisibility();
    }

    /// <summary>Einträge aus deaktivierten Quellen erscheinen nur, solange sie noch Werte tragen.</summary>
    private void ApplyVisibility()
    {
        var filter = Filter.Trim();
        foreach (var group in Groups)
        {
            foreach (var row in group.Rows)
                row.IsVisible = (row.Available || row.Value > 0) &&
                                (filter.Length == 0 || row.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase));
            group.IsVisible = group.Rows.Any(r => r.IsVisible);
        }
        // Nur bei geänderter Sichtbarkeit neu setzen, sonst springt die Liste nach jedem Klick nach oben
        var rows = Groups.Where(g => g.IsVisible).SelectMany(g => g.Rows.Where(r => r.IsVisible).Cast<object>().Prepend(g)).ToList();
        if (!rows.SequenceEqual(Rows)) Rows = rows;
    }
}

public sealed partial class SkillGroupViewModel(string title, IReadOnlyList<SkillRowViewModel> rows) : ObservableObject
{
    public string Title { get; } = title;

    public IReadOnlyList<SkillRowViewModel> Rows { get; } = rows;

    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;
}
