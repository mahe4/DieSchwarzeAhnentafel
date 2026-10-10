using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Core.Rules;

/// <summary>Zauber, Rituale, magische Handlungen, Liturgien, Zeremonien, Segnungen.</summary>
public sealed partial class CharacterCalculator
{
    /// <summary>
    /// Zauber und Liturgien (Wiki, Heldenerschaffung Schritt 8 und Erfahrung): Aktivierung plus Steigerung nach
    /// Kostentabelle, Zaubertricks und Segnungen 1 AP. Höchstwert: höchste Probeneigenschaft + 2, ohne Merkmals- bzw.
    /// Aspektkenntnis höchstens 14; bei der Erschaffung zusätzlich der Höchstwert des Erfahrungsgrads und die Anzahl
    /// der Zauber/Liturgien bzw. Fremdzauber.
    /// </summary>
    private List<SpellState> Spells(CreationLayer creation, bool inCreation, ExperienceLevel level, SourceSelection sources,
        IReadOnlyDictionary<string, int> attr, List<TraitState> traits, List<TraitState> specialAbilities,
        Func<EffectTarget, int> effect, CharacterState? creationState, List<Issue> issues)
    {
        var result = new List<SpellState>();
        var magic = specialAbilities.Where(t => t.Definition.Tradition?.Kind == AbilityDomain.Magical).Select(t => TraditionName(t.Definition.Name)).ToList();
        var karma = specialAbilities.Where(t => t.Definition.Tradition?.Kind == AbilityDomain.Karmal).Select(t => TraditionName(t.Definition.Name)).ToList();
        var costs = rules.Costs;
        for (var i = 0; i < creation.Spells.Count; i++)
        {
            var entry = creation.Spells[i];
            var target = SpellTarget(i);
            if (rules.FindSpell(entry.Id) is not { } def)
            {
                issues.Add(new Issue(IssueSeverity.Error, "spell.unknown", RuleScope.Always,
                    $"'{entry.Id}' ist in den Regeldaten nicht vorhanden.", target));
                continue;
            }
            var available = sources.IsAvailable(def.Publications);
            if (!available) issues.Add(SourceDisabled(target, def.Name));

            if (def.IsKarmal ? effect(EffectTarget.KarmaBase) <= 0 : effect(EffectTarget.AstralBase) <= 0)
                issues.Add(new Issue(IssueSeverity.Error, "spell.noGift", RuleScope.Always,
                    $"{def.Name} setzt den Vorteil {(def.IsKarmal ? "Geweihter" : "Zauberer")} voraus.", target));
            var own = def.IsKarmal ? OwnLiturgy(def, karma) : OwnSpell(def, magic, specialAbilities);
            foreach (var p in def.Prerequisites ?? [])
                if (p.Type == PrerequisiteType.RequiresAdvantage && traits.All(t => t.Definition.Name != p.Name))
                    issues.Add(new Issue(IssueSeverity.Error, "spell.prerequisite", RuleScope.Always,
                        $"{def.Name} ({def.Category}) setzt den Vorteil {p.Name} voraus.", target));
            var foreign = !own && !def.IsKarmal && def.Group is SpellGroup.Spell or SpellGroup.Ritual;
            if (!own && def.Group == SpellGroup.MagicalAction)
                issues.Add(new Issue(IssueSeverity.Warning, "spell.tradition", RuleScope.Always,
                    $"{def.Name} ({def.Category}) gehört zu einer anderen Tradition.", target));
            if (!own && def.IsKarmal && karma.Count > 0)
                issues.Add(new Issue(IssueSeverity.Warning, "spell.tradition", RuleScope.Always,
                    $"{def.Name} gehört nicht zur Tradition ({def.TraditionsText}).", target));

            int? value = null, max = null;
            int ap;
            var min = 0;
            if (def.FactorOptions is not null && def.FactorFor(entry.Option) is null)
                issues.Add(new Issue(IssueSeverity.Warning, "spell.factorOption", RuleScope.Always,
                    $"{def.Name}: Ausprägung wählen (bestimmt den Steigerungsfaktor).", target));
            if (def.HasValue)
            {
                var factor = def.FactorFor(entry.Option);
                value = Math.Max(0, entry.Value ?? 0);
                // Herausragende Fertigkeit gilt für alle Fertigkeiten, auch Zauber und Liturgien
                var bonus = Outstanding(traits, OutstandingSkill, def.Id);
                var ruleMax = def.Check is { } check ? check.Max(a => attr[a]) + rules.Creation.SkillMaxAboveCheckAttribute + bonus : (int?)null;
                // ohne Merkmals- bzw. Aspektkenntnis höchstens 14 (Erfahrung: Maximalwerte)
                var knowledge = Knows(def);
                if (!knowledge) ruleMax = Math.Min(ruleMax ?? 14 + bonus, 14 + bonus);
                var creationMax = level.MaxSkill + bonus;
                max = inCreation && ruleMax is { } m ? Math.Min(m, creationMax) : ruleMax;
                if (ruleMax is { } rm && value > rm)
                    issues.Add(new Issue(IssueSeverity.Error, "spell.max", RuleScope.Always,
                        $"{def.Name} {value} liegt über dem Höchstwert {rm}" +
                        (knowledge ? "." : $" (ohne {(def.IsKarmal ? "Aspektkenntnis" : "Merkmalskenntnis")} höchstens {14 + bonus})."), target));
                if (inCreation && value > creationMax)
                    issues.Add(new Issue(IssueSeverity.Error, "spell.creationMax", RuleScope.CreationOnly,
                        $"{def.Name} {value} liegt über dem Höchstwert {creationMax} ({level.Name}{OutstandingText(bonus)}).", target));
                ap = factor is { } f ? costs.Activation(f) + costs.Cost(f, 0, Math.Min(value.Value, costs.MaxValue)) : 0;
                min = creationState?.Spells.FirstOrDefault(x => x.Entry.Key == entry.Key)?.Value ?? 0;
            }
            else ap = def.Ap ?? 1;
            if (creation.Spells.Take(i).Any(o => o.Id == entry.Id))
                issues.Add(new Issue(IssueSeverity.Error, "spell.duplicate", RuleScope.Always, $"{def.Name} ist bereits vorhanden.", target));
            result.Add(new SpellState(def, entry, i, value, min, max, ap, foreign, available));
        }

        if (inCreation)
        {
            var count = result.Count(x => x.Definition.CountsForCreationLimit);
            if (count > level.MaxSpellsAndLiturgies)
                issues.Add(new Issue(IssueSeverity.Error, "spell.creationCount", RuleScope.CreationOnly,
                    $"{count} Zauber/Liturgien aktiviert, bei der Erschaffung höchstens {level.MaxSpellsAndLiturgies} ({level.Name})."));
            var foreignCount = result.Count(x => x.Foreign);
            if (foreignCount > level.MaxForeignSpells)
                issues.Add(new Issue(IssueSeverity.Error, "spell.creationForeign", RuleScope.CreationOnly,
                    $"{foreignCount} Fremdzauber, bei der Erschaffung höchstens {level.MaxForeignSpells} ({level.Name})."));
        }
        return result;

        bool Knows(SpellDefinition def) => specialAbilities.Any(t => t.Entry.Option is { } option &&
            (def.IsKarmal
                ? t.Definition.OptionTarget == TraitOptionTarget.Aspect && def.Aspects?.Contains(option) == true
                : t.Definition.OptionTarget == TraitOptionTarget.Property && def.Property == option));
    }

    /// <summary>Gehört der Zauber bzw. die Liturgie zur eigenen Tradition (für Auswahllisten)?</summary>
    public static bool IsOwnTradition(SpellDefinition def, IEnumerable<TraitState> specialAbilities)
    {
        var kind = def.IsKarmal ? AbilityDomain.Karmal : AbilityDomain.Magical;
        var names = specialAbilities.Where(t => t.Definition.Tradition?.Kind == kind).Select(t => TraditionName(t.Definition.Name)).ToList();
        return def.IsKarmal ? OwnLiturgy(def, names) : OwnSpell(def, names, specialAbilities);
    }

    /// <summary>„Tradition (Gildenmagier)“ → „Gildenmagier“.</summary>
    private static string TraditionName(string name) =>
        name.StartsWith("Tradition (", StringComparison.Ordinal) ? name[11..].TrimEnd(')') : name;

    /// <summary>Magische Handlungen gehören zu der Tradition, die sie im Wiki beherrscht.</summary>
    private static readonly Dictionary<string, string[]> ActionTraditions = new()
    {
        ["Animistenkräfte"] = ["Animisten"], ["Elfenlieder"] = ["Elfen"], ["Verzerrte Elfenlieder"] = ["Nachtalben"],
        ["Geodenrituale"] = ["Geoden", "Brobim-Geoden"], ["Goblinrituale"] = ["Goblinzauberinnen"], ["Herrschaftsrituale"] = ["Druiden"],
        ["Hexenflüche"] = ["Hexen"], ["Schelmenstreiche"] = ["Schelme"], ["Zaubermelodien"] = ["Zauberbarden"],
        ["Zaubertänze"] = ["Zaubertänzer"], ["Zibiljarituale"] = ["Zibiljas"], ["Bannzeichen"] = ["Bannzeichner"],
        ["Zauberrunen"] = ["Runenschöpfer"],
    };

    private static readonly Dictionary<string, string> ActionAbilities = new()
    {
        ["Zauberrunen"] = "Zauberrunenkunde", ["Bannzeichen"] = "Bannzeichnungskunde",
    };

    private static bool OwnSpell(SpellDefinition def, List<string> traditions, IEnumerable<TraitState>? specialAbilities = null)
    {
        if (def.Group == SpellGroup.MagicalAction)
            return ActionTraditions.TryGetValue(def.Category, out var owners)
                ? traditions.Any(t => owners.Any(o => SameStem(o, t))) ||
                  // Zauberrunen und Bannzeichen auch über Zauberrunenkunde bzw. Bannzeichnungskunde (Wiki, Übersichtsseiten)
                  ActionAbilities.TryGetValue(def.Category, out var sf) && (specialAbilities ?? []).Any(t => t.Definition.Name == sf)
                : true;
        if (def.Group == SpellGroup.Cantrip || def.Traditions is not { Count: > 0 } list) return true;
        return list.Any(t => t == "Allgemein" || traditions.Any(own => SameStem(t, own)));
    }

    /// <summary>Liturgie der eigenen Gottheit: „Praios“ passt zu „Praioskirche“, „allgemein“ zu allen.</summary>
    private static bool OwnLiturgy(SpellDefinition def, List<string> traditions) =>
        def.Traditions is not { Count: > 0 } list ||
        list.Any(t => t == "Allgemein" || traditions.Any(own => own.StartsWith(t.Split(' ')[0], StringComparison.CurrentCultureIgnoreCase)));

    /// <summary>„Hexe“/„Hexen“, „Elf“/„Elfen“, „Druide“/„Druiden“: Wortstamm vergleichen.</summary>
    private static bool SameStem(string a, string b)
    {
        static string Stem(string x) => x.Trim().TrimEnd('n', 'e').ToLowerInvariant();
        return Stem(a) == Stem(b);
    }

    public static string SpellTarget(int index) => "spell:" + index;
}
