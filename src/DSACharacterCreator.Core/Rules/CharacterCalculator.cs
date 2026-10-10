using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Core.Rules;

/// <summary>Berechnet den Stand eines Helden als reine Funktion seiner Entscheidungen (PLAN.md Abschnitt 4).</summary>
public sealed class CharacterCalculator(RuleData rules)
{
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
            var ap = SafeCost(def.ImprovementCost, 0, value, def.Id, def.Name);
            var floor = creationState?.Skills.First(x => x.Definition.Id == def.Id).Value ?? 0;
            return new SkillState(def, value, package, max, ap, available) { Min = floor };
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
            var ap = SafeCost(def.ImprovementCost, start, value, def.Id, def.Name);
            var floor = creationState?.CombatTechniques.First(x => x.Definition.Id == def.Id).Value ?? start;
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
                var ruleMax = def.Check is { } check ? check.Max(a => attr[a]) + rules.Creation.SkillMaxAboveCheckAttribute : (int?)null;
                // ohne Merkmals- bzw. Aspektkenntnis höchstens 14 (Erfahrung: Maximalwerte)
                var knowledge = Knows(def);
                if (!knowledge) ruleMax = Math.Min(ruleMax ?? 14, 14);
                max = inCreation && ruleMax is { } m ? Math.Min(m, level.MaxSkill) : ruleMax;
                if (ruleMax is { } rm && value > rm)
                    issues.Add(new Issue(IssueSeverity.Error, "spell.max", RuleScope.Always,
                        $"{def.Name} {value} liegt über dem Höchstwert {rm}" +
                        (knowledge ? "." : $" (ohne {(def.IsKarmal ? "Aspektkenntnis" : "Merkmalskenntnis")} höchstens 14)."), target));
                if (inCreation && value > level.MaxSkill)
                    issues.Add(new Issue(IssueSeverity.Error, "spell.creationMax", RuleScope.CreationOnly,
                        $"{def.Name} {value} liegt über dem Höchstwert {level.MaxSkill} ({level.Name}).", target));
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

    /// <summary>
    /// Zugekaufte Energien (Wiki, Erfahrung: Kostentabelle D; höchstens KO bzw. Leiteigenschaft). In der Erschaffung ist
    /// kein Zukauf vorgesehen (Heldenerschaffung, Schritt 8).
    /// </summary>
    private List<EnergyState> Energies(CreationLayer creation, bool inCreation, IReadOnlyDictionary<string, int> attr,
        TraditionInfo? magic, TraditionInfo? karma, Func<EffectTarget, int> effect, List<TraitState> specialAbilities,
        List<Issue> issues)
    {
        var result = new List<EnergyState>();
        int Stored(string id) => creation.BoughtEnergies.GetValueOrDefault(id);
        var hasAstral = effect(EffectTarget.AstralBase) > 0;
        var hasKarma = effect(EffectTarget.KarmaBase) > 0;

        // Permanente Verluste: Bindungen von Traditionsartefakten plus sonstige (von Hand eingetragen)
        var bindings = specialAbilities.Sum(t => TraitRules.PermanentAstral(t.Definition, t.Entry));
        var astralLost = bindings + Stored(Characters.Energies.AstralLost);
        var karmaLost = Stored(Characters.Energies.KarmaLost);
        result.Add(new EnergyState(Characters.Energies.AstralLost, "Permanent verlorene AsP", astralLost, hasAstral, null, 0,
            EnergyKind.Lost) { Fixed = bindings });
        result.Add(new EnergyState(Characters.Energies.KarmaLost, "Permanent verlorene KaP", karmaLost, hasKarma, null, 0, EnergyKind.Lost));

        // Rückkauf: 2 AP je Punkt, höchstens so viele wie verloren (auch schon bei der Erschaffung, z. B. bei Bindungen)
        void Repurchase(string id, string name, int lost)
        {
            var value = Stored(id);
            if (value > lost)
                issues.Add(new Issue(IssueSeverity.Error, "energy.repurchaseMax", RuleScope.Always,
                    $"Es sind nur {lost} {name} permanent verloren.", "energy:" + id));
            result.Add(new EnergyState(id, name + " zurückkaufen", value, lost > 0 || value > 0, lost,
                value * PackageCosts.AstralRepurchaseAp, EnergyKind.Repurchase));
        }
        Repurchase(Characters.Energies.AstralRepurchased, "AsP", astralLost);
        Repurchase(Characters.Energies.KarmaRepurchased, "KaP", karmaLost);

        // Zukauf (Kostentabelle D) nach der Erschaffung; vorher alle verlorenen Punkte zurückkaufen
        void Buy(string id, string name, bool available, int? max, int openLoss)
        {
            var value = Stored(id);
            var ap = rules.Costs.Cost(ImprovementCost.D, 0, Math.Min(value, rules.Costs.MaxValue));
            if (value > 0 && inCreation)
                issues.Add(new Issue(IssueSeverity.Error, "energy.creation", RuleScope.CreationOnly,
                    $"{name} lassen sich erst nach der Erschaffung zukaufen.", "energy:" + id));
            if (value > 0 && !available)
                issues.Add(new Issue(IssueSeverity.Error, "energy.unavailable", RuleScope.Always,
                    $"{name} kann nur zukaufen, wer über diese Energie verfügt.", "energy:" + id));
            else if (max is { } m && value > m)
                issues.Add(new Issue(IssueSeverity.Error, "energy.max", RuleScope.Always,
                    $"Höchstens {m} {name} zukaufen.", "energy:" + id));
            if (value > 0 && openLoss > 0)
                issues.Add(new Issue(IssueSeverity.Error, "energy.repurchaseFirst", RuleScope.Always,
                    $"Vor dem Zukauf erst die {openLoss} permanent verlorenen Punkte zurückkaufen.", "energy:" + id));
            result.Add(new EnergyState(id, name, value, available, max, ap, EnergyKind.Bought));
        }
        Buy(Characters.Energies.LifePoints, "Lebenspunkte", true, attr["KO"], 0);
        Buy(Characters.Energies.Astral, "Astralpunkte", hasAstral, magic?.PrimaryAttribute is { } ma ? attr[ma] : null,
            Math.Max(0, astralLost - Stored(Characters.Energies.AstralRepurchased)));
        Buy(Characters.Energies.Karma, "Karmapunkte", hasKarma, karma?.PrimaryAttribute is { } ka ? attr[ka] : null,
            Math.Max(0, karmaLost - Stored(Characters.Energies.KarmaRepurchased)));
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

        // Dieselbe Auswahl zweimal ergibt keinen Sinn – außer bei Fertigkeitsspezialisierungen (n-te Spezialisierung).
        if (t.Entry.Option is { } chosen && !def.Cost.Escalating && all.Any(o => !o.Automatic && o.Index < t.Index &&
                o.Definition.Id == def.Id && o.Entry.Option == chosen))
            Fail("ist für diese Auswahl bereits vorhanden.");
        // Fertigkeitsspezialisierung: n-te Spezialisierung desselben Talents braucht FW 6 × n.
        if (def.Cost.Escalating && def.OptionTarget == TraitOptionTarget.Skill && t.Entry.Option is { } skill)
        {
            var n = 1 + all.Count(o => !o.Automatic && o.Index < t.Index && o.Definition.Id == def.Id && o.Entry.Option == skill);
            if (ctx.Skills.GetValueOrDefault(skill) < 6 * n)
                Fail($"{n}. Spezialisierung setzt {ctx.Rules.Skills.First(x => x.Id == skill).Name} {6 * n} voraus.");
        }
        if (def.OptionTarget == TraitOptionTarget.Language && t.Entry.Option is { } lang &&
            ctx.Rules.Languages.FirstOrDefault(l => l.Id == lang) is { MaxLevel: { } maxLanguage } language && t.Level > maxLanguage)
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
                case PrerequisiteType.MaxCount when p.Count is { } max &&
                        all.Count(o => o.Definition.Id == def.Id && (o.Automatic || o.Index <= t.Index)) > max:
                    Fail($"darf höchstens {max}-mal gewählt werden (jeweils für eine andere Fertigkeit).");
                    break;
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
        string SkillName(string id) => ctx.Rules.Skills.FirstOrDefault(s => s.Id == id)?.Name ?? ctx.Rules.FindSpell(id)?.Name ?? id;
        string CombatName(string id) => ctx.Rules.CombatTechniques.FirstOrDefault(s => s.Id == id)?.Name ?? id;

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
