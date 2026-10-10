using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Core.Rules;

/// <summary>Voraussetzungen von Vor-/Nachteilen und Sonderfertigkeiten.</summary>
public sealed partial class CharacterCalculator
{
    /// <summary>Alles, was die Voraussetzungsprüfung braucht.</summary>
    private sealed record PrerequisiteContext(
        List<TraitState> All,
        OriginState Origin,
        IReadOnlyDictionary<string, int> Attributes,
        IReadOnlyDictionary<string, int> Skills,
        IReadOnlyDictionary<string, int> CombatTechniques,
        RuleData Rules,
        TraditionInfo? MagicTradition,
        TraditionInfo? KarmaTradition)
    {
        public IReadOnlyList<SpellState> Spells { get; init; } = [];
    }

    private static string Label(TraitState t) =>
        t.Definition.Name + (t.Entry.Specification is { Length: > 0 } s ? $" ({s})" : "") +
        (t.Definition.MaxLevel is not null ? " " + Roman(t.Level) : "");

    private static string Roman(int level) => TraitRules.Roman(level);

    private static void CheckPrerequisites(TraitState t, PrerequisiteContext ctx, List<Issue> issues)
    {
        var def = t.Definition;
        var all = ctx.All;
        var origin = ctx.Origin;
        var target = TargetOf(t);
        void Fail(string message) => issues.Add(new Issue(IssueSeverity.Error, "traits.prerequisite", RuleScope.Always,
            $"{Label(t)}: {message}", target));

        // Dieselbe Auswahl zweimal ergibt keinen Sinn – außer bei Fertigkeitsspezialisierungen (n-te Spezialisierung) und
        // Herausragender Fertigkeit (bis zu zweimal je Fertigkeit, Grenze über MaxCount).
        if (t.Entry.Option is { } chosen && !TraitRules.AllowsRepeatedOption(def) && all.Any(o => !o.Automatic && o.Index < t.Index &&
                o.Definition.Id == def.Id && o.Entry.Option == chosen))
            Fail("ist für diese Auswahl bereits vorhanden.");
        // Fertigkeitsspezialisierung: n-te Spezialisierung desselben Talents braucht FW 6 × n.
        if (def.Cost.Escalating && def.OptionTarget == TraitOptionTarget.Skill && t.Entry.Option is { } skill)
        {
            var n = 1 + all.Count(o => !o.Automatic && o.Index < t.Index && o.Definition.Id == def.Id && o.Entry.Option == skill);
            if (ctx.Skills.GetValueOrDefault(skill) < 6 * n)
                Fail($"{n}. Spezialisierung setzt {ctx.Rules.FindSkill(skill)!.Name} {6 * n} voraus.");
        }
        if (def.OptionTarget == TraitOptionTarget.Language && t.Entry.Option is { } lang &&
            ctx.Rules.FindLanguage(lang) is { MaxLevel: { } maxLanguage } language && t.Level > maxLanguage)
            Fail($"{language.Name} kann höchstens bis Stufe {Roman(maxLanguage)} erlernt werden.");

        foreach (var p in def.Prerequisites ?? [])
        {
            if (p.FromLevel is { } from && t.Level < from) continue;
            switch (p.Type)
            {
                case PrerequisiteType.OriginRecommends:
                    var texts = new[]
                    {
                        origin.Species?.AutomaticAdvantagesText, origin.Species?.AutomaticDisadvantagesText, origin.Species?.RecommendedText,
                        origin.Species?.TypicalAdvantagesText, origin.Species?.TypicalDisadvantagesText,
                        origin.Culture?.TypicalAdvantagesText, origin.Culture?.TypicalDisadvantagesText,
                        origin.Profession?.RecommendedAdvantagesText, origin.Profession?.RecommendedDisadvantagesText,
                    };
                    if (!texts.Any(x => x?.Contains(p.Name!, StringComparison.CurrentCultureIgnoreCase) == true))
                        issues.Add(new Issue(IssueSeverity.Warning, "traits.prerequisite", RuleScope.Always,
                            $"{Label(t)}: Spezies, Kultur oder Profession müssen {p.Name} als automatischen oder empfohlenen Eintrag aufweisen.",
                            target));
                    break;
                case PrerequisiteType.ExclusiveGroup when all.Any(o => o != t &&
                        o.Definition.Prerequisites?.Any(q => q.Type == PrerequisiteType.ExclusiveGroup && q.Group == p.Group) == true):
                    Fail("Nur ein Vorteil dieser Art ist erlaubt.");
                    break;
                case PrerequisiteType.MaxCount:
                {
                    // Einträge bis zu diesem (automatische zählen immer): Gesamtzahl und gleiche Auswahl
                    var upTo = all.Where(o => o.Definition.Id == def.Id && (o.Automatic || o.Index <= t.Index)).ToList();
                    if (p.Count is { } max && upTo.Count > max)
                        Fail(p.PerOption is null
                            ? $"darf höchstens {max}-mal gewählt werden (jeweils für eine andere Fertigkeit)."
                            : $"darf höchstens {max}-mal gewählt werden.");
                    // je Auswahl einmal meldet schon die allgemeine Prüfung „bereits vorhanden“
                    else if (p.PerOption is > 1 and var per && upTo.Count(o => o.Entry.Option == t.Entry.Option) > per)
                        Fail($"darf für dieselbe Auswahl höchstens {per}-mal gewählt werden.");
                    break;
                }
                default:
                    if (Evaluate(p, t, ctx) is (false, var message)) Fail(message);
                    break;
            }
        }
        if (def.UnparsedPrerequisites is { Count: > 0 } u)
            issues.Add(new Issue(IssueSeverity.Info, "traits.prerequisiteManual", RuleScope.Always,
                $"{Label(t)}: Voraussetzung bitte von Hand bzw. mit der Spielleitung prüfen: {string.Join("; ", u)}.", target));
    }

    /// <summary>Prüft eine Voraussetzung; (true, …) = erfüllt bzw. nicht prüfbar.</summary>
    private static (bool Ok, string Message) Evaluate(Prerequisite p, TraitState t, PrerequisiteContext ctx)
    {
        var all = ctx.All;
        var origin = ctx.Origin;
        bool Owns(string name, Func<TraitState, bool> kind)
        {
            // „Unfähig für diese Fertigkeit“, „Begabung auf das gleiche Talent“: nur bei derselben Fertigkeit
            var sameOption = System.Text.RegularExpressions.Regex.Match(name,
                @"^(.+?)\s+(?:für diese Fertigkeit|auf das gleiche Talent|für die gleiche Fertigkeit)$");
            if (sameOption.Success)
                return t.Entry.Option is { } option && all.Any(o => o != t && kind(o) &&
                    o.Definition.Name == sameOption.Groups[1].Value && o.Entry.Option == option);
            return all.Any(o => o != t && kind(o) && NameMatches(o, name));
        }
        static bool Adv(TraitState o) => !o.Definition.IsDisadvantage && !o.Definition.IsSpecialAbility;
        static bool Dis(TraitState o) => o.Definition.IsDisadvantage;
        static bool Sf(TraitState o) => o.Definition.IsSpecialAbility;
        string SkillName(string id) => ctx.Rules.FindSkill(id)?.Name ?? ctx.Rules.FindSpell(id)?.Name ?? id;
        string CombatName(string id) => ctx.Rules.FindCombatTechnique(id)?.Name ?? id;

        return p.Type switch
        {
            PrerequisiteType.Attribute => (ctx.Attributes.GetValueOrDefault(p.Attribute!) >= p.Min, $"setzt {p.Attribute} {p.Min} voraus."),
            PrerequisiteType.Skill => (ctx.Skills.GetValueOrDefault(p.Name!) >= p.Min, $"setzt {SkillName(p.Name!)} {p.Min} voraus."),
            PrerequisiteType.CombatTechnique => (ctx.CombatTechniques.GetValueOrDefault(p.Name!) >= p.Min,
                $"setzt {CombatName(p.Name!)} {p.Min} voraus."),
            PrerequisiteType.RequiresAdvantage => (Owns(p.Name!, Adv), $"setzt den Vorteil {p.Name} voraus."),
            PrerequisiteType.RequiresDisadvantage => (Owns(p.Name!, Dis), $"setzt den Nachteil {p.Name} voraus."),
            PrerequisiteType.RequiresSpecialAbility => (Owns(p.Name!, Sf), $"setzt die Sonderfertigkeit {p.Name} voraus."),
            PrerequisiteType.ForbidsAdvantage => (!Owns(p.Name!, Adv), $"ist mit dem Vorteil {p.Name} nicht vereinbar."),
            PrerequisiteType.ForbidsDisadvantage => (!Owns(p.Name!, Dis), $"ist mit dem Nachteil {p.Name} nicht vereinbar."),
            PrerequisiteType.ForbidsSpecialAbility => (!Owns(p.Name!, Sf), $"ist mit der Sonderfertigkeit {p.Name} nicht vereinbar."),
            PrerequisiteType.Species when origin.Species is { } s => (p.Names!.Any(n => SameSpecies(n, s.Name)),
                $"nur für Spezies {string.Join(" oder ", p.Names!)}."),
            PrerequisiteType.ForbidsSpecies when origin.Species is { } s => (!p.Names!.Any(n => SameSpecies(n, s.Name)),
                $"nicht für Spezies {s.Name}."),
            PrerequisiteType.Culture when origin.Culture is { } cu => (p.Names!.Any(n => SameSpecies(n, cu.Name)),
                $"nur für Kultur {string.Join(" oder ", p.Names!)}."),
            PrerequisiteType.CultureSocialStatus when origin.Culture is { } cu =>
                (cu.SocialStatusText?.Contains(p.Name!, StringComparison.CurrentCultureIgnoreCase) == true,
                    $"Die Kultur {cu.Name} kennt keinen passenden Sozialstatus ({p.Name})."),
            PrerequisiteType.AnyOf => p.Options!.Select(o => Evaluate(o, t, ctx)).ToList() is var results && results.Any(r => r.Ok)
                ? (true, "")
                : (false, "setzt eines voraus: " + string.Join(" oder ", results.Select(r => r.Message.Replace("setzt ", "").Replace(" voraus.", "")))),
            PrerequisiteType.FittingStyle => (all.Any(o => o != t && Sf(o) &&
                    (o.Definition.ExtendedAbilitiesText?.Contains(t.Definition.Name, StringComparison.CurrentCultureIgnoreCase) == true ||
                     t.Definition.FittingStylesText?.Contains(o.Definition.Name, StringComparison.CurrentCultureIgnoreCase) == true)),
                "setzt einen passenden Kampf- bzw. Talentstil voraus" +
                (t.Definition.FittingStylesText is { } styles ? $" ({styles})." : ".")),
            PrerequisiteType.PrimaryAttribute => PrimaryAttributeMet(p, t, ctx),
            // „3 Zauber des Merkmals auf 10“ für das gewählte Merkmal bzw. den gewählten Aspekt
            PrerequisiteType.SpellsWithOption when t.Entry.Option is { } option =>
                (ctx.Spells.Count(s => s.Value >= p.Min && (t.Definition.OptionTarget == TraitOptionTarget.Aspect
                    ? s.Definition.Aspects?.Contains(option) == true
                    : s.Definition.Property == option)) >= p.Count,
                    $"setzt {p.Count} {(t.Definition.OptionTarget == TraitOptionTarget.Aspect ? "Liturgien/Zeremonien des Aspekts" : "Zauber des Merkmals")} {option} auf {p.Min} voraus."),
            _ => (true, ""),
        };
    }

    /// <summary>„Leiteigenschaft der Tradition 13“: bei karmalen Sonderfertigkeiten die geweihte, sonst die magische Tradition.</summary>
    private static (bool Ok, string Message) PrimaryAttributeMet(Prerequisite p, TraitState t, PrerequisiteContext ctx)
    {
        var tradition = t.Definition.Domain switch
        {
            AbilityDomain.Karmal => ctx.KarmaTradition,
            AbilityDomain.Magical => ctx.MagicTradition,
            _ => ctx.MagicTradition ?? ctx.KarmaTradition,
        };
        if (tradition is null) return (false, $"setzt eine Tradition mit Leiteigenschaft {p.Min} voraus.");
        if (tradition.PrimaryAttribute is not { } a) return (true, "");
        return (ctx.Attributes.GetValueOrDefault(a) >= p.Min, $"setzt die Leiteigenschaft der Tradition ({a}) {p.Min} voraus.");
    }

    /// <summary>„Herausragender Sinn (Sicht)“ passt auf den Eintrag „Herausragender Sinn“ mit Präzisierung/Option „Sicht“.</summary>
    private static bool NameMatches(TraitState owned, string name)
    {
        var def = owned.Definition.Name;
        if (string.Equals(name, def, StringComparison.CurrentCultureIgnoreCase)) return true;
        if (!name.StartsWith(def, StringComparison.CurrentCultureIgnoreCase)) return false;
        var rest = name[def.Length..].Trim();
        if (rest.StartsWith('(')) rest = rest.Trim('(', ')');
        // „Prinzipientreue I (…)“, „Drachenblut (alle)“: Stufe bzw. „alle“ passt auf jede Ausprägung
        if (rest.Length == 0 || rest.Equals("alle", StringComparison.CurrentCultureIgnoreCase)) return true;
        var spec = owned.Entry.Specification ?? owned.Entry.Option ?? "";
        // „Finte II“: Stufe II oder höher
        var levelMatch = System.Text.RegularExpressions.Regex.Match(rest, @"^(X{0,1}(?:IX|IV|V?I{0,3}))\b");
        if (levelMatch.Success && TraitRules.ParseRoman(levelMatch.Value) is > 0 and var required)
            return owned.Level >= required;
        return rest.Contains(spec, StringComparison.CurrentCultureIgnoreCase) && spec.Length > 0;
    }

    /// <summary>
    /// „Elfen“/„Elf“, „Zwerg“/„Zwerge“, „Nachalben“ (Tippfehler im Wiki)/„Nachtalb“, „Thorwaler“/„Thorwal“ – grob über den
    /// Wortstamm (auch für Kulturnamen).
    /// </summary>
    private static bool SameSpecies(string a, string b)
    {
        string Stem(string x) => x.Trim().ToLowerInvariant().Replace("nachalb", "nachtalb")[..Math.Min(4, x.Trim().Length)];
        return Stem(a) == Stem(b);
    }
}
