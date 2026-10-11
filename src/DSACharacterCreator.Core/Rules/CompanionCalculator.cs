using System.Text.RegularExpressions;
using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Core.Rules;

/// <summary>
/// Berechnet Vertrautentiere und tierische Begleiter als reine Funktion von Tierart und Protokoll (Kodex des Schwertes
/// S. 153–158, Grundregeln › Vertrautentiere). AP: ein Viertel der AP-Gutschriften des Helden seit der Bindung bzw.
/// Aufnahme (über die Summe abgerundet) plus Bonus-AP im Protokoll des Tiers. Gesteigert wird wie bei Helden;
/// Angriffe und VW in Spalte C, Loyalität in Spalte B mit den AP des Helden.
/// </summary>
public sealed partial class CompanionCalculator(RuleData rules)
{
    private RuleData Rules => rules;

    /// <summary>Anteil der Helden-AP: ein Viertel (Vertraute und tierische Begleiter).</summary>
    public const int ApShareDivisor = 4;

    /// <summary>Startwert der Loyalität: domestizierte Tiere (und Vertraute) 4, Wildtiere 0.</summary>
    public const int DomesticLoyalty = 4;

    /// <summary>Höchstens so viele AP für Vor- bzw. Nachteile bei der Aufnahme eines Tiers.</summary>
    public const int MaxTraitAp = 30;

    private const ImprovementCost AttributeCost = ImprovementCost.E;
    private const ImprovementCost CombatCost = ImprovementCost.C;
    private const ImprovementCost LoyaltyCost = ImprovementCost.B;
    private const ImprovementCost EnergyCost = ImprovementCost.D;
    private const string VertrautenbindungId = "sf.vertrautenbindung";
    private const string ReittierId = "animal-training.reittier";

    /// <summary>Alle Tiere des Helden; prüft zusätzlich, dass höchstens ein lebender Vertrauter gebunden ist.</summary>
    public IReadOnlyList<CompanionState> CalculateAll(Character hero, CharacterState? heroState)
    {
        var result = hero.Companions.Select(c => Calculate(hero, c, heroState)).ToList();
        var living = result.Where(s => s.Companion.Kind == CompanionKind.Familiar && !s.Companion.Deceased).ToList();
        if (living.Count > 1)
            result = result.Select(s => living.Contains(s)
                ? s with { Issues = [.. s.Issues, new Issue(IssueSeverity.Warning, "companion.familiarCount", RuleScope.Always,
                    "Eine Zauberin kann nicht mehrere Vertrautentiere an sich binden.")] }
                : s).ToList();
        return result;
    }

    /// <summary>AP des Helden für die Loyalität aller Tiere (Loyalität steigert die Heldin, nicht das Tier).</summary>
    public int LoyaltyAp(Character hero) => hero.Companions.Sum(LoyaltyAp);

    public int LoyaltyAp(Companion companion)
    {
        var value = StartLoyalty(companion, rules.FindAnimal(companion.Animal));
        var ap = 0;
        foreach (var r in companion.Log.OfType<RaiseEntry>().Where(r => r.Kind == RaiseTarget.Loyalty))
        {
            var to = value + (r.To - r.From);
            ap += SafeCost(LoyaltyCost, value, to);
            value = to;
        }
        return ap;
    }

    public CompanionState Calculate(Character hero, Companion companion, CharacterState? heroState)
    {
        var issues = new List<Issue>();
        var animal = rules.FindAnimal(companion.Animal);
        var familiar = companion.Kind == CompanionKind.Familiar;
        var tradition = Tradition(heroState);
        if (companion.Animal is null)
            issues.Add(new Issue(IssueSeverity.Info, "companion.noAnimal", RuleScope.Always, "Noch keine Tierart gewählt."));
        else if (animal is null)
            issues.Add(new Issue(IssueSeverity.Error, "companion.unknownAnimal", RuleScope.Always,
                $"Unbekannte Tierart '{companion.Animal}'."));

        // Protokoll abspielen
        var replay = new Replay(this, companion, animal, familiar);
        var entries = new List<CompanionEntryState>();
        var bonusAp = 0;
        foreach (var entry in companion.Log)
        {
            var (text, ap, heroAp) = replay.Apply(entry, ref bonusAp, issues);
            entries.Add(new CompanionEntryState(entry, text, ap, heroAp));
        }

        var abilities = replay.Abilities(tradition);
        var attributes = rules.Attributes.Select(def =>
        {
            var baseValue = animal?.Attributes.GetValueOrDefault(def.Id) ?? 0;
            var bonus = replay.AttributeBonus(def.Id);
            return new CompanionAttribute(def, baseValue + bonus + replay.Raised(RaiseTarget.Attribute, def.Id), baseValue, bonus);
        }).ToList();
        var attr = attributes.ToDictionary(a => a.Definition.Id, a => a.Value);

        var skills = Skills(animal, replay);
        var attacks = (animal?.Attacks ?? []).Select(a =>
        {
            var at = a.Attack is { } v ? v + replay.Bonus("AT") + replay.Raised(RaiseTarget.Attack, a.Name) : (int?)null;
            return new CompanionAttack(a.Name, at, a.Ranged, AddDamage(a.Damage, replay.Bonus("TP")), a.Reach, a.Text);
        }).ToList();

        var values = Values(animal, familiar, replay, attr);
        var loyalty = new CompanionLoyalty(StartLoyalty(companion, animal) + replay.Raised(RaiseTarget.Loyalty, ""),
            StartLoyalty(companion, animal), replay.LoyaltyAp);

        var awards = HeroAwards(hero, companion, issues);
        var account = new CompanionAp(awards, awards / ApShareDivisor, bonusAp, entries.Where(e => !e.HeroAp).Sum(e => e.Ap));
        if (account.Remaining < 0)
            issues.Add(new Issue(IssueSeverity.Warning, "companion.overspent", RuleScope.Always,
                $"Das AP-Konto von {Name(companion, animal)} ist um {-account.Remaining} AP überzogen."));

        Check(hero, companion, animal, familiar, tradition, heroState, attr, abilities, issues);
        if (replay.EnergyBought(Energies.LifePoints) > attr.GetValueOrDefault("KO"))
            issues.Add(new Issue(IssueSeverity.Warning, "companion.energyMax", RuleScope.Always,
                $"Höchstens {attr.GetValueOrDefault("KO")} LeP (KO) zukaufen.", "energy:" + Energies.LifePoints));

        return new CompanionState(companion, animal, tradition, attributes, values, attacks, skills, loyalty, abilities, account,
            entries, issues);
    }

    private static string Name(Companion c, AnimalDefinition? a) =>
        c.Name.Length > 0 ? c.Name : a?.Name ?? "dem Tier";

    private static int StartLoyalty(Companion companion, AnimalDefinition? animal) =>
        companion.Kind == CompanionKind.Familiar || animal?.Domesticated == true ? DomesticLoyalty : 0;

    /// <summary>Tradition des Helden mit Vertrauten (Hexen, Geoden …); <c>null</c>, wenn keine passt.</summary>
    private FamiliarTradition? Tradition(CharacterState? heroState) =>
        heroState?.SpecialAbilities.Select(t => t.Definition.Id)
            .Select(id => rules.Familiars.Traditions.FirstOrDefault(f => f.Tradition == id))
            .FirstOrDefault(f => f is not null);

    /// <summary>
    /// Traditionen, bei denen die Tierart ein übliches Vertrautentier ist, mit Ausprägung
    /// („Tradition (Hexen) mit Schwesternschaft Töchter der Erde“); leer, wenn es nur als besonderes Vertrautentier geht.
    /// </summary>
    public IReadOnlyList<string> FamiliarRequirements(string animalId) =>
        rules.Familiars.Traditions.SelectMany(t => t.Choices.Where(c => c.Animals.Contains(animalId))
                .Select(c => TraditionName(t) + (c.Name is { } n ? " mit " + ChoiceName(t, n) : "")))
            .Distinct().ToList();

    /// <summary>Die Anforderungen als Satzteil: „A oder B“.</summary>
    public string? FamiliarRequirementText(string animalId) =>
        FamiliarRequirements(animalId) is { Count: > 0 } r ? string.Join(" oder ", r) : null;

    private string TraditionName(FamiliarTradition t) => rules.FindSpecialAbility(t.Tradition)?.Name ?? t.Tradition;

    private static string ChoiceName(FamiliarTradition t, string name) =>
        t.ChoiceLabel is { } label && !name.StartsWith(label, StringComparison.Ordinal) ? $"{label} {name}" : name;

    /// <summary>Summe der AP-Gutschriften des Helden nach <see cref="Companion.BoundAfter"/>.</summary>
    private static int HeroAwards(Character hero, Companion companion, List<Issue> issues)
    {
        var counting = companion.BoundAfter is null;
        if (!counting && hero.Log.All(e => e.Id != companion.BoundAfter))
        {
            issues.Add(new Issue(IssueSeverity.Warning, "companion.boundAfterMissing", RuleScope.Always,
                "Der Protokolleintrag des Helden, ab dem das Tier AP bekommt, fehlt. Es zählen alle Gutschriften."));
            counting = true;
        }
        var sum = 0;
        foreach (var e in hero.Log)
        {
            if (!counting)
            {
                counting = e.Id == companion.BoundAfter;
                continue;
            }
            if (e is ApAwardEntry award) sum += award.Amount;
        }
        return Math.Max(0, sum);
    }

    private List<CompanionSkill> Skills(AnimalDefinition? animal, Replay replay)
    {
        var baseSkills = (animal?.Skills ?? []).GroupBy(s => s.Skill).ToDictionary(g => g.Key, g => g.First());
        var ids = baseSkills.Keys.Concat(replay.SkillTargets()).Distinct();
        return ids.Select(id => rules.FindSkill(id) is { } def ? def : null).OfType<SkillDefinition>()
            .Select(def =>
            {
                var b = baseSkills.GetValueOrDefault(def.Id);
                var baseValue = b?.Value ?? 0;
                var bonus = replay.Bonus(def.Id);
                return new CompanionSkill(def, baseValue + bonus + replay.Raised(RaiseTarget.Skill, def.Id), baseValue, bonus,
                    b?.Note, b is not null && b.Value is null);
            })
            .OrderBy(s => s.Definition.Name, StringComparer.CurrentCulture)
            .ToList();
    }

    private CompanionValues Values(AnimalDefinition? animal, bool familiar, Replay replay, IReadOnlyDictionary<string, int> attr)
    {
        if (animal is null) return new CompanionValues(null, null, null, null, null, null, null, null, null, null, null, null);
        var fb = rules.Familiars.Bonuses;
        int F(int value) => familiar ? value : 0;
        int? Percent(int? baseValue, string target) =>
            baseValue is { } b ? b + (int)Math.Round(b * replay.PercentBonus(target) / 100.0, MidpointRounding.AwayFromZero) : null;

        var life = Percent(animal.LifePoints, "LeP") + F(fb.LifePoints) + replay.Bonus("LeP") + replay.EnergyBought(Energies.LifePoints);
        int? astral = familiar || animal.AstralEnergy is not null
            ? (animal.AstralEnergy ?? 0) + F(fb.AstralEnergy) + replay.EnergyBought(Energies.Astral)
            : null;
        return new CompanionValues(
            LifePoints: life,
            Astral: astral,
            Initiative: animal.Initiative,
            Defense: animal.Defense + replay.Bonus("VW") + replay.Raised(RaiseTarget.Defense, ""),
            Dodge: animal.Dodge,
            Parry: animal.Parry,
            Spirit: animal.Spirit + F(fb.Spirit),
            Toughness: animal.Toughness + F(fb.Toughness),
            Speed: Percent(animal.Speed, "GS") + replay.Bonus("GS"),
            SpeedText: animal.SpeedText,
            Protection: (animal.Protection ?? 0) + F(fb.Protection),
            Encumbrance: animal.Encumbrance);
    }

    /// <summary>„1W6+2“ + 1 → „1W6+3“; andere Angaben bekommen den Bonus angehängt.</summary>
    private static string? AddDamage(string? damage, int bonus)
    {
        if (damage is null || bonus == 0) return damage;
        var m = DamagePattern().Match(damage);
        if (!m.Success) return $"{damage} +{bonus}";
        var total = (m.Groups[3].Success ? int.Parse(m.Groups[3].Value.Replace("–", "-")) : 0) + bonus;
        var sign = total switch { > 0 => $"+{total}", < 0 => $"−{-total}", _ => "" };
        return $"{m.Groups[1].Value}W{m.Groups[2].Value}{sign}{m.Groups[4].Value}";
    }

    [GeneratedRegex(@"^(\d+)W(\d+)([+\-–]\d+)?(.*)$")]
    private static partial Regex DamagePattern();

    private int SafeCost(ImprovementCost cost, int from, int to)
    {
        var max = rules.Costs.MaxValue;
        return rules.Costs.Cost(cost, Math.Clamp(from, 0, max), Math.Clamp(to, 0, max));
    }

    /// <summary>Kosten einer Fähigkeit; ein eigener AP-Wert geht immer vor (Meisterentscheid, ältere Regelstände).</summary>
    internal static int? AbilityAp(AnimalAbilityDefinition def, TraitEntry entry)
    {
        if (entry.ManualAp is { } manual) return manual;
        var level = Math.Max(1, entry.Level ?? 1);
        var c = def.Cost;
        return c.Type switch
        {
            TraitCostType.Fixed => c.Ap,
            TraitCostType.PerLevel => c.Ap * level,
            TraitCostType.Levels => c.Levels?.Take(level).Sum(),
            TraitCostType.ByOption => c.Options?.FirstOrDefault(o => o.Name == entry.Option)?.Ap,
            _ => null,
        };
    }

    /// <summary>Spielt das Protokoll eines Tiers ab und merkt sich Steigerungen, Fähigkeiten und Boni.</summary>
    private sealed class Replay(CompanionCalculator calc, Companion companion, AnimalDefinition? animal, bool familiar)
    {
        private readonly RuleData _rules = calc.Rules;
        private readonly Dictionary<(RaiseTarget, string), int> _raised = [];
        private readonly List<TraitEntry> _acquired = [];
        private int _loyalty = StartLoyalty(companion, animal);

        public int LoyaltyAp { get; private set; }

        public int Raised(RaiseTarget kind, string target) => _raised.GetValueOrDefault((kind, target));

        public int EnergyBought(string id) => Raised(RaiseTarget.Energy, id);

        public IEnumerable<string> SkillTargets() =>
            _raised.Keys.Where(k => k.Item1 == RaiseTarget.Skill).Select(k => k.Item2)
                .Concat(Bonuses().Where(b => b.Target.StartsWith("skill.", StringComparison.Ordinal)).Select(b => b.Target));

        /// <summary>Boni aus Ausbildungsaufsätzen und Tiervorteilen (bei „A- oder B-FW“ die gewählte Option).</summary>
        private IEnumerable<AnimalBonus> Bonuses() =>
            _acquired.Select(e => (Entry: e, Def: _rules.FindAnimalAbility(e.Id)))
                .Where(x => x.Def?.Bonuses is not null)
                .SelectMany(x => x.Def!.Bonuses!.Select(b => b.Options is { Count: > 0 } options
                    ? b with { Target = options.Contains(x.Entry.Option ?? "") ? x.Entry.Option! : options[0] }
                    : b));

        public int Bonus(string target) => Bonuses().Where(b => b.Target == target && !b.Percent).Sum(b => b.Value);

        public int PercentBonus(string target) => Bonuses().Where(b => b.Target == target && b.Percent).Sum(b => b.Value);

        public int AttributeBonus(string id) =>
            Bonus(id) + (familiar ? _rules.Familiars.Bonuses.Attributes : 0);

        private int Current(RaiseTarget kind, string target) => kind switch
        {
            RaiseTarget.Attribute => (animal?.Attributes.GetValueOrDefault(target) ?? 0) + AttributeBonus(target) + Raised(kind, target),
            RaiseTarget.Skill => (animal?.Skills?.FirstOrDefault(s => s.Skill == target)?.Value ?? 0) + Bonus(target) + Raised(kind, target),
            RaiseTarget.Attack => (animal?.Attacks.FirstOrDefault(a => a.Name == target)?.Attack ?? 0) + Bonus("AT") + Raised(kind, target),
            RaiseTarget.Defense => (animal?.Defense ?? 0) + Bonus("VW") + Raised(kind, ""),
            RaiseTarget.Loyalty => _loyalty,
            _ => Raised(kind, target),
        };

        public (string Text, int Ap, bool HeroAp) Apply(LogEntry entry, ref int bonusAp, List<Issue> issues)
        {
            switch (entry)
            {
                case ApAwardEntry award:
                    bonusAp += award.Amount;
                    return ($"Bonus-AP {award.Amount:+0;−0}" + (award.Note is { Length: > 0 } n ? $" ({n})" : ""), 0, false);
                case RaiseEntry raise:
                    return ApplyRaise(raise, issues);
                case AcquireEntry { Kind: EntryKind.AnimalAbility } acquire:
                {
                    var def = _rules.FindAnimalAbility(acquire.Entry.Id);
                    var entryCopy = acquire.Entry.Clone();
                    entryCopy.LogEntryId = acquire.Id;
                    _acquired.Add(entryCopy);
                    if (def is null)
                    {
                        issues.Add(new Issue(IssueSeverity.Error, "companion.unknownAbility", RuleScope.Always,
                            $"Unbekannter Eintrag '{acquire.Entry.Id}'.", "log:" + acquire.Id));
                        return (acquire.Entry.Id, 0, false);
                    }
                    var ap = acquire.Entry.Free ? 0 : AbilityAp(def, acquire.Entry);
                    if (ap is null)
                        issues.Add(new Issue(IssueSeverity.Warning, "companion.abilityNoAp", RuleScope.Always,
                            $"{def.Name}: AP-Wert eintragen (das Wiki nennt keinen festen Wert).", "log:" + acquire.Id));
                    return ($"{KindName(def.Kind)}: {def.Name}{LevelText(def, acquire.Entry)}", ap ?? 0, false);
                }
                case RemoveEntry { Kind: EntryKind.AnimalAbility } remove:
                {
                    var index = _acquired.FindLastIndex(e => e.Id == remove.EntryId && e.Option == remove.Option);
                    var def = _rules.FindAnimalAbility(remove.EntryId);
                    if (index < 0 || def is null)
                    {
                        issues.Add(new Issue(IssueSeverity.Warning, "companion.removeMissing", RuleScope.Always,
                            $"Ablegen: '{def?.Name ?? remove.EntryId}' ist nicht vorhanden.", "log:" + remove.Id));
                        return ($"Ablegen: {def?.Name ?? remove.EntryId}", 0, false);
                    }
                    var removed = _acquired[index];
                    _acquired.RemoveAt(index);
                    // Ablegen gibt die Kosten zurück bzw. kostet bei Nachteilen deren AP-Wert.
                    var ap = removed.Free ? 0 : -(AbilityAp(def, removed) ?? 0);
                    return ($"Ablegen: {def.Name}", ap, false);
                }
                case NoteEntry note:
                    return (note.Text, 0, false);
                default:
                    issues.Add(new Issue(IssueSeverity.Warning, "companion.entryType", RuleScope.Always,
                        $"Eintrag „{entry.GetType().Name}“ gilt nicht für Tiere.", "log:" + entry.Id));
                    return (entry.GetType().Name, 0, false);
            }
        }

        private (string, int, bool) ApplyRaise(RaiseEntry raise, List<Issue> issues)
        {
            var steps = raise.To - raise.From;
            var key = (raise.Kind, raise.Kind is RaiseTarget.Defense or RaiseTarget.Loyalty ? "" : raise.Target);
            var from = Current(raise.Kind, key.Item2);
            var to = from + steps;
            var (name, cost) = raise.Kind switch
            {
                RaiseTarget.Attribute => (_rules.Attributes.FirstOrDefault(a => a.Id == raise.Target)?.Name ?? raise.Target, AttributeCost),
                RaiseTarget.Skill => (_rules.FindSkill(raise.Target)?.Name ?? raise.Target,
                    _rules.FindSkill(raise.Target)?.ImprovementCost ?? ImprovementCost.B),
                RaiseTarget.Attack => ($"{raise.Target} (AT)", CombatCost),
                RaiseTarget.Defense => ("Verteidigung (VW)", CombatCost),
                RaiseTarget.Loyalty => ("Loyalität", LoyaltyCost),
                RaiseTarget.Energy => (raise.Target == Energies.Astral ? "Astralpunkte" : "Lebenspunkte", EnergyCost),
                _ => (raise.Target, ImprovementCost.B),
            };
            if (raise.Kind == RaiseTarget.Energy)
            {
                // Zukauf: Kostentabelle D nach Anzahl der gekauften Punkte
                var bought = Raised(raise.Kind, raise.Target);
                _raised[key] = bought + steps;
                return ($"{name} +{steps}", calc.SafeCost(cost, bought, bought + steps), false);
            }
            if (raise.Kind == RaiseTarget.Attack && animal?.Attacks.All(a => a.Name != raise.Target) == true)
                issues.Add(new Issue(IssueSeverity.Warning, "companion.unknownAttack", RuleScope.Always,
                    $"Die Tierart hat keinen Angriff „{raise.Target}“.", "log:" + raise.Id));
            if (raise.Kind == RaiseTarget.Spell || raise.Kind == RaiseTarget.CombatTechnique)
                issues.Add(new Issue(IssueSeverity.Warning, "companion.raiseKind", RuleScope.Always,
                    $"„{name}“ lässt sich bei Tieren nicht steigern.", "log:" + raise.Id));
            if (raise.Kind == RaiseTarget.Defense && familiar)
                issues.Add(new Issue(IssueSeverity.Info, "companion.familiarDefense", RuleScope.Always,
                    "Vertraute steigern laut Regel Kampftechniken (Angriffe), die VW nennt die Regel nur für tierische Begleiter.",
                    "log:" + raise.Id));
            var ap = calc.SafeCost(cost, from, to);
            _raised[key] = Raised(key.Kind, key.Item2) + steps;
            if (raise.Kind == RaiseTarget.Loyalty)
            {
                _loyalty = to;
                LoyaltyAp += ap;
                return ($"{name} {from} → {to} (AP des Helden)", ap, true);
            }
            return ($"{name} {from} → {to}", ap, false);
        }

        /// <summary>Fähigkeiten nach dem Abspielen: kostenlose Vertrautentricks, Tricks aus Ausbildungsaufsätzen, erworbene.</summary>
        public List<CompanionAbility> Abilities(FamiliarTradition? tradition)
        {
            var result = new List<CompanionAbility>();
            string Display(AnimalAbilityDefinition d) => tradition?.TrickNames?.GetValueOrDefault(d.Id) ?? d.Name;
            if (familiar)
            {
                foreach (var id in _rules.Familiars.FreeTricks)
                    if (_rules.FindAnimalAbility(id) is { } d)
                        result.Add(new CompanionAbility(d, new TraitEntry { Id = id, Free = true }, Display(d), 1, 0,
                            CompanionAbilitySource.Familiar));
            }
            foreach (var e in _acquired)
            {
                if (_rules.FindAnimalAbility(e.Id) is not { } d) continue;
                result.Add(new CompanionAbility(d, e, Display(d), Math.Max(1, e.Level ?? 1), e.Free ? 0 : AbilityAp(d, e),
                    CompanionAbilitySource.Acquired));
                foreach (var trick in d.Tricks ?? [])
                    if (_rules.FindAnimalAbility(trick) is { } t)
                        result.Add(new CompanionAbility(t, new TraitEntry { Id = trick, Free = true }, Display(t), 1, 0,
                            CompanionAbilitySource.Training) { From = d });
            }
            return result;
        }

        private static string KindName(AnimalAbilityKind kind) => kind switch
        {
            AnimalAbilityKind.Trick => "Trick",
            AnimalAbilityKind.Training => "Ausbildungsaufsatz",
            AnimalAbilityKind.FamiliarTrick => "Vertrautentrick",
            AnimalAbilityKind.SpecialAbility => "Sonderfertigkeit",
            AnimalAbilityKind.CombatSpecialAbility => "Kampfsonderfertigkeit",
            AnimalAbilityKind.Advantage => "Vorteil",
            AnimalAbilityKind.Disadvantage => "Nachteil",
            _ => "Eintrag",
        };

        private static string LevelText(AnimalAbilityDefinition def, TraitEntry e) =>
            (def.MaxLevel is > 1 ? $" {Math.Max(1, e.Level ?? 1)}" : "") + (e.Option is { Length: > 0 } o ? $" ({o})" : "");
    }
}
