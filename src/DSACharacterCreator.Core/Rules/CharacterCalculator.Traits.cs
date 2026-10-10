using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Core.Rules;

/// <summary>Vor- und Nachteile, Sonderfertigkeiten, Traditionen.</summary>
public sealed partial class CharacterCalculator
{
    /// <summary>Automatische Vor-/Nachteile der Spezies und gewählte Einträge, mit Kosten und Voraussetzungsprüfung.</summary>
    private List<TraitState> Traits(CreationLayer creation, OriginState origin, SourceSelection sources, List<Issue> issues)
    {
        var traitRules = new TraitRules(rules);
        var result = new List<TraitState>();

        if (origin.Species is { } species)
        {
            foreach (var (names, disadvantage) in new[] { (species.AutomaticAdvantages, false), (species.AutomaticDisadvantages, true) })
            foreach (var name in names)
            {
                if (traitRules.Resolve(name, disadvantage) is { } resolved)
                    result.Add(new TraitState(resolved.Definition, resolved.Entry, -1,
                        TraitRules.Level(resolved.Definition, resolved.Entry), 0, Automatic: true, Available: true));
                else
                    issues.Add(new Issue(IssueSeverity.Info, "traits.automaticUnknown", RuleScope.Always,
                        $"{species.Name}: Automatischer {(disadvantage ? "Nachteil" : "Vorteil")} „{name}“ ist in den Regeldaten nicht zu finden.",
                        species.Id));
            }
        }

        result.AddRange(Entries(creation.Traits, rules.FindTrait, "Vor-/Nachteil", TraitTarget, sources, issues));
        return result;
    }

    private List<TraitState> SpecialAbilities(CreationLayer creation, SourceSelection sources, List<Issue> issues) =>
        Entries(creation.SpecialAbilities, rules.FindSpecialAbility, "Sonderfertigkeit", SpecialAbilityTarget,
            sources, issues);

    private List<TraitState> Entries(List<TraitEntry> entries, Func<string, TraitDefinition?> find, string kind,
        Func<int, string> target, SourceSelection sources, List<Issue> issues)
    {
        var traitRules = new TraitRules(rules);
        var result = new List<TraitState>();
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (find(entry.Id) is not { } def)
            {
                issues.Add(new Issue(IssueSeverity.Error, "traits.unknown", RuleScope.Always,
                    $"{kind} '{entry.Id}' ist in den Regeldaten nicht vorhanden."));
                continue;
            }
            var occurrence = TraitRules.Occurrence(def, entries, i);
            var ap = traitRules.Cost(def, entry, occurrence);
            var available = sources.IsAvailable(def.Publications);
            if (!available) issues.Add(SourceDisabled(def.Id, def.Name));
            if (ap is null)
                issues.Add(new Issue(IssueSeverity.Warning, "traits.costOpen", RuleScope.Always,
                    def.OptionTarget switch
                    {
                        TraitOptionTarget.Skill => $"{def.Name}: Talent wählen.",
                        TraitOptionTarget.CombatTechnique => $"{def.Name}: Kampftechnik wählen.",
                        TraitOptionTarget.Script => $"{def.Name}: Schrift wählen.",
                        TraitOptionTarget.Language => $"{def.Name}: Sprache wählen.",
                        TraitOptionTarget.Property => $"{def.Name}: Merkmal wählen.",
                        TraitOptionTarget.Aspect => $"{def.Name}: Aspekt wählen.",
                        _ when def.Cost.Type == TraitCostType.ByOption => $"{def.Name}: Ausprägung wählen.",
                        _ => $"{def.Name}: AP-Wert eintragen ({def.ApValueText.Replace('\n', ' ')}).",
                    }, target(i)));
            result.Add(new TraitState(def, entry, i, TraitRules.Level(def, entry), ap, Automatic: false, available));
        }
        return result;
    }

    /// <summary>Die (erste) Tradition eines Bereichs; mehr als eine geweihte Tradition ist nicht erlaubt.</summary>
    private static TraditionInfo? Tradition(List<TraitState> specialAbilities, AbilityDomain kind, List<Issue> issues)
    {
        var found = specialAbilities.Where(t => t.Definition.Tradition?.Kind == kind).ToList();
        if (kind == AbilityDomain.Karmal && found.Count > 1)
            issues.Add(new Issue(IssueSeverity.Error, "karma.multipleTraditions", RuleScope.Always,
                "Man kann nur eine geweihte Tradition besitzen.", SpecialAbilityTarget(found[1].Index)));
        return found.FirstOrDefault()?.Definition.Tradition;
    }

    /// <summary>Leiteigenschaft der Tradition für den AE-/KE-Grundwert (bei manchen Traditionen nur zur Hälfte, aufgerundet).</summary>
    private static int PrimaryBonus(TraditionInfo? tradition, IReadOnlyDictionary<string, int> attr) =>
        tradition?.PrimaryAttribute is { } a ? tradition.HalfPrimaryAttribute ? HalfRoundUp(attr[a]) : attr[a] : 0;

    public static string TraitTarget(int index) => "trait:" + index;

    public static string SpecialAbilityTarget(int index) => "sf:" + index;

    private static string TargetOf(TraitState t) =>
        t.Definition.IsSpecialAbility ? SpecialAbilityTarget(t.Index) : TraitTarget(t.Index);
}
