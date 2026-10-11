using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Core.Rules;

/// <summary>Regelprüfungen der Tiergefährten (warnen, sperren nicht).</summary>
public sealed partial class CompanionCalculator
{
    /// <summary>Regeltext der Vertrautenbindung: andere Traditionen nur ausnahmsweise.</summary>
    public const string OtherTraditionsText =
        "Zauberer anderer Traditionen können in seltenen Fällen Vertrautentiere an sich binden (Meisterentscheid).";

    private void Check(Character hero, Companion companion, AnimalDefinition? animal, bool familiar,
        FamiliarTradition? tradition, CharacterState? heroState, IReadOnlyDictionary<string, int> attr,
        IReadOnlyList<CompanionAbility> abilities, List<Issue> issues)
    {
        void Add(IssueSeverity severity, string code, string message, string? target = null) =>
            issues.Add(new Issue(severity, code, RuleScope.Always, message, target));

        if (familiar && heroState is not null)
        {
            if (heroState.SpecialAbilities.All(t => t.Definition.Id != VertrautenbindungId))
                Add(IssueSeverity.Warning, "companion.noBinding",
                    "Ein Vertrautentier setzt die Sonderfertigkeit Vertrautenbindung voraus.");
            var usual = animal is null ? null : FamiliarRequirementText(animal.Id);
            var special = animal is not null && usual is null
                ? $"{animal.Name} ist bei keiner Tradition ein übliches Vertrautentier, nur als besonderes Vertrautentier (Meisterentscheid)."
                : null;
            if (tradition is null)
                Add(IssueSeverity.Info, "companion.noFamiliarTradition", usual is not null
                    ? $"{animal!.Name} ist übliches Vertrautentier bei {usual}. Der Held hat keine dieser Traditionen. " + OtherTraditionsText
                    : "Vertrautentiere haben üblicherweise Hexen, Geoden, Brobim-Geoden, Goblinzauberinnen und Zibiljas. " +
                      OtherTraditionsText + (special is not null ? " " + special : ""));
            else if (animal is not null && tradition.Choices.All(c => !c.Animals.Contains(animal.Id)))
                Add(IssueSeverity.Info, "companion.specialFamiliar", special ??
                    $"{animal.Name} ist kein übliches Vertrautentier dieser Tradition (besonderes Vertrautentier, Meisterentscheid). " +
                    $"Üblich bei {usual}.");
        }

        var acquired = abilities.Where(a => a.Source == CompanionAbilitySource.Acquired).ToList();
        var all = abilities.Select(a => a.Definition.Id).ToHashSet();
        var kind = animal is null ? null : rules.Familiars.Kinds.GetValueOrDefault(animal.Id);
        var loyalty = StartLoyalty(companion, animal) +
                      companion.Log.OfType<RaiseEntry>().Where(r => r.Kind == RaiseTarget.Loyalty).Sum(r => r.To - r.From);

        foreach (var a in acquired)
        {
            var d = a.Definition;
            var target = a.Entry.LogEntryId is { } id ? "log:" + id : d.Id;
            if (d.Kind == AnimalAbilityKind.FamiliarTrick)
            {
                if (!familiar)
                    Add(IssueSeverity.Warning, "companion.familiarTrick", $"{a.Name}: Vertrautentricks lernen nur Vertraute.", target);
                else if (tradition?.ExcludedTricks?.Contains(d.Id) == true)
                    Add(IssueSeverity.Warning, "companion.trickExcluded",
                        $"{a.Name} können die Vertrauten dieser Tradition nicht lernen.", target);
                else if (!FitsAnimal(d.AnimalsText, kind))
                    Add(IssueSeverity.Warning, "companion.trickAnimal",
                        $"{a.Name} ist nur für {d.AnimalsText} (Meisterentscheid).", target);
            }
            if (d.Loyalty is { } min && loyalty < min)
                Add(IssueSeverity.Warning, "companion.trickLoyalty", $"{a.Name} setzt Loyalität {min} voraus (jetzt {loyalty}).", target);
            foreach (var req in (d.RequiredTricks ?? []).Where(r => !all.Contains(r)))
                Add(IssueSeverity.Warning, "companion.trickRequires",
                    $"{a.Name} setzt den Trick {rules.FindAnimalAbility(req)?.Name ?? req} voraus.", target);
            if (d.Kind == AnimalAbilityKind.Training && animal?.Training is { } training && !training.Trainings.Contains(d.Id))
                Add(IssueSeverity.Warning, "companion.trainingAnimal",
                    $"{a.Name} ist für {animal.Name} nicht vorgesehen (Meisterentscheid).", target);
        }

        // Doppelt erworben (Kunststück u. Ä. unterscheiden sich durch die Präzisierung)
        foreach (var dup in acquired.GroupBy(a => (a.Definition.Id, a.Entry.Option, a.Entry.Specification)).Where(g => g.Count() > 1))
            Add(IssueSeverity.Warning, "companion.duplicate", $"{dup.First().Name} ist mehrfach eingetragen.", dup.Key.Id);
        foreach (var a in acquired.Where(a => abilities.Any(o => o.Source != CompanionAbilitySource.Acquired && o.Definition.Id == a.Definition.Id)))
            Add(IssueSeverity.Info, "companion.alreadyIncluded",
                $"{a.Name} hat das Tier bereits (Vertrautentier bzw. Ausbildungsaufsatz).", a.Definition.Id);

        // Ausbildungsaufsätze: einer, Reittier zusätzlich kombinierbar; Vertraute zwei
        var trainings = acquired.Where(a => a.Definition.Kind == AnimalAbilityKind.Training).ToList();
        var allowed = (familiar ? 2 : 1) + (trainings.Any(t => t.Definition.Id == ReittierId) ? 1 : 0);
        if (trainings.Count > allowed)
            Add(IssueSeverity.Warning, "companion.trainingCount",
                $"Höchstens {allowed} Ausbildungsaufs{(allowed == 1 ? "atz" : "ätze")} ({trainings.Count} eingetragen).");

        // Tricks: domestizierte Tiere zusätzlich zu den Aufsätzen bis zu KL/2, Wildtiere höchstens KL/2
        var maxTricks = attr.GetValueOrDefault("KL") / 2;
        var tricks = acquired.Count(a => a.Definition.Kind == AnimalAbilityKind.Trick);
        if (tricks > maxTricks)
            Add(IssueSeverity.Warning, "companion.trickCount", $"Höchstens KL/2 = {maxTricks} weitere Tricks ({tricks} eingetragen).");

        // Vor- und Nachteile bei der Aufnahme je höchstens 30 AP
        var advantages = acquired.Where(a => a.Definition.Kind == AnimalAbilityKind.Advantage).Sum(a => a.Ap ?? 0);
        var disadvantages = acquired.Where(a => a.Definition.Kind == AnimalAbilityKind.Disadvantage).Sum(a => a.Ap ?? 0);
        if (advantages > MaxTraitAp)
            Add(IssueSeverity.Warning, "companion.advantageAp", $"Vorteile für {advantages} AP – bei der Aufnahme höchstens {MaxTraitAp} AP.");
        if (-disadvantages > MaxTraitAp)
            Add(IssueSeverity.Warning, "companion.disadvantageAp", $"Nachteile für {-disadvantages} AP – bei der Aufnahme höchstens {MaxTraitAp} AP.");
    }

    /// <summary>„alle“, „alle (außer …)“ oder eine Liste („Eulen, Katzen, Wildkatzen“) mit der Bezeichnung der Tierart.</summary>
    public static bool FitsAnimal(string? animalsText, string? kind)
    {
        if (string.IsNullOrWhiteSpace(animalsText) || animalsText.StartsWith("alle", StringComparison.OrdinalIgnoreCase)) return true;
        if (kind is null) return false;
        return animalsText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(k => k.Equals(kind, StringComparison.OrdinalIgnoreCase));
    }
}
