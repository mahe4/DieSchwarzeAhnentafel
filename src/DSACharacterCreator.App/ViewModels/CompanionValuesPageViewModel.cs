using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.ViewModels;

/// <summary>Texte für Liste, Ergebnis und Übersicht eines Tiers.</summary>
public static class CompanionTexts
{
    public static string Name(CompanionState s) =>
        s.Companion.Name.Length > 0 ? s.Companion.Name : s.Animal?.Name ?? "Neues Tier";

    public static string Subtitle(CompanionState s, string heroName)
    {
        var hero = heroName.Length > 0 ? heroName : "des Helden";
        var kind = s.Companion.Kind == CompanionKind.Familiar
            ? $"Vertrautentier von {hero}" + (s.Companion.FamiliarChoice is { } c ? $" ({c})" : "")
            : $"tierischer Begleiter von {hero}";
        return string.Join(" · ", new[] { s.Animal?.Name, kind, s.Animal?.SizeCategory is { } size ? $"Größenkategorie {size}" : null }
            .OfType<string>());
    }

    /// <summary>„LeP 20 · AsP 15 · INI 13+1W6 · VW 7 · RS 1 · Loyalität 4“.</summary>
    public static string ShortValues(CompanionState s)
    {
        var v = s.Values;
        var parts = new List<string>();
        if (v.LifePoints is { } lep) parts.Add($"LeP {lep}");
        if (v.Astral is { } asp) parts.Add($"AsP {asp}");
        if (v.Initiative is { } ini) parts.Add($"INI {ini}");
        if (v.Defense is { } vw) parts.Add($"VW {vw}");
        if (v.Protection is { } rs) parts.Add($"RS {rs}");
        parts.Add($"Loyalität {s.Loyalty.Value}");
        return string.Join(" · ", parts);
    }

    public static string Signed(int value) => value switch { > 0 => $"+{value}", < 0 => $"−{-value}", _ => "0" };
}

/// <summary>Zeile mit −/Wert/+ eines Tiers (Eigenschaft, Talent, Angriff, VW, LeP/AsP, Loyalität).</summary>
public sealed class CompanionValueRowViewModel : ValueRowViewModel
{
    private readonly CompanionEditorViewModel _owner;
    private readonly RaiseTarget _kind;
    private readonly Func<int, int?> _nextCost;
    private string _detail;

    /// <param name="nextCost">AP für die Steigerung auf den übergebenen Wert; <c>null</c> = nicht steigerbar.</param>
    public CompanionValueRowViewModel(CompanionEditorViewModel owner, RaiseTarget kind, string target, string name,
        string detail, Func<int, int?> nextCost, string? details = null)
        : base(owner.Editor, target, name)
    {
        _owner = owner;
        _kind = kind;
        _nextCost = nextCost;
        _detail = detail;
        _details = details;
    }

    private readonly string? _details;

    public override string Detail => _detail;

    public override string? Details => _details;

    public void Update(int value, string detail, IEnumerable<Issue> issues)
    {
        if (detail != _detail)
        {
            _detail = detail;
            OnPropertyChanged(nameof(Detail));
        }
        SetValues(value, value - _owner.Raised(_kind, Id), null, issues);
    }

    protected override int? NextStepCost(int value) => _nextCost(value);

    protected override void Store(int value) => _owner.Step(_kind, Id, value - Value);
}

/// <summary>Tab „Eigenschaften“: Eigenschaften, LeP/AsP-Zukauf, Angriffe und VW.</summary>
public sealed partial class CompanionValuesPageViewModel(CompanionEditorViewModel owner) : PageViewModel
{
    public ObservableCollection<CompanionValueRowViewModel> Attributes { get; } = [];

    public ObservableCollection<CompanionValueRowViewModel> Energies { get; } = [];

    public ObservableCollection<CompanionValueRowViewModel> Combat { get; } = [];

    [ObservableProperty]
    public partial string CombatHint { get; private set; } = "";

    private string? _animal;

    public void Update(CompanionState s)
    {
        var costs = owner.Rules.Costs;
        int? Step(ImprovementCost cost, int value) => value <= costs.MaxValue ? costs.StepCost(cost, Math.Max(value, 1)) : null;
        var familiar = s.Companion.Kind == CompanionKind.Familiar;
        if (_animal != s.Animal?.Id || Combat.Count != s.Attacks.Count + 1 || Energies.Count != (s.Values.Astral is null ? 1 : 2))
        {
            _animal = s.Animal?.Id;
            Attributes.Clear();
            foreach (var a in s.Attributes)
                Attributes.Add(new CompanionValueRowViewModel(owner, RaiseTarget.Attribute, a.Definition.Id, a.Definition.Name, "",
                    v => Step(ImprovementCost.E, v)));
            Energies.Clear();
            Energies.Add(new CompanionValueRowViewModel(owner, RaiseTarget.Energy, Core.Characters.Energies.LifePoints, "Lebenspunkte", "",
                _ => Step(ImprovementCost.D, owner.Raised(RaiseTarget.Energy, Core.Characters.Energies.LifePoints) + 1)));
            if (s.Values.Astral is not null)
                Energies.Add(new CompanionValueRowViewModel(owner, RaiseTarget.Energy, Core.Characters.Energies.Astral, "Astralpunkte", "",
                    _ => Step(ImprovementCost.D, owner.Raised(RaiseTarget.Energy, Core.Characters.Energies.Astral) + 1)));
            Combat.Clear();
            foreach (var a in s.Attacks)
                Combat.Add(new CompanionValueRowViewModel(owner, RaiseTarget.Attack, a.Name, $"{a.Name} (AT)", "",
                    v => a.Attack is null ? null : Step(ImprovementCost.C, v)));
            Combat.Add(new CompanionValueRowViewModel(owner, RaiseTarget.Defense, "", "Verteidigung (VW)", "",
                v => s.Values.Defense is null ? null : Step(ImprovementCost.C, v)));
        }

        foreach (var (row, a) in Attributes.Zip(s.Attributes))
            row.Update(a.Value, $"Tierart {a.Base}" + (a.Bonus != 0 ? $" · Boni {CompanionTexts.Signed(a.Bonus)}" : "") +
                                " · Spalte E" + (s.Animal?.AttributeNotes?.GetValueOrDefault(a.Definition.Id) switch
                                {
                                    "t" => " · tierische Klugheit",
                                    "k" => " · KK schwacher Wesen",
                                    _ => "",
                                }), IssuesFor(s, "attr:" + a.Definition.Id));
        Energies[0].Update(s.Values.LifePoints ?? 0,
            $"Zukauf {owner.Raised(RaiseTarget.Energy, Core.Characters.Energies.LifePoints)} (Spalte D, höchstens KO)",
            IssuesFor(s, "energy:" + Core.Characters.Energies.LifePoints));
        if (Energies.Count > 1)
            Energies[1].Update(s.Values.Astral ?? 0,
                $"Zukauf {owner.Raised(RaiseTarget.Energy, Core.Characters.Energies.Astral)} (Spalte D)" + (familiar ? " · Vertraute 15 AsP" : ""), []);
        foreach (var (row, a) in Combat.Zip(s.Attacks))
            row.Update(a.Attack ?? 0, $"TP {a.Damage} · RW {a.Reach} · Spalte C", []);
        Combat[^1].Update(s.Values.Defense ?? 0, "Spalte C", []);
        CombatHint = familiar
            ? "Vertraute steigern ihre Kampftechniken wie Biss oder Schnabel (Spalte C, Wert = AT)."
            : "Tierische Begleiter steigern AT und VW direkt (Spalte C).";
    }

    private static IEnumerable<Issue> IssuesFor(CompanionState s, string target) => s.Issues.Where(i => i.TargetId == target);
}

/// <summary>Tab „Talente“: Talente der Tierart, gesteigerte und Loyalität.</summary>
public sealed partial class CompanionSkillsPageViewModel(CompanionEditorViewModel owner) : PageViewModel
{
    public ObservableCollection<CompanionValueRowViewModel> Skills { get; } = [];

    public ObservableCollection<CompanionValueRowViewModel> Loyalty { get; } = [];

    [ObservableProperty]
    public partial string LoyaltyHint { get; private set; } = "";

    public void Update(CompanionState s)
    {
        var costs = owner.Rules.Costs;
        int? Step(ImprovementCost cost, int value) => value <= costs.MaxValue ? costs.StepCost(cost, Math.Max(value, 1)) : null;
        if (Skills.Count != s.Skills.Count || !Skills.Zip(s.Skills).All(p => p.First.Id == p.Second.Definition.Id))
        {
            Skills.Clear();
            foreach (var sk in s.Skills)
            {
                var def = sk.Definition;
                Skills.Add(new CompanionValueRowViewModel(owner, RaiseTarget.Skill, def.Id, def.Name, "",
                    v => sk.NoCheck ? null : Step(def.ImprovementCost, v)));
            }
        }
        foreach (var (row, sk) in Skills.Zip(s.Skills))
            row.Update(sk.Value, $"{string.Join("/", sk.Definition.Check)} · {sk.Definition.ImprovementCost} · Tierart {sk.Base}" +
                                 (sk.Bonus != 0 ? $" · Boni {CompanionTexts.Signed(sk.Bonus)}" : "") +
                                 (sk.Note is { } n ? $" · {n}" : ""), []);
        if (Loyalty.Count == 0)
            Loyalty.Add(new CompanionValueRowViewModel(owner, RaiseTarget.Loyalty, "", "Loyalität", "",
                v => Step(ImprovementCost.B, v)));
        Loyalty[0].Update(s.Loyalty.Value, $"MU/KL/IN · B · Startwert {s.Loyalty.Start} · {s.Loyalty.HeroAp} AP vom Helden", []);
        LoyaltyHint = "Die Loyalität steigert die Heldin mit ihren eigenen AP (nicht das Tier), nach einer gelungenen " +
                      "Tierkunde-Probe; bei der Heldenerschaffung ohne Probe.";
    }

    /// <summary>Weiteres Talent für das Tier (gleich auf 1 gesteigert).</summary>
    [RelayCommand]
    private void AddSkill()
    {
        var known = Skills.Select(r => r.Id).ToHashSet();
        owner.Editor.OpenPicker("Talent hinzufügen",
            owner.Rules.Skills.Where(s => !known.Contains(s.Id))
                .Select(s => new PickerItemViewModel(s.Id, s.Name, s.Group switch
                {
                    SkillGroup.Body => "Körper",
                    SkillGroup.Social => "Gesellschaft",
                    SkillGroup.Nature => "Natur",
                    SkillGroup.Knowledge => "Wissen",
                    _ => "Handwerk",
                }, $"{string.Join("/", s.Check)} · {s.ImprovementCost}")),
            null, id => owner.Step(RaiseTarget.Skill, id, 1));
    }
}
