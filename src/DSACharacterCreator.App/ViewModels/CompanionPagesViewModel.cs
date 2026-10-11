using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.ViewModels;

/// <summary>Bezeichnung und Wert (Basiswerte, Eigenschaften).</summary>
public sealed record LabeledValue(string Label, string Value);

/// <summary>Angriff im Ergebnis.</summary>
public sealed record AttackLine(string Name, string Attack, string Damage, string Reach);

/// <summary>Tab „Ergebnis“: alle Werte des Tiers wie im Tierkasten.</summary>
public sealed partial class CompanionResultPageViewModel(CompanionEditorViewModel owner) : PageViewModel
{
    public CompanionEditorViewModel Owner { get; } = owner;

    [ObservableProperty]
    public partial IReadOnlyList<LabeledValue> Attributes { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<LabeledValue> BaseValues { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<AttackLine> Attacks { get; private set; } = [];

    [ObservableProperty]
    public partial string AttributeNote { get; private set; } = "";

    [ObservableProperty]
    public partial string ActionsText { get; private set; } = "";

    [ObservableProperty]
    public partial string SkillsText { get; private set; } = "";

    [ObservableProperty]
    public partial string TricksTitle { get; private set; } = "";

    [ObservableProperty]
    public partial string TricksText { get; private set; } = "";

    [ObservableProperty]
    public partial string TrainingText { get; private set; } = "";

    [ObservableProperty]
    public partial string TraitsText { get; private set; } = "";

    [ObservableProperty]
    public partial string SpecialAbilitiesText { get; private set; } = "";

    [ObservableProperty]
    public partial string? SpecialRulesText { get; private set; }

    [ObservableProperty]
    public partial string? PersonalText { get; private set; }

    [ObservableProperty]
    public partial bool HasAnimal { get; private set; }

    public void Update(CompanionState s)
    {
        var a = s.Animal;
        HasAnimal = a is not null;
        var familiar = s.Companion.Kind == CompanionKind.Familiar;
        Attributes = s.Attributes.Select(x => new LabeledValue(x.Definition.Id, x.Value.ToString(CultureInfo.InvariantCulture))).ToList();
        AttributeNote = familiar ? "Enthält die Boni des Vertrauten (+1 auf alle Eigenschaften)." : "";
        var v = s.Values;
        var values = new List<LabeledValue>();
        void Add(string label, object? value)
        {
            if (value is not null) values.Add(new LabeledValue(label, value.ToString()!));
        }
        Add("Lebensenergie", v.LifePoints);
        Add("Astralenergie", v.Astral);
        Add("Initiative", v.Initiative);
        Add("Verteidigung (VW)", v.Defense);
        Add("Ausweichen (AW)", v.Dodge);
        Add("Parade (PA)", v.Parry);
        Add("Seelenkraft / Zähigkeit", v.Spirit is null && v.Toughness is null ? null : $"{v.Spirit} / {v.Toughness}");
        Add("Geschwindigkeit", v.SpeedText is { } st && st != v.Speed?.ToString() ? $"{v.Speed} ({st})" : v.Speed);
        Add("RS / BE", v.Protection is null ? null : $"{v.Protection} / {v.Encumbrance ?? 0}");
        Add("Schmerz +1 bei", a?.PainText);
        Add("Loyalität", s.Loyalty.Value);
        BaseValues = values;
        Attacks = s.Attacks.Select(x => new AttackLine(x.Name, x.Attack?.ToString() ?? x.Text, x.Damage ?? "–", x.Reach ?? "–")).ToList();
        ActionsText = a?.Actions is { } act ? $"Aktionen: {act}" : "";
        SkillsText = string.Join(", ", s.Skills.Select(x => x.NoCheck && x.Value == 0 ? $"{x.Definition.Name} (keine Probe)" : $"{x.Definition.Name} {x.Value}")
            .Append($"Loyalität {s.Loyalty.Value}"));

        TricksTitle = familiar ? "Vertrautentricks und Tricks" : "Tricks";
        var tricks = s.Abilities.Where(x => x.Definition.Kind is AnimalAbilityKind.Trick or AnimalAbilityKind.FamiliarTrick)
            .Select(x => x.Name + (x.Entry.Specification is { Length: > 0 } sp ? $" ({sp})" : "")).Distinct().ToList();
        TricksText = tricks.Count > 0 ? string.Join(", ", tricks) : "keine";
        var trainings = s.Abilities.Where(x => x.Definition.Kind == AnimalAbilityKind.Training).ToList();
        TrainingText = trainings.Count > 0
            ? string.Join(Environment.NewLine, trainings.Select(t => $"{t.Name}: {t.Definition.BonusesText?.Replace("\n", ", ")}"))
            : "keine Ausbildung";
        string Names(IEnumerable<string>? fromAnimal, params AnimalAbilityKind[] kinds) =>
            string.Join(", ", (fromAnimal ?? []).Concat(s.Abilities.Where(x => kinds.Contains(x.Definition.Kind)).Select(x => x.Name)));
        var adv = Names(a?.Advantages, AnimalAbilityKind.Advantage);
        var dis = Names(a?.Disadvantages, AnimalAbilityKind.Disadvantage);
        TraitsText = adv.Length + dis.Length == 0 ? "keine" : $"{(adv.Length > 0 ? adv : "keine")} / {(dis.Length > 0 ? dis : "keine")}";
        var sf = Names(a?.SpecialAbilities, AnimalAbilityKind.SpecialAbility, AnimalAbilityKind.CombatSpecialAbility);
        SpecialAbilitiesText = sf.Length > 0 ? sf : "keine";
        SpecialRulesText = string.Join(Environment.NewLine, new[] { a?.SpecialRules, a?.Note }.OfType<string>()) is { Length: > 0 } r ? r : null;
        var c = s.Companion;
        PersonalText = string.Join(Environment.NewLine, new[]
        {
            c.Gender is { Length: > 0 } g ? $"Geschlecht: {g}" : null,
            c.Age is { Length: > 0 } age ? $"Alter: {age}" : null,
            c.Appearance is { Length: > 0 } ap ? $"Aussehen: {ap}" : null,
            c.Notes,
        }.OfType<string>()) is { Length: > 0 } p ? p : null;
    }
}

/// <summary>Tab „Grundlagen“: Art, Tierart, AP-Beginn, persönliche Angaben, Status.</summary>
public sealed partial class CompanionBasicsPageViewModel(CompanionEditorViewModel owner) : PageViewModel
{
    private bool _updating;

    [ObservableProperty]
    public partial string Name { get; set; } = "";

    [ObservableProperty]
    public partial bool IsFamiliar { get; set; }

    [ObservableProperty]
    public partial string AnimalText { get; private set; } = "";

    [ObservableProperty]
    public partial string? AnimalDetails { get; private set; }

    [ObservableProperty]
    public partial string? FamiliarHint { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<ChoiceOption> BoundOptions { get; private set; } = [];

    [ObservableProperty]
    public partial ChoiceOption? SelectedBound { get; set; }

    [ObservableProperty]
    public partial string ApRuleText { get; private set; } = "";

    [ObservableProperty]
    public partial string Gender { get; set; } = "";

    [ObservableProperty]
    public partial string Age { get; set; } = "";

    [ObservableProperty]
    public partial string Appearance { get; set; } = "";

    [ObservableProperty]
    public partial string Notes { get; set; } = "";

    [ObservableProperty]
    public partial bool Deceased { get; set; }

    [ObservableProperty]
    public partial string DeceasedNote { get; set; } = "";

    private const string AllAwards = "";

    public void Update(CompanionState s)
    {
        _updating = true;
        var c = s.Companion;
        Name = c.Name;
        IsFamiliar = c.Kind == CompanionKind.Familiar;
        AnimalText = s.Animal is { } a ? a.Name + (a.Group is { } g ? $" ({g})" : "") + (c.FamiliarChoice is { } fc ? $" · {fc}" : "") : "noch keine Tierart";
        AnimalDetails = s.Animal is { } animal ? AnimalDescription(animal) : null;
        FamiliarHint = IsFamiliar ? FamiliarText(s) : null;

        // Ab welcher AP-Gutschrift des Helden das Tier mitzählt
        var hero = owner.Editor.Character;
        var options = new List<ChoiceOption> { new(AllAwards, "alle Gutschriften nach der Erschaffung") };
        foreach (var e in hero.Log)
        {
            if (e is ApAwardEntry award)
                options.Add(new ChoiceOption(e.Id.ToString(), $"nach Gutschrift {award.Amount:+0;−0} AP" +
                    (award.Note is { Length: > 0 } n ? $" ({n})" : "") + $" vom {e.Date.LocalDateTime:d}"));
        }
        if (!options.Select(o => o.Label).SequenceEqual(BoundOptions.Select(o => o.Label))) BoundOptions = options;
        SelectedBound = BoundOptions.FirstOrDefault(o => o.Id == (c.BoundAfter?.ToString() ?? AllAwards)) ?? BoundOptions[0];
        ApRuleText = $"Das Tier bekommt ein Viertel der AP, die {(hero.Name.Length > 0 ? hero.Name : "der Held")} danach " +
                     $"gutgeschrieben bekommt (Summe {s.Ap.HeroAwards} AP → {s.Ap.Share} AP, abgerundet), " +
                     "dazu Bonus-AP nach Meisterentscheid (Tab Protokoll).";
        Gender = c.Gender ?? "";
        Age = c.Age ?? "";
        Appearance = c.Appearance ?? "";
        Notes = c.Notes ?? "";
        Deceased = c.Deceased;
        DeceasedNote = c.DeceasedNote ?? "";
        _updating = false;
    }

    /// <summary>Übliche Tierarten der Tradition und was dem Helden für das gewählte Tier noch fehlt.</summary>
    private string FamiliarText(CompanionState s)
    {
        var calculator = new CompanionCalculator(owner.Rules);
        var lines = new List<string>();
        var usual = s.Animal is { } a ? calculator.FamiliarRequirementText(a.Id) : null;
        if (s.Tradition is { } t)
            lines.Add("Übliche Tierarten: " + string.Join("; ", t.Choices.Select(ch =>
                (ch.Name is { } n ? n + ": " : "") + string.Join(", ", ch.Animals.Select(id => owner.Rules.FindAnimal(id)?.Name ?? id)))));
        else if (usual is null)
            lines.Add("Der Held hat keine Tradition mit üblichen Vertrauten (Hexen, Geoden, Brobim-Geoden, Goblinzauberinnen, Zibiljas). " +
                      CompanionCalculator.OtherTraditionsText);
        if (s.Animal is { } animal)
        {
            if (usual is null)
                lines.Add($"{animal.Name} ist bei keiner Tradition ein übliches Vertrautentier, nur als besonderes Vertrautentier (Meisterentscheid).");
            else if (s.Tradition is null)
                lines.Add($"Üblich ist {animal.Name} als Vertrautentier bei {usual}; bei anderen Traditionen nur in seltenen Fällen (Meisterentscheid).");
            else if (s.Tradition.Choices.All(ch => !ch.Animals.Contains(animal.Id)))
                lines.Add($"{animal.Name} ist übliches Vertrautentier bei {usual}; für diesen Helden nur als besonderes Vertrautentier (Meisterentscheid).");
        }
        if (owner.Editor.State.SpecialAbilities.All(x => x.Definition.Id != "sf.vertrautenbindung"))
            lines.Add("Außerdem fehlt die Sonderfertigkeit Vertrautenbindung.");
        return string.Join(Environment.NewLine, lines);
    }

    private static string AnimalDescription(AnimalDefinition a)
    {
        var lines = new List<string>();
        void Add(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) lines.Add($"{label}: {value}");
        }
        Add("Größe", a.SizeText);
        Add("Gewicht", a.WeightText);
        Add("Typus", a.Type);
        Add("Anzahl", a.Count);
        Add("Beute", a.Prey);
        Add("Kampfverhalten", a.CombatBehaviour);
        Add("Flucht", a.Flight);
        Add("Empfohlene Vorteile", a.RecommendedAdvantages is { } ra ? string.Join(", ", ra) : null);
        Add("Empfohlene Nachteile", a.RecommendedDisadvantages is { } rd ? string.Join(", ", rd) : null);
        Add("Sonderregeln", a.SpecialRules);
        Add("Abrichten", a.Training is { } t
            ? $"Ausbildungsaufsatz-Mod {CompanionTexts.Signed(t.TrainingModifier ?? 0)}, Trick-Mod {CompanionTexts.Signed(t.TrickModifier ?? 0)}, {t.Complexity}"
            : null);
        Add("Publikation", string.Join(", ", a.Publications.Select(p => p.Book + (p.Page is { } pg ? $" S. {pg}" : ""))));
        return string.Join(Environment.NewLine, lines);
    }

    private void Set(Action<Companion> change)
    {
        if (!_updating) owner.Change(change);
    }

    partial void OnNameChanged(string value) => Set(c => c.Name = value.Trim());
    partial void OnGenderChanged(string value) => Set(c => c.Gender = Empty(value));
    partial void OnAgeChanged(string value) => Set(c => c.Age = Empty(value));
    partial void OnAppearanceChanged(string value) => Set(c => c.Appearance = Empty(value));
    partial void OnNotesChanged(string value) => Set(c => c.Notes = Empty(value));
    partial void OnDeceasedChanged(bool value) => Set(c => c.Deceased = value);
    partial void OnDeceasedNoteChanged(string value) => Set(c => c.DeceasedNote = Empty(value));

    partial void OnIsFamiliarChanged(bool value) =>
        Set(c => c.Kind = value ? CompanionKind.Familiar : CompanionKind.Companion);

    partial void OnSelectedBoundChanged(ChoiceOption? value)
    {
        if (value is null) return;
        Set(c => c.BoundAfter = Guid.TryParse(value.Id, out var id) ? id : null);
    }

    private static string? Empty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Tierart wählen; bei Vertrauten stehen die Tierarten der Tradition vorn.</summary>
    [RelayCommand]
    private void ChooseAnimal()
    {
        var state = owner.State;
        var rules = owner.Rules;
        var sources = SourceSelection.ForCharacter(owner.Editor.Character, rules.Books);
        var choices = IsFamiliar ? state?.Tradition?.Choices ?? [] : [];
        var suggested = choices.SelectMany(ch => ch.Animals.Select(id => (Id: id, Group: ch.Name is { } n ? $"Vertraute: {n}" : "Vertraute")))
            .ToList();
        // Übliche Vertraute anderer Traditionen mit der nötigen Tradition und Ausprägung
        var calculator = new CompanionCalculator(rules);
        var others = IsFamiliar
            ? rules.Familiars.Traditions.SelectMany(t => t.Choices).SelectMany(ch => ch.Animals).Distinct()
                .Where(id => suggested.All(x => x.Id != id))
                .Select(rules.FindAnimal).OfType<AnimalDefinition>().Where(a => sources.IsAvailable(a.Publications))
                .OrderBy(a => a.Name, StringComparer.CurrentCulture)
                .Select(a => new PickerItemViewModel(a.Id, a.Name,
                    state?.Tradition is null ? "Übliche Vertraute" : "Übliche Vertraute anderer Traditionen",
                    "braucht " + calculator.FamiliarRequirementText(a.Id), () => AnimalDescription(a)))
                .ToList()
            : [];
        var usualIds = IsFamiliar
            ? rules.Familiars.Traditions.SelectMany(t => t.Choices).SelectMany(ch => ch.Animals).ToHashSet()
            : [];
        var items = suggested.Select(x => rules.FindAnimal(x.Id) is { } a
                ? new PickerItemViewModel(a.Id, a.Name, x.Group, Short(a), () => AnimalDescription(a))
                : null).OfType<PickerItemViewModel>()
            .Concat(others)
            .Concat(rules.Animals.Where(a => sources.IsAvailable(a.Publications) && !usualIds.Contains(a.Id))
                .OrderBy(a => a.Group is null).ThenBy(a => a.Group, StringComparer.CurrentCulture)
                .ThenBy(a => a.Name, StringComparer.CurrentCulture) // Tiere ohne Gruppe am Ende
                .Select(a => IsFamiliar
                    // Ohne übliche Tradition: eine Gruppe, die Tiergruppe steht in der Zeile
                    ? new PickerItemViewModel(a.Id, a.Name, "Besondere Vertraute (nur Meisterentscheid)",
                        string.Join(" · ", new[] { a.Group, Short(a) }.Where(x => !string.IsNullOrEmpty(x))), () => AnimalDescription(a))
                    : new PickerItemViewModel(a.Id, a.Name, a.Group ?? "Weitere Tiere", Short(a), () => AnimalDescription(a))));
        owner.Editor.OpenPicker("Tierart wählen", items, state?.Animal?.Id, id => owner.Change(c =>
        {
            c.Animal = id;
            c.FamiliarChoice = choices.FirstOrDefault(ch => ch.Animals.Contains(id))?.Name;
        }), sortGroups: false);
    }

    private static string Short(AnimalDefinition a) =>
        string.Join(" · ", new[] { a.SizeCategory, a.LifePoints is { } l ? $"LeP {l}" : null,
            a.Domesticated == true ? "domestiziert" : a.Domesticated == false ? "Wildtier" : null }.OfType<string>());

    /// <summary>Tier aus der Liste entfernen (rückgängig machbar).</summary>
    [RelayCommand]
    private void Delete() => owner.Editor.Confirm("Tier entfernen",
        $"„{(Name.Length > 0 ? Name : "Dieses Tier")}“ mit allen Werten und dem Protokoll entfernen? Verstorbene Tiere lassen sich " +
        "stattdessen als verstorben markieren.", "Entfernen", () => owner.Editor.RemoveCompanion(owner.Id));
}

/// <summary>Tab „Protokoll“: Bonus-AP nach Meisterentscheid und alle Einträge des Tiers.</summary>
public sealed partial class CompanionLogPageViewModel(CompanionEditorViewModel owner) : PageViewModel
{
    public ObservableCollection<CompanionLogLineViewModel> Entries { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AwardCommand))]
    public partial string AwardAmount { get; set; } = "";

    [ObservableProperty]
    public partial string AwardNote { get; set; } = "";

    [ObservableProperty]
    public partial string HeroAwardsText { get; private set; } = "";

    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    public void Update(CompanionState s)
    {
        Entries.Clear();
        foreach (var e in s.Entries.Reverse()) Entries.Add(new CompanionLogLineViewModel(owner, e));
        IsEmpty = Entries.Count == 0;
        HeroAwardsText = $"Vom Helden: {s.Ap.HeroAwards} AP seit der Bindung bzw. Aufnahme → {s.Ap.Share} AP für das Tier · " +
                         $"Bonus-AP {s.Ap.Bonus} · ausgegeben {s.Ap.Spent} · frei {s.Ap.Remaining}";
    }

    private bool CanAward() => int.TryParse(AwardAmount.Trim().Replace('−', '-'), out var v) && v != 0;

    /// <summary>Bonus-AP nach Meisterentscheid bzw. Startausbildung gutschreiben.</summary>
    [RelayCommand(CanExecute = nameof(CanAward))]
    private void Award()
    {
        if (!int.TryParse(AwardAmount.Trim().Replace('−', '-'), out var amount) || amount == 0) return;
        var note = string.IsNullOrWhiteSpace(AwardNote) ? null : AwardNote.Trim();
        owner.Change(c => c.Log.Add(new ApAwardEntry { Amount = amount, Note = note }));
        AwardAmount = "";
        AwardNote = "";
    }
}

public sealed partial class CompanionLogLineViewModel(CompanionEditorViewModel owner, CompanionEntryState state) : ObservableObject
{
    public string Text { get; } = state.Text;

    public string DateText { get; } = state.Entry.Date.LocalDateTime.ToString("d");

    public string ApText { get; } = state.Entry is ApAwardEntry a ? $"{a.Amount:+0;−0} AP"
        : state.Ap == 0 ? ""
        : state.HeroAp ? $"{state.Ap} AP (Held)"
        : state.Ap > 0 ? $"−{state.Ap} AP" : $"+{-state.Ap} AP";

    /// <summary>Eintrag löschen (Korrektur, rückgängig machbar).</summary>
    [RelayCommand]
    private void Delete() => owner.Change(c => c.Log.RemoveAll(e => e.Id == state.Entry.Id));
}

/// <summary>Rechte Spalte bei geöffnetem Tier.</summary>
public sealed partial class CompanionSummaryViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Title { get; private set; } = "";

    [ObservableProperty]
    public partial string ApText { get; private set; } = "";

    [ObservableProperty]
    public partial string ApDetailText { get; private set; } = "";

    [ObservableProperty]
    public partial bool IsOverspent { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<AttributeValueViewModel> Attributes { get; private set; } = [];

    [ObservableProperty]
    public partial string DerivedText { get; private set; } = "";

    public ObservableCollection<IssueViewModel> Issues { get; } = [];

    [ObservableProperty]
    public partial bool HasIssues { get; private set; }

    public void Update(CompanionState s)
    {
        Title = $"AP von {CompanionTexts.Name(s)}";
        ApText = $"{s.Ap.Spent} / {s.Ap.Total} AP";
        ApDetailText = $"¼ von {s.Ap.HeroAwards} AP des Helden = {s.Ap.Share} AP" +
                       (s.Ap.Bonus != 0 ? $" · Bonus-AP {CompanionTexts.Signed(s.Ap.Bonus)}" : "") +
                       (s.Loyalty.HeroAp > 0 ? $" · Loyalität {s.Loyalty.HeroAp} AP vom Helden" : "");
        IsOverspent = s.Ap.Remaining < 0;
        Attributes = s.Attributes.Select(a => new AttributeValueViewModel(a.Definition.Id, a.Value)).ToList();
        DerivedText = CompanionTexts.ShortValues(s);
        Issues.Clear();
        foreach (var i in s.Issues.OrderByDescending(i => i.Severity)) Issues.Add(new IssueViewModel(i));
        HasIssues = Issues.Count > 0;
    }
}
