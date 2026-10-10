using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Core.Rules;

/// <summary>Berechnet den Stand eines Helden als reine Funktion seiner Entscheidungen (PLAN.md Abschnitt 4).</summary>
public sealed class CharacterCalculator(RuleData rules)
{
    public CharacterState Calculate(Character character)
    {
        var creation = character.Creation;
        var inCreation = character.Phase == CharacterPhase.Creation;
        var level = rules.ExperienceLevel(creation.ExperienceLevel);
        var sources = SourceSelection.ForCharacter(character, rules.Books);
        var issues = new List<Issue>();
        var costs = rules.Costs;
        var cr = rules.Creation;

        var origin = Origin(creation, sources, issues);

        // Eigenschaften: gekaufter Wert (Kosten, Summe) + Eigenschaftsänderung der Spezies
        var attributes = rules.Attributes.Select(def =>
        {
            var purchased = creation.Attributes.GetValueOrDefault(def.Id, cr.AttributeStartValue);
            var modifier = origin.AttributeModifiers.GetValueOrDefault(def.Id);
            var value = purchased + modifier;
            var ap = SafeCost(ImprovementCost.E, cr.AttributeStartValue, purchased, def.Id, def.Name);
            if (inCreation && purchased > level.MaxAttribute)
                issues.Add(new Issue(IssueSeverity.Error, "attribute.creationMax", RuleScope.CreationOnly,
                    $"{def.Name} {value} liegt über dem Höchstwert {level.MaxAttribute + modifier} ({level.Name}" +
                    (modifier != 0 ? $", Spezies {modifier:+0;−0}" : "") + ").", def.Id));
            if (purchased < cr.AttributeStartValue)
                issues.Add(new Issue(IssueSeverity.Error, "attribute.min", RuleScope.CreationOnly,
                    $"{def.Name} darf nicht unter {cr.AttributeStartValue + modifier} liegen.", def.Id));
            return new AttributeState(def, value, purchased, modifier, cr.AttributeStartValue + modifier,
                inCreation ? level.MaxAttribute + modifier : null, ap);
        }).ToList();
        var attr = attributes.ToDictionary(a => a.Definition.Id, a => a.Value);

        var total = attributes.Sum(a => a.Purchased);
        if (inCreation && total > level.MaxAttributeTotal)
            issues.Add(new Issue(IssueSeverity.Error, "attribute.creationTotal", RuleScope.CreationOnly,
                $"Die Eigenschaften ergeben zusammen {total} Punkte (ohne Spezies-Änderungen), erlaubt sind höchstens {level.MaxAttributeTotal} ({level.Name})."));

        foreach (var (id, min) in origin.Profession?.AttributeRequirements ?? new Dictionary<string, int>())
        {
            if (attr[id] < min)
                issues.Add(new Issue(IssueSeverity.Error, "profession.attribute", RuleScope.CreationOnly,
                    $"{origin.Profession!.Name} setzt {id} {min} voraus ({id} ist {attr[id]}).", id));
        }

        // Paketwerte
        var skillPackage = SkillPackage(origin);
        var combatPackage = CombatPackage(origin, creation, issues);

        // Talente
        var skills = rules.Skills.Select(def =>
        {
            var package = skillPackage.GetValueOrDefault(def.Id);
            var value = Math.Max(0, package + creation.SkillAdjustments.GetValueOrDefault(def.Id));
            var ruleMax = def.Check.Max(a => attr[a]) + cr.SkillMaxAboveCheckAttribute;
            var max = inCreation ? Math.Min(ruleMax, level.MaxSkill) : ruleMax;
            var available = sources.IsAvailable(def.Publications);
            if (inCreation && value > level.MaxSkill)
                issues.Add(new Issue(IssueSeverity.Error, "skill.creationMax", RuleScope.CreationOnly,
                    $"{def.Name} {value} liegt über dem Höchstwert {level.MaxSkill} ({level.Name}).", def.Id));
            if (value > ruleMax)
                issues.Add(new Issue(IssueSeverity.Error, "skill.attributeMax", RuleScope.Always,
                    $"{def.Name} {value} liegt über der höchsten Probeneigenschaft + {cr.SkillMaxAboveCheckAttribute} ({ruleMax}).", def.Id));
            if (!available && value > 0)
                issues.Add(SourceDisabled(def.Id, def.Name));
            var ap = SafeCost(def.ImprovementCost, package, value, def.Id, def.Name);
            return new SkillState(def, value, package, max, ap, available);
        }).ToList();

        // Kampftechniken
        var combatTechniques = rules.CombatTechniques.Select(def =>
        {
            var start = cr.CombatTechniqueStartValue;
            var package = Math.Max(start, combatPackage.GetValueOrDefault(def.Id));
            var value = Math.Max(start, package + creation.CombatTechniqueAdjustments.GetValueOrDefault(def.Id));
            var ruleMax = def.PrimaryAttributes.Max(a => attr[a]) + cr.CombatTechniqueMaxAbovePrimaryAttribute;
            var max = inCreation ? Math.Min(ruleMax, level.MaxCombatTechnique) : ruleMax;
            var available = sources.IsAvailable(def.Publications);
            if (inCreation && value > level.MaxCombatTechnique)
                issues.Add(new Issue(IssueSeverity.Error, "ct.creationMax", RuleScope.CreationOnly,
                    $"{def.Name} {value} liegt über dem Höchstwert {level.MaxCombatTechnique} ({level.Name}).", def.Id));
            if (value > ruleMax)
                issues.Add(new Issue(IssueSeverity.Error, "ct.attributeMax", RuleScope.Always,
                    $"{def.Name} {value} liegt über der Leiteigenschaft + {cr.CombatTechniqueMaxAbovePrimaryAttribute} ({ruleMax}).", def.Id));
            if (!available && value != start)
                issues.Add(SourceDisabled(def.Id, def.Name));
            var ap = SafeCost(def.ImprovementCost, package, value, def.Id, def.Name);
            return CombatValues(def, value, package, start, max, ap, available, attr);
        }).ToList();

        var originAp = (origin.Species?.Ap ?? 0)
                       + (origin.CulturePackage ? origin.Culture?.Package?.Ap ?? 0 : 0)
                       + (origin.Variant?.Ap ?? origin.Profession?.Ap ?? 0);
        var account = new ApAccount(level.Ap, originAp, attributes.Sum(a => a.ApSpent), skills.Sum(s => s.ApSpent),
            combatTechniques.Sum(c => c.ApSpent));
        if (account.Remaining < 0)
            issues.Add(new Issue(IssueSeverity.Error, "ap.overspent", RuleScope.Always,
                $"Das AP-Konto ist um {-account.Remaining} AP überzogen."));
        else if (inCreation && account.Remaining > cr.MaxLeftoverAp)
            issues.Add(new Issue(IssueSeverity.Info, "ap.leftover", RuleScope.CreationOnly,
                $"Noch {account.Remaining} AP offen. Am Ende der Erschaffung dürfen höchstens {cr.MaxLeftoverAp} AP übrig bleiben."));

        // Basiswerte (Kodex der Helden S. 22): halbe Werte und Sechstel werden kaufmännisch gerundet.
        var species = origin.Species;
        var derived = new DerivedValues(
            LifePoints: species is null ? null : species.LifeBase + 2 * attr["KO"],
            Spirit: species is null ? null : species.SpiritBase + SixthRounded(attr["MU"] + attr["KL"] + attr["IN"]),
            Toughness: species is null ? null : species.ToughnessBase + SixthRounded(attr["KO"] + attr["KO"] + attr["KK"]),
            Speed: species?.SpeedBase,
            Dodge: HalfRoundUp(attr["GE"]),
            Initiative: HalfRoundUp(attr["MU"] + attr["GE"]),
            FatePoints: cr.FatePoints);

        return new CharacterState(level, origin, attributes, skills, combatTechniques, account, derived, issues);

        int SafeCost(ImprovementCost cost, int from, int to, string id, string name)
        {
            if (Math.Max(from, to) <= costs.MaxValue) return costs.Cost(cost, from, to);
            issues.Add(new Issue(IssueSeverity.Error, "cost.tableMax", RuleScope.Always,
                $"{name} {to} liegt über dem Ende der Kostentabelle ({costs.MaxValue}).", id));
            return costs.Cost(cost, Math.Min(from, costs.MaxValue), Math.Min(to, costs.MaxValue));
        }
    }

    private OriginState Origin(CreationLayer creation, SourceSelection sources, List<Issue> issues)
    {
        var species = rules.FindSpecies(creation.Species);
        var culture = rules.FindCulture(creation.Culture);
        var profession = rules.FindProfession(creation.Profession);
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
                issues.Add(new Issue(IssueSeverity.Warning, "profession.variant", RuleScope.CreationOnly,
                    $"{profession.Name}: Eine Variante muss gewählt werden.", profession.Id));
            if (profession.Unparsed is { Count: > 0 })
                issues.Add(new Issue(IssueSeverity.Warning, "profession.unparsed", RuleScope.CreationOnly,
                    $"{profession.Name}: Einige Angaben konnten nicht automatisch übernommen werden und müssen von Hand " +
                    $"eingetragen werden: {string.Join("; ", profession.Unparsed)}", profession.Id));
            if (profession.SpecialAbilitiesText is not null || profession.SpellsText is not null || profession.LiturgiesText is not null)
                issues.Add(new Issue(IssueSeverity.Info, "profession.textOnly", RuleScope.CreationOnly,
                    $"{profession.Name}: Sonderfertigkeiten, Zauber und Liturgien des Pakets werden erst mit späteren " +
                    "Meilensteinen übernommen; bis dahin bitte nach dem Pakettext eintragen.", profession.Id));
        }

        return new OriginState(species, culture, creation.CulturePackage && culture?.Package is not null,
            profession, variant, modifiers);
    }

    /// <summary>Talentwerte aus Professionspaket (bzw. Variante) plus Boni des Kulturpakets.</summary>
    private static Dictionary<string, int> SkillPackage(OriginState origin)
    {
        var result = new Dictionary<string, int>();
        foreach (var v in origin.Profession?.Skills ?? []) result[v.Skill] = v.Value;
        foreach (var v in origin.Variant?.Skills ?? []) result[v.Skill] = v.Value;
        if (origin.CulturePackage)
            foreach (var v in origin.Culture!.Package!.Skills) result[v.Skill] = result.GetValueOrDefault(v.Skill) + v.Value;
        return result;
    }

    /// <summary>Kampftechnikwerte aus Professionspaket, Variante und getroffenen Auswahlen (jeweils das Maximum).</summary>
    private static Dictionary<string, int> CombatPackage(OriginState origin, CreationLayer creation, List<Issue> issues)
    {
        var result = new Dictionary<string, int>();
        void Raise(string id, int value) => result[id] = Math.Max(result.GetValueOrDefault(id), value);

        var profession = origin.Profession;
        if (profession is null) return result;
        foreach (var v in profession.CombatTechniques) Raise(v.CombatTechnique, v.Value);

        var chosen = new List<string>();
        var choices = profession.CombatTechniqueChoices ?? [];
        for (var i = 0; i < choices.Count; i++)
        {
            var choice = choices[i];
            var pick = creation.ProfessionCombatTechniqueChoices.ElementAtOrDefault(i);
            if (pick is null || !choice.Options.Contains(pick))
            {
                issues.Add(new Issue(IssueSeverity.Warning, "profession.combatChoice", RuleScope.CreationOnly,
                    $"{profession.Name}: Kampftechnik auf {choice.Value} wählen.", profession.Id));
                continue;
            }
            if (chosen.Contains(pick))
                issues.Add(new Issue(IssueSeverity.Warning, "profession.combatChoiceTwice", RuleScope.CreationOnly,
                    $"{profession.Name}: Für die zweite Auswahl eine andere Kampftechnik wählen.", profession.Id));
            chosen.Add(pick);
            Raise(pick, choice.Value);
        }

        // Varianten ersetzen Werte („Dolche 10 statt 6“).
        foreach (var v in origin.Variant?.CombatTechniques ?? []) result[v.CombatTechnique] = v.Value;
        return result;
    }

    /// <summary>
    /// AT = KtW + je 3 volle Punkte der Attacke-Eigenschaft über 8; PA = halber KtW (aufgerundet) + je 3 volle Punkte
    /// der (besten) Leiteigenschaft über 8; FK = KtW + je 3 volle Punkte FF über 8 (Kodex der Helden S. 19 ff.).
    /// </summary>
    private static CombatTechniqueState CombatValues(CombatTechniqueDefinition def, int value, int package, int min,
        int max, int ap, bool available, IReadOnlyDictionary<string, int> attr)
    {
        if (def.Category == CombatTechniqueCategory.Ranged)
            return new CombatTechniqueState(def, value, package, min, max, ap, available, null, null,
                value + Bonus(attr[def.AttackAttribute]));

        int? parry = def.CanParry
            ? HalfRoundUp(value) + Bonus(def.PrimaryAttributes.Max(a => attr[a]))
            : null;
        return new CombatTechniqueState(def, value, package, min, max, ap, available,
            value + Bonus(attr[def.AttackAttribute]), parry, null);
    }

    private static int Bonus(int attribute) => Math.Max(0, (attribute - 8) / 3);

    private static int HalfRoundUp(int value) => (value + 1) / 2;

    /// <summary>Summe / 6, kaufmännisch gerundet (Tabelle Seelenkraft/Zähigkeit: 24–26 → 4, 27–32 → 5 …).</summary>
    private static int SixthRounded(int sum) => (sum + 3) / 6;

    private static Issue SourceDisabled(string id, string name) =>
        new(IssueSeverity.Warning, "source.disabled", RuleScope.Always,
            $"{name} stammt aus einer deaktivierten Quelle.", id);
}
