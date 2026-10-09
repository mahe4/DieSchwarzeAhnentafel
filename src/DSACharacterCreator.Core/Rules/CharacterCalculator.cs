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
        var sources = new SourceSelection(character.EnabledBooks);
        var issues = new List<Issue>();
        var costs = rules.Costs;
        var cr = rules.Creation;

        // Eigenschaften
        var attributes = rules.Attributes.Select(def =>
        {
            var value = creation.Attributes.GetValueOrDefault(def.Id, cr.AttributeStartValue);
            var ap = SafeCost(ImprovementCost.E, cr.AttributeStartValue, value, def.Id, def.Name);
            if (inCreation && value > level.MaxAttribute)
                issues.Add(new Issue(IssueSeverity.Error, "attribute.creationMax", RuleScope.CreationOnly,
                    $"{def.Name} {value} liegt über dem Höchstwert {level.MaxAttribute} ({level.Name}).", def.Id));
            if (value < cr.AttributeStartValue)
                issues.Add(new Issue(IssueSeverity.Error, "attribute.min", RuleScope.CreationOnly,
                    $"{def.Name} darf nicht unter {cr.AttributeStartValue} liegen.", def.Id));
            return new AttributeState(def, value, cr.AttributeStartValue, inCreation ? level.MaxAttribute : null, ap);
        }).ToList();
        var attr = attributes.ToDictionary(a => a.Definition.Id, a => a.Value);

        var total = attributes.Sum(a => a.Value);
        if (inCreation && total > level.MaxAttributeTotal)
            issues.Add(new Issue(IssueSeverity.Error, "attribute.creationTotal", RuleScope.CreationOnly,
                $"Die Eigenschaften ergeben zusammen {total} Punkte, erlaubt sind höchstens {level.MaxAttributeTotal} ({level.Name})."));

        // Talente
        var skills = rules.Skills.Select(def =>
        {
            var value = creation.Skills.GetValueOrDefault(def.Id, 0);
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
            var ap = SafeCost(def.ImprovementCost, 0, value, def.Id, def.Name);
            return new SkillState(def, value, max, ap, available);
        }).ToList();

        // Kampftechniken
        var combatTechniques = rules.CombatTechniques.Select(def =>
        {
            var start = cr.CombatTechniqueStartValue;
            var value = creation.CombatTechniques.GetValueOrDefault(def.Id, start);
            var ruleMax = def.PrimaryAttributes.Max(a => attr[a]) + cr.CombatTechniqueMaxAbovePrimaryAttribute;
            var max = inCreation ? Math.Min(ruleMax, level.MaxCombatTechnique) : ruleMax;
            var available = sources.IsAvailable(def.Publications);
            if (inCreation && value > level.MaxCombatTechnique)
                issues.Add(new Issue(IssueSeverity.Error, "ct.creationMax", RuleScope.CreationOnly,
                    $"{def.Name} {value} liegt über dem Höchstwert {level.MaxCombatTechnique} ({level.Name}).", def.Id));
            if (value > ruleMax)
                issues.Add(new Issue(IssueSeverity.Error, "ct.attributeMax", RuleScope.Always,
                    $"{def.Name} {value} liegt über der Leiteigenschaft + {cr.CombatTechniqueMaxAbovePrimaryAttribute} ({ruleMax}).", def.Id));
            if (value < start)
                issues.Add(new Issue(IssueSeverity.Error, "ct.min", RuleScope.CreationOnly,
                    $"{def.Name} darf nicht unter {start} liegen.", def.Id));
            if (!available && value != start)
                issues.Add(SourceDisabled(def.Id, def.Name));
            var ap = SafeCost(def.ImprovementCost, start, value, def.Id, def.Name);
            return CombatValues(def, value, start, max, ap, available, attr);
        }).ToList();

        var account = new ApAccount(level.Ap, attributes.Sum(a => a.ApSpent), skills.Sum(s => s.ApSpent),
            combatTechniques.Sum(c => c.ApSpent));
        if (account.Remaining < 0)
            issues.Add(new Issue(IssueSeverity.Error, "ap.overspent", RuleScope.Always,
                $"Das AP-Konto ist um {-account.Remaining} AP überzogen."));
        else if (inCreation && account.Remaining > cr.MaxLeftoverAp)
            issues.Add(new Issue(IssueSeverity.Info, "ap.leftover", RuleScope.CreationOnly,
                $"Noch {account.Remaining} AP offen. Am Ende der Erschaffung dürfen höchstens {cr.MaxLeftoverAp} AP übrig bleiben."));

        // Basiswerte (Kodex der Helden S. 22): halbe Werte werden aufgerundet.
        var derived = new DerivedValues(
            Dodge: HalfRoundUp(attr["GE"]),
            Initiative: HalfRoundUp(attr["MU"] + attr["GE"]),
            FatePoints: cr.FatePoints);

        return new CharacterState(level, attributes, skills, combatTechniques, account, derived, issues);

        int SafeCost(ImprovementCost cost, int from, int to, string id, string name)
        {
            if (to <= costs.MaxValue) return costs.Cost(cost, from, Math.Max(to, from));
            issues.Add(new Issue(IssueSeverity.Error, "cost.tableMax", RuleScope.Always,
                $"{name} {to} liegt über dem Ende der Kostentabelle ({costs.MaxValue}).", id));
            return costs.Cost(cost, from, costs.MaxValue);
        }
    }

    /// <summary>
    /// AT = KtW + je 3 volle Punkte der Attacke-Eigenschaft über 8; PA = halber KtW (aufgerundet) + je 3 volle Punkte
    /// der (besten) Leiteigenschaft über 8; FK = KtW + je 3 volle Punkte FF über 8 (Kodex der Helden S. 19 ff.).
    /// </summary>
    private static CombatTechniqueState CombatValues(CombatTechniqueDefinition def, int value, int min, int max, int ap,
        bool available, IReadOnlyDictionary<string, int> attr)
    {
        if (def.Category == CombatTechniqueCategory.Ranged)
            return new CombatTechniqueState(def, value, min, max, ap, available, null, null,
                value + Bonus(attr[def.AttackAttribute]));

        int? parry = def.CanParry
            ? HalfRoundUp(value) + Bonus(def.PrimaryAttributes.Max(a => attr[a]))
            : null;
        return new CombatTechniqueState(def, value, min, max, ap, available,
            value + Bonus(attr[def.AttackAttribute]), parry, null);
    }

    private static int Bonus(int attribute) => Math.Max(0, (attribute - 8) / 3);

    private static int HalfRoundUp(int value) => (value + 1) / 2;

    private static Issue SourceDisabled(string id, string name) =>
        new(IssueSeverity.Warning, "source.disabled", RuleScope.Always,
            $"{name} stammt aus einer deaktivierten Quelle.", id);
}
