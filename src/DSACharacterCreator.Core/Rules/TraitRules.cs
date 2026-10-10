using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Core.Rules;

/// <summary>Kosten, Auswirkungen und Voraussetzungen von Vor- und Nachteilen.</summary>
public sealed class TraitRules(RuleData rules)
{
    /// <summary>AP eines gewählten Eintrags (Nachteile negativ); <c>null</c>, wenn noch eine Angabe fehlt.</summary>
    public int? Cost(TraitDefinition def, TraitEntry entry)
    {
        var level = Level(def, entry);
        var c = def.Cost;
        return c.Type switch
        {
            TraitCostType.Fixed => c.Ap,
            TraitCostType.PerLevel => c.Ap * level,
            TraitCostType.Levels => c.Levels![Math.Min(level, c.Levels.Count) - 1],
            TraitCostType.BySkillFactor => Factor(c.Target!.Value, entry.Option) is { } f && c.ByFactor!.TryGetValue(f, out var ap)
                ? ap
                : null,
            TraitCostType.ByOption => c.Options!.FirstOrDefault(o => o.Name == entry.Option)?.Ap,
            _ => entry.ManualAp,
        };
    }

    public static int Level(TraitDefinition def, TraitEntry entry) =>
        def.MaxLevel is { } max ? Math.Clamp(entry.Level ?? 1, 1, max) : 1;

    private ImprovementCost? Factor(TraitOptionTarget target, string? option) => target switch
    {
        TraitOptionTarget.Skill => rules.Skills.FirstOrDefault(s => s.Id == option)?.ImprovementCost,
        _ => rules.CombatTechniques.FirstOrDefault(t => t.Id == option)?.ImprovementCost,
    };

    /// <summary>
    /// Ordnet Namen aus Spezies-Texten („Dunkelsicht I“, „Herausragender Sinn (Gehör)“) einer Definition zu.
    /// </summary>
    public (TraitDefinition Definition, TraitEntry Entry)? Resolve(string text, bool disadvantage)
    {
        var list = disadvantage ? rules.Disadvantages : rules.Advantages;
        var name = text.Trim();
        var level = 1;
        var m = System.Text.RegularExpressions.Regex.Match(name, @"^(.+?)\s+(I{1,3}|IV|V|VI|[1-6])$");
        if (m.Success)
        {
            name = m.Groups[1].Value;
            level = m.Groups[2].Value switch { "I" or "1" => 1, "II" or "2" => 2, "III" or "3" => 3, "IV" or "4" => 4, "V" or "5" => 5, _ => 6 };
        }
        var def = list.FirstOrDefault(d => d.Name == name);
        string? option = null;
        if (def is null)
        {
            var open = name.IndexOf(" (", StringComparison.Ordinal);
            if (open > 0)
            {
                def = list.FirstOrDefault(d => d.Name == name[..open]);
                option = name[(open + 2)..].TrimEnd(')');
            }
        }
        return def is null ? null : (def, new TraitEntry { Id = def.Id, Level = level, Option = option, Specification = option });
    }
}
