using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Core.Rules;

/// <summary>Berechnet den Stand eines Helden als reine Funktion seiner Entscheidungen (PLAN.md Abschnitt 4).</summary>
public sealed partial class CharacterCalculator(RuleData rules)
{
    /// <summary>Zwischenspeicher fürs Protokoll (eine Berechnung je Editor; nicht threadsicher).</summary>
    internal ReplayCache Cache { get; } = new();

    public CharacterState Calculate(Character character)
    {
        var inCreation = character.Phase == CharacterPhase.Creation;
        var creationState = CalculateLayer(character, character.Creation, inCreation, 0, null);
        if (character.Log.Count == 0) return creationState;
        return new AdvancementReplay(this, rules).Run(character, creationState);
    }

    /// <summary>
    /// Berechnet eine Entscheidungsschicht: die Erschaffung selbst oder (beim Abspielen des Protokolls) eine Arbeitskopie
    /// mit den bis dahin angewandten Steigerungen.
    /// </summary>
    /// <param name="inCreation">Erschaffungsgrenzen prüfen (nur für den Erschaffungsstand in der Erschaffungsphase).</param>
    /// <param name="bonusAp">AP-Gutschriften abzüglich verfallener Start-AP.</param>
    /// <param name="creationState">Erschaffungsstand beim Abspielen: Steigerungen lassen sich nur bis dorthin zurücknehmen.</param>
    internal CharacterState CalculateLayer(Character character, CreationLayer creation, bool inCreation, int bonusAp,
        CharacterState? creationState)
    {
        var level = rules.ExperienceLevel(creation.ExperienceLevel);
        var sources = SourceSelection.ForCharacter(character, rules.Books);
        var issues = new List<Issue>();
        var costs = rules.Costs;
        var cr = rules.Creation;

        var origin = Origin(creation, sources, issues, level);

        // Eigenschaften: Die Eigenschaftsänderungen der Spezies verschieben nur den Höchstwert bei der Erschaffung
        // (Zwerg: KO/KK bis 15 statt 14); der Wert selbst und die Eigenschaftssumme bleiben unberührt.
        var attributes = rules.Attributes.Select(def =>
        {
            var value = creation.Attributes.GetValueOrDefault(def.Id, cr.AttributeStartValue);
            var modifier = origin.AttributeModifiers.GetValueOrDefault(def.Id);
            var creationMax = level.MaxAttribute + modifier;
            var ap = SafeCost(ImprovementCost.E, cr.AttributeStartValue, value, def.Id, def.Name);
            if (inCreation && value > creationMax)
                issues.Add(new Issue(IssueSeverity.Error, "attribute.creationMax", RuleScope.CreationOnly,
                    $"{def.Name} {value} liegt über dem Höchstwert {creationMax} ({level.Name}" +
                    (modifier != 0 ? $", Spezies {modifier:+0;−0}" : "") + ").", def.Id));
            if (value < cr.AttributeStartValue)
                issues.Add(new Issue(IssueSeverity.Error, "attribute.min", RuleScope.CreationOnly,
                    $"{def.Name} darf nicht unter {cr.AttributeStartValue} liegen.", def.Id));
            var min = creationState?.Attribute(def.Id) ?? cr.AttributeStartValue;
            return new AttributeState(def, value, value, modifier, min, inCreation ? creationMax : null, ap);
        }).ToList();
        var attr = attributes.ToDictionary(a => a.Definition.Id, a => a.Value);

        var total = attributes.Sum(a => a.Value);
        if (inCreation && total > level.MaxAttributeTotal)
            issues.Add(new Issue(IssueSeverity.Error, "attribute.creationTotal", RuleScope.CreationOnly,
                $"Die Eigenschaften ergeben zusammen {total} Punkte, erlaubt sind höchstens {level.MaxAttributeTotal} ({level.Name})."));

        foreach (var (id, min) in origin.Profession?.AttributeRequirements ?? new Dictionary<string, int>())
        {
            if (attr[id] < min)
                issues.Add(new Issue(IssueSeverity.Warning, "profession.attribute", RuleScope.CreationOnly,
                    $"Die Vorlage {origin.Profession!.Name} setzt {id} {min} voraus ({id} ist {attr[id]}). " +
                    "Wer die betroffenen Teile der Vorlage nicht übernimmt, braucht den Wert nicht.", id));
        }

        // Vor- und Nachteile
        var traits = Traits(creation, origin, sources, issues);
        var specialAbilities = SpecialAbilities(creation, sources, issues);
        int Effect(EffectTarget target) => traits.Sum(t => t.Definition.Effects?
            .Where(e => e.Target == target).Sum(e => e.PerLevel ? e.Value * t.Level : e.Value) ?? 0);
        var advantageAp = traits.Where(t => !t.Automatic && !t.Definition.IsDisadvantage).Sum(t => t.Ap ?? 0);
        var disadvantageAp = traits.Where(t => !t.Automatic && t.Definition.IsDisadvantage).Sum(t => t.Ap ?? 0);
        var advantageTotal = advantageAp + (origin.Species?.AutomaticAdvantageAp ?? 0);
        var disadvantageTotal = disadvantageAp + (origin.Species?.AutomaticDisadvantageAp ?? 0);
        if (inCreation && advantageTotal > cr.MaxAdvantageAp)
            issues.Add(new Issue(IssueSeverity.Error, "traits.advantageMax", RuleScope.CreationOnly,
                $"Vorteile kosten zusammen {advantageTotal} AP, erlaubt sind höchstens {cr.MaxAdvantageAp} AP (inkl. automatischer Vorteile der Spezies)."));
        if (inCreation && -disadvantageTotal > cr.MaxDisadvantageAp)
            issues.Add(new Issue(IssueSeverity.Error, "traits.disadvantageMax", RuleScope.CreationOnly,
                $"Nachteile bringen zusammen {-disadvantageTotal} AP, erlaubt sind höchstens {cr.MaxDisadvantageAp} AP (inkl. automatischer Nachteile der Spezies)."));

        // Paketwerte
        var packages = new PackageCosts(rules);
        var skillPackage = SkillPackage(origin);
        var combatPackage = CombatPackage(origin, creation, issues, packages);

        // Talente
        var skills = rules.Skills.Select((def, index) =>
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
            var ap = SafeCost(def.ImprovementCost, 0, value, def.Id, def.Name);
            var floor = creationState?.Skills[index].Value ?? 0; // gleiche Reihenfolge wie rules.Skills
            return new SkillState(def, value, package, max, ap, available) { Min = floor };
        }).ToList();

        // Kampftechniken
        var combatTechniques = rules.CombatTechniques.Select((def, index) =>
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
            var ap = SafeCost(def.ImprovementCost, start, value, def.Id, def.Name);
            var floor = creationState?.CombatTechniques[index].Value ?? start;
            return CombatValues(def, value, package, floor, max, ap, available, attr);
        }).ToList();

        // Kultur und Profession sind Vorlagen: ihre Werte kosten wie selbst gesteigerte (oben in Talenten/Kampftechniken).
        // Die Vorlagekosten werden nur zur Anzeige und zum Abgleich mit dem Wiki-AP-Wert berechnet.
        var cultureAp = 0;
        if (origin.CulturePackage)
        {
            var package = origin.Culture!.Package!;
            cultureAp = packages.CultureCost(package);
            if (cultureAp != package.WikiAp)
                issues.Add(new Issue(IssueSeverity.Info, "wiki.cultureAp", RuleScope.Always,
                    $"{package.Name}: Das Wiki nennt {package.WikiAp} AP, die enthaltenen Talente kosten {cultureAp} AP. " +
                    "Berechnet werden die tatsächlichen Kosten (vermutlich Fehler im Wiki).", origin.Culture.Id));
        }
        ProfessionCost? professionCost = null;
        if (origin.Profession is { } profession)
        {
            professionCost = packages.ProfessionCost(profession, origin.Variant, creation.ProfessionCombatTechniqueChoices);
            if (professionCost.WikiMismatch is { } mismatch)
                issues.Add(new Issue(IssueSeverity.Info, "wiki.professionAp", RuleScope.Always,
                    $"{profession.Name}{(origin.Variant is { } v ? $" ({v.Name})" : "")}: {mismatch} " +
                    "Berechnet werden die tatsächlichen Kosten (vermutlich Fehler im Wiki).", profession.Id));
        }
        // Voraussetzungen erst jetzt prüfen: Talent-/Kampftechnikwerte und alle Einträge sind bekannt.
        var magicTradition = Tradition(specialAbilities, AbilityDomain.Magical, issues);
        var karmaTradition = Tradition(specialAbilities, AbilityDomain.Karmal, issues);
        var spells = Spells(creation, inCreation, level, sources, attr, traits, specialAbilities, Effect, creationState, issues);
        var context = new PrerequisiteContext(traits.Concat(specialAbilities).ToList(), origin, attr,
            skills.ToDictionary(s => s.Definition.Id, s => s.Value), combatTechniques.ToDictionary(c => c.Definition.Id, c => c.Value),
            rules, magicTradition, karmaTradition) { Spells = spells };
        // Kostenlose Vorlagen-Einträge (Muttersprache, Ortskenntnis aus der Kultur) sind gegeben, keine Prüfung.
        foreach (var t in context.All.Where(t => !t.Automatic && !t.Entry.Free)) CheckPrerequisites(t, context, issues);

        // Zugekaufte LeP/AsP/KaP (Erfahrung: Kostentabelle D; höchstens KO bzw. Leiteigenschaft der Tradition)
        var energies = Energies(creation, inCreation, attr, magicTradition, karmaTradition, Effect, specialAbilities, issues);
        var account = new ApAccount(level.Ap + bonusAp, origin.Species?.Ap ?? 0, attributes.Sum(a => a.ApSpent),
            skills.Sum(s => s.ApSpent), combatTechniques.Sum(c => c.ApSpent), professionCost?.Unmodeled ?? 0,
            advantageAp, disadvantageAp, specialAbilities.Sum(t => t.Ap ?? 0))
        {
            Energies = energies.Sum(e => e.ApSpent),
            Spells = spells.Where(x => !x.Definition.IsKarmal).Sum(x => x.Ap),
            Liturgies = spells.Where(x => x.Definition.IsKarmal).Sum(x => x.Ap),
        };
        if (account.Remaining < 0)
            issues.Add(new Issue(IssueSeverity.Error, "ap.overspent", RuleScope.Always,
                $"Das AP-Konto ist um {-account.Remaining} AP überzogen."));
        else if (inCreation && account.Remaining > cr.MaxLeftoverAp)
            issues.Add(new Issue(IssueSeverity.Info, "ap.leftover", RuleScope.CreationOnly,
                $"Noch {account.Remaining} AP offen. Am Ende der Erschaffung dürfen höchstens {cr.MaxLeftoverAp} AP übrig bleiben."));

        // Zauberer und Geweihte müssen mit einer Tradition starten (Vorteile Zauberer/Geweihter).
        var astralBase = Effect(EffectTarget.AstralBase);
        var karmaBase = Effect(EffectTarget.KarmaBase);
        if (inCreation && astralBase > 0 && magicTradition is null)
            issues.Add(new Issue(IssueSeverity.Warning, "magic.noTradition", RuleScope.CreationOnly,
                "Zauberer müssen mit einer Tradition starten (Sonderfertigkeit Tradition, z. B. Tradition (Gildenmagier))."));
        if (inCreation && karmaBase > 0 && karmaTradition is null)
            issues.Add(new Issue(IssueSeverity.Warning, "karma.noTradition", RuleScope.CreationOnly,
                "Geweihte müssen mit einer Tradition starten (Sonderfertigkeit Tradition, z. B. Tradition (Praioskirche))."));

        // Basiswerte (Kodex der Helden S. 22): halbe Werte und Sechstel werden kaufmännisch gerundet.
        // AE/KE: Grundwert des Vorteils + Leiteigenschaft der Tradition ± Vor-/Nachteile (Heldenerschaffung, Schritt 12).
        var species = origin.Species;
        var derived = new DerivedValues(
            Astral: astralBase > 0 ? astralBase + PrimaryBonus(magicTradition, attr) + Effect(EffectTarget.Astral) +
                    Permanent(Characters.Energies.Astral, Characters.Energies.AstralLost, Characters.Energies.AstralRepurchased) : null,
            Karma: karmaBase > 0 ? karmaBase + PrimaryBonus(karmaTradition, attr) + Effect(EffectTarget.Karma) +
                    Permanent(Characters.Energies.Karma, Characters.Energies.KarmaLost, Characters.Energies.KarmaRepurchased) : null,
            LifePoints: species is null ? null : species.LifeBase + 2 * attr["KO"] + Effect(EffectTarget.LifePoints) + Bought(Characters.Energies.LifePoints),
            Spirit: species is null ? null : species.SpiritBase + SixthRounded(attr["MU"] + attr["KL"] + attr["IN"]) + Effect(EffectTarget.Spirit),
            Toughness: species is null ? null : species.ToughnessBase + SixthRounded(attr["KO"] + attr["KO"] + attr["KK"]) + Effect(EffectTarget.Toughness),
            Speed: species is null ? null : species.SpeedBase + Effect(EffectTarget.Speed),
            Dodge: HalfRoundUp(attr["GE"]),
            Initiative: HalfRoundUp(attr["MU"] + attr["GE"]),
            FatePoints: Math.Max(0, cr.FatePoints + Effect(EffectTarget.FatePoints)));

        return new CharacterState(level, origin, attributes, traits, specialAbilities, skills, combatTechniques, account, derived, issues)
        {
            Energies = energies,
            Equipment = Equipment(character, attr, traits, specialAbilities, combatTechniques, derived, sources, issues),
            Spells = spells,
            AdvantageApTotal = advantageTotal,
            DisadvantageApTotal = disadvantageTotal,
            CulturePackageAp = cultureAp,
            ProfessionCost = professionCost,
        };

        int Bought(string id) => energies.First(e => e.Id == id).Value;
        // AsP/KaP: zugekauft − permanent verloren + zurückgekauft
        int Permanent(string bought, string lost, string repurchased) => Bought(bought) - Bought(lost) + Bought(repurchased);

        int SafeCost(ImprovementCost cost, int from, int to, string id, string name)
        {
            if (Math.Max(from, to) <= costs.MaxValue) return costs.Cost(cost, from, to);
            issues.Add(new Issue(IssueSeverity.Error, "cost.tableMax", RuleScope.Always,
                $"{name} {to} liegt über dem Ende der Kostentabelle ({costs.MaxValue}).", id));
            return costs.Cost(cost, Math.Min(from, costs.MaxValue), Math.Min(to, costs.MaxValue));
        }
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
