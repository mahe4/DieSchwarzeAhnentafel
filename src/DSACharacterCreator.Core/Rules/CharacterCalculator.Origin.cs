using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Core.Rules;

/// <summary>Herkunft: Spezies, Kultur und Profession, Paketwerte der Vorlagen.</summary>
public sealed partial class CharacterCalculator
{
    private OriginState Origin(CreationLayer creation, SourceSelection sources, List<Issue> issues, ExperienceLevel level)
    {
        var species = rules.FindSpecies(creation.Species);
        var culture = rules.FindCulture(creation.Culture);
        var profession = rules.FindProfession(creation.Profession) is { } found ? rules.ProfessionForLevel(found, level.Id) : null;
        var variant = profession?.Variants?.FirstOrDefault(v => v.Id == creation.ProfessionVariant);

        void Unknown(string? id, object? found, string what)
        {
            if (id is not null && found is null)
                issues.Add(new Issue(IssueSeverity.Error, "origin.unknown", RuleScope.Always,
                    $"{what} '{id}' ist in den Regeldaten nicht vorhanden."));
        }
        Unknown(creation.Species, species, "Spezies");
        Unknown(creation.Culture, culture, "Kultur");
        Unknown(creation.Profession, profession, "Profession");

        if (species is not null && !sources.IsAvailable(species.Publications)) issues.Add(SourceDisabled(species.Id, species.Name));
        if (culture is not null && !sources.IsAvailable(culture.Publications)) issues.Add(SourceDisabled(culture.Id, culture.Name));
        if (profession is not null && !sources.IsAvailable(profession.Publications))
            issues.Add(SourceDisabled(profession.Id, profession.Name));

        // Eigenschaftsänderungen der Spezies
        var modifiers = new Dictionary<string, int>();
        if (species is not null)
        {
            var choiceIndex = 0;
            foreach (var change in species.AttributeChanges)
            {
                string? attribute = change.Options[0];
                if (change.IsChoice)
                {
                    attribute = creation.SpeciesAttributeChoices.ElementAtOrDefault(choiceIndex++);
                    if (attribute is null || !change.Options.Contains(attribute))
                    {
                        issues.Add(new Issue(IssueSeverity.Warning, "species.attributeChoice", RuleScope.CreationOnly,
                            $"Eigenschaftsänderung der Spezies wählen: {string.Join(" oder ", change.Options)} {change.Value:+0;−0}.",
                            species.Id));
                        continue;
                    }
                }
                modifiers[attribute] = modifiers.GetValueOrDefault(attribute) + change.Value;
            }
        }

        if (profession is not null)
        {
            if (profession.VariantRequired && variant is null)
                issues.Add(new Issue(IssueSeverity.Info, "profession.variant", RuleScope.CreationOnly,
                    $"{profession.Name}: Die Vorlage sieht die Wahl einer Variante vor.", profession.Id));
            if (profession.Unparsed is { Count: > 0 })
                issues.Add(new Issue(IssueSeverity.Warning, "profession.unparsed", RuleScope.CreationOnly,
                    $"{profession.Name}: Einige Angaben konnten nicht automatisch übernommen werden und müssen von Hand " +
                    $"eingetragen werden: {string.Join("; ", profession.Unparsed)}", profession.Id));
            var open = (profession.UnparsedSpecialAbilities ?? []).Concat(profession.UnparsedSpells ?? []).ToList();
            if (open.Count > 0)
                issues.Add(new Issue(IssueSeverity.Info, "profession.textOnly", RuleScope.CreationOnly,
                    $"{profession.Name}: Diese Teile der Vorlage bitte von Hand übernehmen: {string.Join("; ", open)}. " +
                    "Ihr Anteil am AP-Wert ist vorläufig mit dem Wiki-Rest angesetzt.", profession.Id));
            if (profession.CurseAp is { } curses)
                issues.Add(new Issue(IssueSeverity.Info, "profession.curses", RuleScope.CreationOnly,
                    $"{profession.Name}: Die Vorlage sieht Flüche für insgesamt {curses} AP vor (unter Magie selbst wählen).",
                    profession.Id));
        }

        return new OriginState(species, culture, creation.CulturePackage && culture?.Package is not null,
            profession, variant, modifiers);
    }

    /// <summary>Talentwerte aus Professionspaket (bzw. Variante) plus Boni des Kulturpakets.</summary>
    private static Dictionary<string, int> SkillPackage(OriginState origin)
    {
        var result = origin.Profession is { } p
            ? PackageCosts.ProfessionSkills(p, origin.Variant)
            : new Dictionary<string, int>();
        if (origin.CulturePackage)
            foreach (var v in origin.Culture!.Package!.Skills) result[v.Skill] = result.GetValueOrDefault(v.Skill) + v.Value;
        return result;
    }

    /// <summary>Kampftechnikwerte aus Professionspaket, Variante und getroffenen Auswahlen; meldet offene Auswahlen.</summary>
    private static Dictionary<string, int> CombatPackage(OriginState origin, CreationLayer creation, List<Issue> issues,
        PackageCosts packages)
    {
        var profession = origin.Profession;
        if (profession is null) return new Dictionary<string, int>();

        var chosen = new List<string>();
        var choices = profession.CombatTechniqueChoices ?? [];
        var picks = new List<string?>();
        for (var i = 0; i < choices.Count; i++)
        {
            var choice = choices[i];
            var pick = creation.ProfessionCombatTechniqueChoices.ElementAtOrDefault(i);
            if (pick is null || !choice.Options.Contains(pick))
            {
                issues.Add(new Issue(IssueSeverity.Info, "profession.combatChoice", RuleScope.CreationOnly,
                    $"{profession.Name}: Die Vorlage sieht eine Kampftechnik auf {choice.Value} vor (noch nicht gewählt).", profession.Id));
                picks.Add(null);
                continue;
            }
            if (chosen.Contains(pick))
                issues.Add(new Issue(IssueSeverity.Warning, "profession.combatChoiceTwice", RuleScope.CreationOnly,
                    $"{profession.Name}: Für die zweite Auswahl eine andere Kampftechnik wählen.", profession.Id));
            chosen.Add(pick);
            picks.Add(pick);
        }

        // Offene Auswahlen erhöhen keinen Wert (es wird nur für die Kostenbasis die günstigste Option angenommen).
        var values = packages.ProfessionCombatTechniques(profession, origin.Variant, picks);
        if (picks.Contains(null))
        {
            var withoutOpen = new Dictionary<string, int>();
            foreach (var v in profession.CombatTechniques) withoutOpen[v.CombatTechnique] = Math.Max(withoutOpen.GetValueOrDefault(v.CombatTechnique), v.Value);
            for (var i = 0; i < choices.Count; i++)
                if (picks[i] is { } p) withoutOpen[p] = Math.Max(withoutOpen.GetValueOrDefault(p), choices[i].Value);
            foreach (var v in origin.Variant?.CombatTechniques ?? []) withoutOpen[v.CombatTechnique] = v.Value;
            return withoutOpen;
        }
        return values;
    }
}
