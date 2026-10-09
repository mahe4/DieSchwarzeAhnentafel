using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.ViewModels;

public sealed class AttributeRowViewModel(EditorViewModel editor, AttributeDefinition definition)
    : ValueRowViewModel(editor, definition.Id, definition.Name)
{
    public override string Detail => definition.Id;

    public void Update(CharacterState state)
    {
        var a = state.Attributes.First(x => x.Definition.Id == Id);
        SetValues(a.Value, a.Min, a.Max, state.IssuesFor(Id));
    }

    protected override int? NextStepCost(int value) =>
        value <= Editor.Rules.Costs.MaxValue ? Editor.Rules.Costs.StepCost(ImprovementCost.E, value) : null;

    protected override void Store(int value) => Editor.Apply(c =>
    {
        if (value == Editor.Rules.Creation.AttributeStartValue) c.Attributes.Remove(Id);
        else c.Attributes[Id] = value;
    });
}

public sealed class SkillRowViewModel(EditorViewModel editor, SkillDefinition definition)
    : ValueRowViewModel(editor, definition.Id, definition.Name)
{
    public SkillDefinition Definition => definition;

    public override string Detail => $"{string.Join("/", definition.Check)} · {definition.ImprovementCost} · Belastung: {definition.EncumbranceText}";

    public override string Details => string.Join(Environment.NewLine + Environment.NewLine, new[]
    {
        Line("Anwendungsgebiete", definition.Applications),
        definition.Tools is { } tools ? Line("Werkzeuge", tools) : null,
        Line("Qualität", definition.Quality),
        Line("Misslungene Probe", definition.FailedCheck),
        Line("Kritischer Erfolg", definition.CriticalSuccess),
        Line("Patzer", definition.Botch),
    }.Where(l => l is not null));

    private static string Line(string label, string text) => $"{label}: {text}";

    public bool Available { get; private set; } = true;

    public void Update(CharacterState state)
    {
        var s = state.Skills.First(x => x.Definition.Id == Id);
        Available = s.Available;
        SetValues(s.Value, 0, s.Max, state.IssuesFor(Id));
    }

    protected override int? NextStepCost(int value) =>
        value <= Editor.Rules.Costs.MaxValue ? Editor.Rules.Costs.StepCost(definition.ImprovementCost, value) : null;

    protected override void Store(int value) => Editor.Apply(c =>
    {
        if (value == 0) c.Skills.Remove(Id);
        else c.Skills[Id] = value;
    });
}

public sealed class CombatTechniqueRowViewModel(EditorViewModel editor, CombatTechniqueDefinition definition)
    : ValueRowViewModel(editor, definition.Id, definition.Name)
{
    private string _combatText = "";

    public override string Detail =>
        $"{(definition.Category == CombatTechniqueCategory.Ranged ? "Fernkampf" : "Nahkampf")} · {string.Join("/", definition.PrimaryAttributes)} · {definition.ImprovementCost}";

    public override string? Details => definition.Special;

    public string CombatText
    {
        get => _combatText;
        private set => SetProperty(ref _combatText, value);
    }

    public bool Available { get; private set; } = true;

    public void Update(CharacterState state)
    {
        var c = state.CombatTechniques.First(x => x.Definition.Id == Id);
        Available = c.Available;
        CombatText = c.RangedAttack is { } fk
            ? $"FK {fk}"
            : c.Parry is { } pa ? $"AT {c.Attack} · PA {pa}" : $"AT {c.Attack}";
        SetValues(c.Value, c.Min, c.Max, state.IssuesFor(Id));
    }

    protected override int? NextStepCost(int value) =>
        value <= Editor.Rules.Costs.MaxValue ? Editor.Rules.Costs.StepCost(definition.ImprovementCost, value) : null;

    protected override void Store(int value) => Editor.Apply(c =>
    {
        if (value == Editor.Rules.Creation.CombatTechniqueStartValue) c.CombatTechniques.Remove(Id);
        else c.CombatTechniques[Id] = value;
    });
}
