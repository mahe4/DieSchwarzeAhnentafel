using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.ViewModels;

/// <summary>
/// Würfelprobe aus dem Charakterbogen: Talent/Zauber (3W20 mit QS) oder Kampfwert (1W20 mit Bestätigung und TP). Beim
/// Öffnen wird sofort gewürfelt; ein geänderter Modifikator, angekreuzte Umstände (<see cref="CombatSituations"/>) oder
/// eine andere Angriffsart (einhändig, beidhändig, falsche Hand, Parade) werten dieselben Würfel neu aus (die Regeln in
/// <see cref="CheckRolls"/>).
/// </summary>
public sealed partial class DiceRollViewModel : ObservableObject
{
    private readonly Random _random;
    private readonly Action _close;
    private SkillCheck? _skill;
    private int[] _dice = [];
    private int _confirmation;
    private int[] _damageDice = [];
    private int[] _botchDice = [];

    /// <summary>Angekreuzte Umstände; bleiben beim Wechsel AT ↔ PA erhalten, soweit es sie dort gibt.</summary>
    private readonly HashSet<string> _situations = [];

    private DiceRollViewModel(string title, Random random, Action close)
    {
        Title = title;
        _random = random;
        _close = close;
    }

    public static DiceRollViewModel ForSkill(SkillCheck check, Random random, Action close)
    {
        var vm = new DiceRollViewModel(check.Name, random, close)
        {
            _skill = check,
            Subtitle = $"{string.Join("/", check.Attributes.Select(a => a.Short))} " +
                       $"({string.Join("/", check.Attributes.Select(a => a.Value))}) · FW {check.SkillValue}",
            Note = check.Note,
        };
        vm.Modifier = check.DefaultModifier;
        vm.Roll();
        return vm;
    }

    /// <param name="title">Waffe bzw. Kampftechnik.</param>
    /// <param name="checks">Wählbare Werte (AT, PA, beidhändig …); der erste ist vorgewählt.</param>
    /// <param name="hero">Für Umstände aus Sonderfertigkeiten und Vorteilen; <c>null</c> = nur die allgemeinen.</param>
    public static DiceRollViewModel ForCombat(string title, IReadOnlyList<CombatCheck> checks, CombatCheck? selected, Random random,
        Action close, CharacterState? hero = null)
    {
        var vm = new DiceRollViewModel(title, random, close)
        {
            Options = checks.Count > 1 ? checks.Select(c => new CombatOption(c)).ToList() : [],
            _hero = hero,
        };
        vm._combat = selected ?? checks[0];
        vm.SelectedOption = vm.Options.FirstOrDefault(o => o.Check == vm._combat);
        vm.BuildSituations();
        vm.Roll();
        return vm;
    }

    private CombatCheck? _combat;
    private CharacterState? _hero;

    public string Title { get; }

    [ObservableProperty]
    public partial string? Subtitle { get; private set; }

    /// <summary>„modifiziert durch SK“, Belastung …</summary>
    [ObservableProperty]
    public partial string? Note { get; private set; }

    /// <summary>Wählbare Kampfwerte; leer bei nur einem.</summary>
    public IReadOnlyList<CombatOption> Options { get; private init; } = [];

    public bool HasOptions => Options.Count > 0;

    [ObservableProperty]
    public partial CombatOption? SelectedOption { get; set; }

    partial void OnSelectedOptionChanged(CombatOption? value)
    {
        if (value is null || value.Check == _combat) return;
        // Auswahl (einhändig, beidhändig, falsche Hand, Parade) wertet dieselben Würfel neu aus wie der Modifikator
        _combat = value.Check;
        BuildSituations();
        Evaluate();
    }

    /// <summary>Frei gesetzter Modifikator (−/+); positiv = Erleichterung, negativ = Erschwernis.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModifierText), nameof(SituationText))]
    public partial int Modifier { get; set; }

    partial void OnModifierChanged(int value) => Evaluate();

    /// <summary>Summe der angekreuzten Umstände.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModifierText), nameof(SituationText), nameof(SituationHeader))]
    public partial int SituationModifier { get; private set; }

    /// <summary>TP aus den Umständen (Fernkampf nah/weit).</summary>
    private int _situationDamage;

    private int TotalModifier => Modifier + SituationModifier;

    public string ModifierText => TotalModifier switch
    {
        > 0 and var m => $"+{m} erleichtert",
        < 0 and var m => $"−{-m} erschwert",
        _ => "±0",
    };

    /// <summary>„davon Umstände −6“; <c>null</c> ohne Umstände.</summary>
    public string? SituationText => SituationModifier == 0 ? null : $"davon Umstände {Signed(SituationModifier)}";

    /// <summary>Kopf der aufklappbaren Liste: „Umstände (−6)“.</summary>
    public string SituationHeader => Situations.SelectMany(g => g.Options).Any(o => o.IsSelected)
        ? $"Umstände ({Signed(SituationModifier)})"
        : "Umstände";

    private static string Signed(int value) => value switch { > 0 => $"+{value}", < 0 => $"−{-value}", _ => "±0" };

    /// <summary>Ankreuzbare Umstände der gewählten Kampfprobe; leer bei Fertigkeitsproben.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSituations), nameof(SituationHeader))]
    public partial IReadOnlyList<SituationGroupViewModel> Situations { get; private set; } = [];

    public bool HasSituations => Situations.Count > 0;

    private void BuildSituations()
    {
        if (_combat is null) return;
        Situations = CombatSituations.For(_combat, _hero).Select(g => new SituationGroupViewModel(g, _situations, SituationsChanged)).ToList();
        UpdateSituationSum();
    }

    private void SituationsChanged()
    {
        UpdateSituationSum();
        OnPropertyChanged(nameof(SituationHeader));
        Evaluate();
    }

    /// <summary>Nur die Umstände der aktuellen Probe zählen; gemerkte von AT bleiben bei PA unberücksichtigt.</summary>
    private void UpdateSituationSum()
    {
        var (modifier, damage) = CombatSituations.Sum(Situations.SelectMany(g => g.Options).Where(o => o.IsSelected).Select(o => o.Option));
        _situationDamage = damage;
        SituationModifier = modifier;
    }

    [RelayCommand]
    private void Increase() => Modifier++;

    [RelayCommand]
    private void Decrease() => Modifier--;

    /// <summary>Gewürfelte Augen mit Zielwert darunter.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<DieFace> Faces { get; private set; } = [];

    /// <summary>„Gelungen“, „Kritischer Treffer“ …</summary>
    [ObservableProperty]
    public partial string Outcome { get; private set; } = "";

    /// <summary>„QS 2“ bei gelungenen Fertigkeitsproben; sonst <c>null</c>.</summary>
    [ObservableProperty]
    public partial string? Quality { get; private set; }

    /// <summary>„5 FP übrig“, „Verteidigung des Gegners halbiert“ …</summary>
    [ObservableProperty]
    public partial string? Detail { get; private set; }

    /// <summary>„TP 1W6+4: 3 + 4 = 7“ nach einem Treffer bzw. die SP eines Patzers.</summary>
    [ObservableProperty]
    public partial string? Damage { get; private set; }

    /// <summary>Art des Ergebnisses für die Farbe: gelungen, misslungen, kritisch, Patzer.</summary>
    [ObservableProperty]
    public partial bool IsSuccess { get; private set; }

    [ObservableProperty]
    public partial bool IsFailure { get; private set; }

    [ObservableProperty]
    public partial bool IsCritical { get; private set; }

    [ObservableProperty]
    public partial bool IsBotch { get; private set; }

    [RelayCommand]
    private void Roll()
    {
        if (_skill is not null)
            _dice = CheckRolls.Roll(_random, 3, 20);
        else
        {
            _dice = [CheckRolls.D20(_random)];
            _confirmation = CheckRolls.D20(_random);
            _damageDice = _combat?.Damage is { } d ? CheckRolls.Roll(_random, d.Dice, d.Sides) : [];
            _botchDice = CheckRolls.Roll(_random, CheckRolls.BotchDamage.Dice, CheckRolls.BotchDamage.Sides);
        }
        Evaluate();
    }

    [RelayCommand]
    private void Close() => _close();

    private void Evaluate()
    {
        if (_dice.Length == 0) return;
        if (_skill is not null) EvaluateSkill(_skill);
        else if (_combat is not null) EvaluateCombat(_combat);
    }

    private void EvaluateSkill(SkillCheck check)
    {
        var r = CheckRolls.Evaluate(check, Modifier, _dice);
        Faces = r.Dice.Select((d, i) => new DieFace(d, $"{check.Attributes[i].Short} {r.Targets[i]}", d > r.Targets[i],
            d is 1 or 20)).ToList();
        (Outcome, Detail) = r.Outcome switch
        {
            CheckOutcome.Impossible => ("Keine Probe möglich", "Eine Eigenschaft sinkt durch die Erschwernis auf 0 oder weniger."),
            CheckOutcome.SpectacularCritical => ("Dreifach-1: spektakulärer Erfolg", PointsText(r.Points)),
            CheckOutcome.Critical => ("Kritischer Erfolg (Doppel-1)", PointsText(r.Points)),
            CheckOutcome.SpectacularBotch => ("Dreifach-20: schreckliches Missgeschick", null),
            CheckOutcome.Botch => ("Patzer (Doppel-20)", null),
            CheckOutcome.Success => ("Gelungen", PointsText(r.Points)),
            _ => ("Misslungen", $"{-r.Points} {(r.Points == -1 ? "Punkt" : "Punkte")} gefehlt"),
        };
        Quality = r.Succeeded ? $"QS {r.Quality}" : null;
        Damage = null;
        SetOutcomeClass(r.Outcome);
    }

    private static string PointsText(int points) =>
        points <= 0 ? "0 FP übrig (zählt als 1 FP)" : $"{points} FP übrig";

    private void EvaluateCombat(CombatCheck check)
    {
        var r = CheckRolls.Evaluate(check.Value, TotalModifier, _dice[0], _confirmation);
        var damageRoll = check.Damage is { } cd ? cd with { Bonus = cd.Bonus + _situationDamage } : null;
        Subtitle = $"{check.Label} {check.Value}" + (damageRoll is { } d ? $" · TP {d.Format()}" : "");
        var faces = new List<DieFace> { new(r.Roll, $"{check.Label} {r.Target}", r.Roll > r.Target || r.Roll == 20, r.Roll is 1 or 20) };
        if (r.Confirmation is { } c) faces.Add(new DieFace(c, "Bestätigung", c > r.Target || c == 20, false));
        Faces = r.Outcome == CheckOutcome.Impossible ? [] : faces;
        var attack = check.IsAttack;
        (Outcome, Detail) = r.Outcome switch
        {
            CheckOutcome.Impossible => ("Keine Probe möglich", "Der Wert sinkt durch die Erschwernis auf 0 oder weniger."),
            CheckOutcome.Critical when attack => ("Kritischer Treffer", "Verteidigung des Gegners halbiert, TP verdoppelt"),
            CheckOutcome.Critical => ("Kritischer Erfolg", "Sofort ein Passierschlag gegen den Gegner"),
            CheckOutcome.CriticalUnconfirmed when attack => ("Treffer", "1 nicht bestätigt: Verteidigung des Gegners trotzdem halbiert"),
            CheckOutcome.CriticalUnconfirmed => ("Gelungen", "1 nicht bestätigt: normale Verteidigung"),
            CheckOutcome.Botch => ("Patzer", null),
            CheckOutcome.BotchUnconfirmed => (attack ? "Verfehlt" : "Misslungen", "20 nicht bestätigt: gewöhnliches Misslingen"),
            CheckOutcome.Success => (attack ? "Treffer" : "Gelungen", null),
            _ => (attack ? "Verfehlt" : "Misslungen", null),
        };
        Quality = null;
        Damage = r.Outcome switch
        {
            CheckOutcome.Botch => DamageText("Der Held erleidet", "SP",
                CheckRolls.Damage(CheckRolls.BotchDamage, _botchDice, false)),
            _ when attack && r.Succeeded && damageRoll is { } dmg => DamageText(null, "TP",
                CheckRolls.Damage(dmg, DamageDice(dmg), r.Outcome == CheckOutcome.Critical)),
            _ => null,
        };
        SetOutcomeClass(r.Outcome);
    }

    /// <summary>TP-Würfel: einmal je Wurf, nachgewürfelt nur, wenn die gewählte Angriffsart andere Würfel braucht.</summary>
    private int[] DamageDice(Core.Content.DamageRoll damage)
    {
        if (_damageDice.Length < damage.Dice)
            _damageDice = [.. _damageDice, .. CheckRolls.Roll(_random, damage.Dice - _damageDice.Length, damage.Sides)];
        return _damageDice;
    }

    /// <summary>„TP 1W6+4: 3 + 4 = 7“, kritisch „TP 1W6+4: (3 + 4) × 2 = 14“.</summary>
    private static string DamageText(string? prefix, string unit, DamageResult d)
    {
        var parts = string.Join(" + ", d.Dice) + d.Roll.Bonus switch { > 0 => $" + {d.Roll.Bonus}", < 0 => $" − {-d.Roll.Bonus}", _ => "" };
        var sum = d.Doubled ? $"({parts}) × 2" : parts;
        return $"{(prefix is null ? "" : prefix + " ")}{unit} {d.Roll.Format()}: {sum} = {d.Total}";
    }

    private void SetOutcomeClass(CheckOutcome outcome)
    {
        IsCritical = outcome is CheckOutcome.Critical or CheckOutcome.SpectacularCritical;
        IsBotch = outcome is CheckOutcome.Botch or CheckOutcome.SpectacularBotch;
        IsSuccess = !IsCritical && outcome is CheckOutcome.Success or CheckOutcome.CriticalUnconfirmed;
        IsFailure = !IsBotch && !IsSuccess && !IsCritical;
    }
}

/// <param name="Label">„MU 13“ (effektiver Wert) bzw. „Bestätigung“.</param>
/// <param name="Missed">Wurf über dem Zielwert.</param>
/// <param name="Special">1 oder 20.</param>
public sealed record DieFace(int Value, string Label, bool Missed, bool Special);

/// <summary>Gruppe ankreuzbarer Umstände: „Sicht“ mit Stufe 1–3; bei <see cref="SituationGroup.Exclusive"/> höchstens eine.</summary>
public sealed class SituationGroupViewModel
{
    public SituationGroupViewModel(SituationGroup group, HashSet<string> selected, Action changed)
    {
        Title = group.Title;
        Note = group.Note;
        Options = group.Options.Select(o => new SituationOptionViewModel(o, selected.Contains(o.Id), option =>
        {
            if (option.IsSelected)
            {
                selected.Add(option.Option.Id);
                if (group.Exclusive)
                    foreach (var other in Options!.Where(x => x != option && x.IsSelected)) other.IsSelected = false;
            }
            else selected.Remove(option.Option.Id);
            changed();
        })).ToList();
    }

    public string Title { get; }

    public string? Note { get; }

    public IReadOnlyList<SituationOptionViewModel> Options { get; }
}

/// <summary>„nah +2 (+1 TP)“ zum Ankreuzen.</summary>
public sealed partial class SituationOptionViewModel(SituationOption option, bool selected, Action<SituationOptionViewModel> changed)
    : ObservableObject
{
    public SituationOption Option { get; } = option;

    public string Text => $"{Option.Label} {Option.Modifier:+0;−0;±0}" +
                          (Option.DamageBonus != 0 ? $" ({Option.DamageBonus:+0;−0} TP)" : "");

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = selected;

    partial void OnIsSelectedChanged(bool value) => changed(this);
}

/// <summary>Auswahl im Würfeldialog: „AT 14“, „PA beidhändig 6“.</summary>
public sealed record CombatOption(CombatCheck Check)
{
    public string Text => $"{Check.Label} {Check.Value}";
}
