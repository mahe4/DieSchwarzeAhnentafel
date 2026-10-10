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
                issues.Add(new Issue(IssueSeverity.Warning, "profession.attribute", RuleScope.CreationOnly,
                    $"Die Vorlage {origin.Profession!.Name} setzt {id} {min} voraus ({id} ist {attr[id]}). " +
                    "Wer die betroffenen Teile der Vorlage nicht übernimmt, braucht den Wert nicht.", id));
        }

        // Vor- und Nachteile
        var traits = Traits(character, origin, attr, sources, issues);
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
            var ap = SafeCost(def.ImprovementCost, start, value, def.Id, def.Name);
            return CombatValues(def, value, package, start, max, ap, available, attr);
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
        var account = new ApAccount(level.Ap, origin.Species?.Ap ?? 0, attributes.Sum(a => a.ApSpent),
            skills.Sum(s => s.ApSpent), combatTechniques.Sum(c => c.ApSpent), professionCost?.Unmodeled ?? 0,
            advantageAp, disadvantageAp);
        if (account.Remaining < 0)
            issues.Add(new Issue(IssueSeverity.Error, "ap.overspent", RuleScope.Always,
                $"Das AP-Konto ist um {-account.Remaining} AP überzogen."));
        else if (inCreation && account.Remaining > cr.MaxLeftoverAp)
            issues.Add(new Issue(IssueSeverity.Info, "ap.leftover", RuleScope.CreationOnly,
                $"Noch {account.Remaining} AP offen. Am Ende der Erschaffung dürfen höchstens {cr.MaxLeftoverAp} AP übrig bleiben."));

        // Basiswerte (Kodex der Helden S. 22): halbe Werte und Sechstel werden kaufmännisch gerundet.
        var species = origin.Species;
        var derived = new DerivedValues(
            Astral: Effect(EffectTarget.AstralBase) is > 0 and var ae ? ae + Effect(EffectTarget.Astral) : null,
            Karma: Effect(EffectTarget.KarmaBase) is > 0 and var ke ? ke + Effect(EffectTarget.Karma) : null,
            LifePoints: species is null ? null : species.LifeBase + 2 * attr["KO"] + Effect(EffectTarget.LifePoints),
            Spirit: species is null ? null : species.SpiritBase + SixthRounded(attr["MU"] + attr["KL"] + attr["IN"]) + Effect(EffectTarget.Spirit),
            Toughness: species is null ? null : species.ToughnessBase + SixthRounded(attr["KO"] + attr["KO"] + attr["KK"]) + Effect(EffectTarget.Toughness),
            Speed: species is null ? null : species.SpeedBase + Effect(EffectTarget.Speed),
            Dodge: HalfRoundUp(attr["GE"]),
            Initiative: HalfRoundUp(attr["MU"] + attr["GE"]),
            FatePoints: Math.Max(0, cr.FatePoints + Effect(EffectTarget.FatePoints)));

        return new CharacterState(level, origin, attributes, traits, skills, combatTechniques, account, derived, issues)
        {
            AdvantageApTotal = advantageTotal,
            DisadvantageApTotal = disadvantageTotal,
            CulturePackageAp = cultureAp,
            ProfessionCost = professionCost,
        };

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
                issues.Add(new Issue(IssueSeverity.Info, "profession.variant", RuleScope.CreationOnly,
                    $"{profession.Name}: Die Vorlage sieht die Wahl einer Variante vor.", profession.Id));
            if (profession.Unparsed is { Count: > 0 })
                issues.Add(new Issue(IssueSeverity.Warning, "profession.unparsed", RuleScope.CreationOnly,
                    $"{profession.Name}: Einige Angaben konnten nicht automatisch übernommen werden und müssen von Hand " +
                    $"eingetragen werden: {string.Join("; ", profession.Unparsed)}", profession.Id));
            if (profession.SpecialAbilitiesText is not null || profession.SpellsText is not null || profession.LiturgiesText is not null)
                issues.Add(new Issue(IssueSeverity.Info, "profession.textOnly", RuleScope.CreationOnly,
                    $"{profession.Name}: Sonderfertigkeiten, Sprachen, Zauber und Liturgien der Vorlage werden erst mit späteren " +
                    "Meilensteinen einzeln übernommen; bis dahin ist ihr Anteil vorläufig mit dem Wiki-Rest angesetzt.", profession.Id));
        }

        return new OriginState(species, culture, creation.CulturePackage && culture?.Package is not null,
            profession, variant, modifiers);
    }

    /// <summary>Automatische Vor-/Nachteile der Spezies und gewählte Einträge, mit Kosten und Voraussetzungsprüfung.</summary>
    private List<TraitState> Traits(Character character, OriginState origin, IReadOnlyDictionary<string, int> attr,
        SourceSelection sources, List<Issue> issues)
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

        var entries = character.Creation.Traits;
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (rules.FindTrait(entry.Id) is not { } def)
            {
                issues.Add(new Issue(IssueSeverity.Error, "traits.unknown", RuleScope.Always,
                    $"Vor-/Nachteil '{entry.Id}' ist in den Regeldaten nicht vorhanden."));
                continue;
            }
            var ap = traitRules.Cost(def, entry);
            var available = sources.IsAvailable(def.Publications);
            if (!available) issues.Add(SourceDisabled(def.Id, def.Name));
            if (ap is null)
                issues.Add(new Issue(IssueSeverity.Warning, "traits.costOpen", RuleScope.Always,
                    def.Cost.Type switch
                    {
                        TraitCostType.BySkillFactor => $"{def.Name}: {(def.Cost.Target == TraitOptionTarget.Skill ? "Fertigkeit" : "Kampftechnik")} wählen.",
                        TraitCostType.ByOption => $"{def.Name}: Ausprägung wählen.",
                        _ => $"{def.Name}: AP-Wert eintragen ({def.ApValueText.Replace('\n', ' ')}).",
                    }, TraitTarget(i)));
            result.Add(new TraitState(def, entry, i, TraitRules.Level(def, entry), ap, Automatic: false, available));
        }

        // Voraussetzungen der gewählten Einträge
        foreach (var t in result.Where(t => !t.Automatic))
            CheckPrerequisites(t, result, origin, attr, issues);
        return result;
    }

    public static string TraitTarget(int index) => "trait:" + index;

    private static string Label(TraitState t) =>
        t.Definition.Name + (t.Entry.Specification is { Length: > 0 } s ? $" ({s})" : "") +
        (t.Definition.MaxLevel is not null ? " " + Roman(t.Level) : "");

    private static string Roman(int level) => level switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", _ => "VII" };

    private static void CheckPrerequisites(TraitState t, List<TraitState> all, OriginState origin,
        IReadOnlyDictionary<string, int> attr, List<Issue> issues)
    {
        var def = t.Definition;
        var target = TraitTarget(t.Index);
        void Fail(string message) => issues.Add(new Issue(IssueSeverity.Error, "traits.prerequisite", RuleScope.Always,
            $"{Label(t)}: {message}", target));
        bool Owns(string name, bool disadvantage)
        {
            // „Unfähig für diese Fertigkeit“, „Begabung auf das gleiche Talent“: nur bei derselben Fertigkeit
            var sameOption = System.Text.RegularExpressions.Regex.Match(name,
                @"^(.+?)\s+(?:für diese Fertigkeit|auf das gleiche Talent|für die gleiche Fertigkeit)$");
            if (sameOption.Success)
                return t.Entry.Option is { } option && all.Any(o => o != t && o.Definition.IsDisadvantage == disadvantage &&
                    o.Definition.Name == sameOption.Groups[1].Value && o.Entry.Option == option);
            return all.Any(o => o != t && o.Definition.IsDisadvantage == disadvantage && NameMatches(o, name));
        }

        // Dieselbe Fertigkeit/Ausprägung zweimal ergibt keinen Sinn („nicht mehrfach in einer Fertigkeit begabt“).
        if (t.Entry.Option is { } chosen && all.Any(o => !o.Automatic && o.Index < t.Index &&
                o.Definition.Id == def.Id && o.Entry.Option == chosen))
            Fail("ist für diese Auswahl bereits vorhanden.");

        foreach (var p in def.Prerequisites ?? [])
        {
            switch (p.Type)
            {
                case PrerequisiteType.Attribute when attr.GetValueOrDefault(p.Attribute!) < p.Min:
                    Fail($"setzt {p.Attribute} {p.Min} voraus.");
                    break;
                case PrerequisiteType.RequiresAdvantage when !Owns(p.Name!, false):
                    Fail($"setzt den Vorteil {p.Name} voraus.");
                    break;
                case PrerequisiteType.RequiresDisadvantage when !Owns(p.Name!, true):
                    Fail($"setzt den Nachteil {p.Name} voraus.");
                    break;
                case PrerequisiteType.ForbidsAdvantage when Owns(p.Name!, false):
                    Fail($"ist mit dem Vorteil {p.Name} nicht vereinbar.");
                    break;
                case PrerequisiteType.ForbidsDisadvantage when Owns(p.Name!, true):
                    Fail($"ist mit dem Nachteil {p.Name} nicht vereinbar.");
                    break;
                case PrerequisiteType.Species when origin.Species is { } s && !p.Names!.Any(n => SameSpecies(n, s.Name)):
                    Fail($"nur für Spezies {string.Join(" oder ", p.Names!)}.");
                    break;
                case PrerequisiteType.ForbidsSpecies when origin.Species is { } s && p.Names!.Any(n => SameSpecies(n, s.Name)):
                    Fail($"nicht für Spezies {s.Name}.");
                    break;
                case PrerequisiteType.OriginRecommends:
                    var texts = new[]
                    {
                        origin.Species?.AutomaticAdvantagesText, origin.Species?.RecommendedText, origin.Species?.TypicalAdvantagesText,
                        origin.Culture?.TypicalAdvantagesText, origin.Profession?.RecommendedAdvantagesText,
                    };
                    if (!texts.Any(x => x?.Contains(p.Name!, StringComparison.CurrentCultureIgnoreCase) == true))
                        issues.Add(new Issue(IssueSeverity.Warning, "traits.prerequisite", RuleScope.Always,
                            $"{Label(t)}: Spezies, Kultur oder Profession müssen {p.Name} als automatischen oder empfohlenen Vorteil aufweisen.",
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
                case PrerequisiteType.RequiresSpecialAbility:
                    issues.Add(new Issue(IssueSeverity.Info, "traits.prerequisiteLater", RuleScope.Always,
                        $"{Label(t)}: setzt die Sonderfertigkeit {p.Name} voraus (Sonderfertigkeiten werden noch nicht geprüft).",
                        target));
                    break;
            }
        }
        if (def.UnparsedPrerequisites is { Count: > 0 } u)
            issues.Add(new Issue(IssueSeverity.Info, "traits.prerequisiteManual", RuleScope.Always,
                $"{Label(t)}: Voraussetzung bitte von Hand prüfen: {string.Join("; ", u)}.", target));
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
        return rest.Contains(spec, StringComparison.CurrentCultureIgnoreCase) && spec.Length > 0
               || rest.StartsWith(Roman(owned.Level));
    }

    /// <summary>„Elfen“/„Elf“, „Zwerg“/„Zwerge“, „Nachalben“ (Tippfehler im Wiki)/„Nachtalb“ – grob über den Wortstamm.</summary>
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
