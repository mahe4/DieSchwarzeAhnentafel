using CommunityToolkit.Mvvm.ComponentModel;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.ViewModels;

/// <summary>Eigenschaften, Talente und Kampftechniken.</summary>
public sealed partial class ValuesPageViewModel : ViewModelBase
{
    private static readonly (SkillGroup Group, string Title)[] GroupTitles =
    [
        (SkillGroup.Body, "Körpertalente"),
        (SkillGroup.Social, "Gesellschaftstalente"),
        (SkillGroup.Nature, "Naturtalente"),
        (SkillGroup.Knowledge, "Wissenstalente"),
        (SkillGroup.Craft, "Handwerkstalente"),
    ];

    public ValuesPageViewModel(EditorViewModel editor)
    {
        var rules = editor.Rules;
        Attributes = rules.Attributes.Select(a => new AttributeRowViewModel(editor, a)).ToList();
        SkillGroups = GroupTitles
            .Select(g => new SkillGroupViewModel(g.Title,
                rules.Skills.Where(s => s.Group == g.Group).OrderBy(s => s.Name, StringComparer.CurrentCulture)
                    .Select(s => new SkillRowViewModel(editor, s)).ToList()))
            .ToList();
        CombatTechniques = rules.CombatTechniques.OrderBy(c => c.Name, StringComparer.CurrentCulture)
            .Select(c => new CombatTechniqueRowViewModel(editor, c)).ToList();
    }

    public IReadOnlyList<AttributeRowViewModel> Attributes { get; }

    public IReadOnlyList<SkillGroupViewModel> SkillGroups { get; }

    public IReadOnlyList<CombatTechniqueRowViewModel> CombatTechniques { get; }

    [ObservableProperty]
    public partial string AttributeTotalText { get; private set; } = "";

    /// <summary>Filtert Talente und Kampftechniken nach Namen.</summary>
    [ObservableProperty]
    public partial string Filter { get; set; } = "";

    partial void OnFilterChanged(string value) => ApplyVisibility();

    public void Update(CharacterState state)
    {
        foreach (var row in Attributes) row.Update(state);
        foreach (var row in SkillGroups.SelectMany(g => g.Rows)) row.Update(state);
        foreach (var row in CombatTechniques) row.Update(state);
        var level = state.ExperienceLevel;
        AttributeTotalText = $"Summe {state.Attributes.Sum(a => a.Value)} / {level.MaxAttributeTotal}";
        ApplyVisibility();
    }

    /// <summary>Einträge aus deaktivierten Quellen erscheinen nur, solange sie noch Werte tragen.</summary>
    private void ApplyVisibility()
    {
        var filter = Filter.Trim();
        bool Matches(string name) => filter.Length == 0 || name.Contains(filter, StringComparison.CurrentCultureIgnoreCase);

        foreach (var group in SkillGroups)
        {
            foreach (var row in group.Rows) row.IsVisible = (row.Available || row.Value > 0) && Matches(row.Name);
            group.IsVisible = group.Rows.Any(r => r.IsVisible);
        }
        foreach (var row in CombatTechniques)
            row.IsVisible = (row.Available || row.IssueText is not null) && Matches(row.Name);
        HasCombatTechniques = CombatTechniques.Any(r => r.IsVisible);
    }

    [ObservableProperty]
    public partial bool HasCombatTechniques { get; private set; } = true;
}

public sealed partial class SkillGroupViewModel(string title, IReadOnlyList<SkillRowViewModel> rows) : ObservableObject
{
    public string Title { get; } = title;

    public IReadOnlyList<SkillRowViewModel> Rows { get; } = rows;

    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;
}
