using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.ViewModels;

/// <summary>Waffen und Rüstungen mit Kampfwerten, Rüstungsschutz und Belastung.</summary>
public sealed partial class EquipmentPageViewModel(EditorViewModel editor) : ViewModelBase
{
    public ObservableCollection<ArmorRowViewModel> Armors { get; } = [];

    public ObservableCollection<WeaponRowViewModel> Weapons { get; } = [];

    /// <summary>„RS 4 · Belastung II“.</summary>
    [ObservableProperty]
    public partial string ArmorText { get; private set; } = "";

    /// <summary>Belastung: Ausgangswert der Rüstung und abgeleiteter Wert (mit Belastungsgewöhnung).</summary>
    [ObservableProperty]
    public partial string EncumbranceText { get; private set; } = "";

    /// <summary>Auswirkungen der Belastung, z. B. „AT −2, Verteidigung −2, INI −2, GS −2“.</summary>
    [ObservableProperty]
    public partial string? EffectsText { get; private set; }

    /// <summary>Basiswerte ohne und mit Rüstung: „INI 12 → 9 · GS 8 → 5 · AW 6 → 4“.</summary>
    [ObservableProperty]
    public partial string ValuesText { get; private set; } = "";

    public void Update(CharacterState state)
    {
        var eq = state.Equipment;
        Sync(Armors, eq.Armors, a => a.Index, (row, a) => row.Update(a, eq, state), a => new ArmorRowViewModel(editor, a));
        Sync(Weapons, eq.Weapons, w => w.Index, (row, w) => row.Update(w, state), w => new WeaponRowViewModel(editor, w));

        ArmorText = eq.WornArmor is { } armor ? $"{armor.Name} · RS {eq.Protection}" : "Keine Rüstung angelegt · RS 0";
        EncumbranceText = eq.Habituation > 0 && eq.WornArmor is not null
            ? $"Belastung {Stufe(eq.Encumbrance)} (Rüstung {Stufe(eq.ArmorEncumbrance)}, Belastungsgewöhnung {TraitRules.Roman(eq.Habituation)})"
            : $"Belastung {Stufe(eq.Encumbrance)}" + (eq.WornArmor is not null ? $" (Rüstung {Stufe(eq.ArmorEncumbrance)})" : "");
        var effects = new List<string>();
        if (eq.Encumbrance > 0)
            effects.Add($"AT −{eq.Encumbrance}, Verteidigung −{eq.Encumbrance}, INI −{eq.Encumbrance}, GS −{eq.Encumbrance}, " +
                        $"Talente mit Belastung −{eq.Encumbrance}");
        if (eq.SpeedPenalty != 0 || eq.InitiativePenalty != 0)
            effects.Add("zusätzlich " + string.Join(", ", new[]
            {
                eq.SpeedPenalty != 0 ? $"{eq.SpeedPenalty:+0;−0} GS" : null,
                eq.InitiativePenalty != 0 ? $"{eq.InitiativePenalty:+0;−0} INI" : null,
            }.OfType<string>()));
        EffectsText = effects.Count > 0 ? string.Join("; ", effects) : null;
        var d = state.Derived;
        ValuesText = $"INI {d.Initiative} → {eq.Initiative} · " + (d.Speed is { } gs ? $"GS {gs} → {eq.Speed} · " : "") +
                     $"AW {d.Dodge} → {eq.Dodge}";
    }

    private static string Stufe(int level) => level == 0 ? "0" : TraitRules.Roman(level);

    /// <summary>Zeilen bleiben erhalten, solange sich die Einträge nicht ändern.</summary>
    private static void Sync<TRow, TState>(ObservableCollection<TRow> rows, IReadOnlyList<TState> items, Func<TState, int> index,
        Action<TRow, TState> update, Func<TState, TRow> create) where TRow : EquipmentRowViewModel
    {
        var same = rows.Count == items.Count && rows.Zip(items).All(p => p.First.Index == index(p.Second));
        if (!same)
        {
            rows.Clear();
            foreach (var item in items) rows.Add(create(item));
        }
        foreach (var (row, item) in rows.Zip(items)) update(row, item);
    }

    [RelayCommand]
    private void AddWeapon() => Add("Waffe hinzufügen", editor.Rules.Weapons.Select(w => (w.Id, w.Name, w.Category, w.Publications,
        Detail: $"TP {w.DamageText}" + (w.AttackModifier is { } at ? $" · AT/PA {at:+0;−0}/" + (w.NoParry ? "–" : $"{w.ParryModifier ?? 0:+0;−0}") : "") +
                (w.Reach is { Length: > 0 } rw ? $" · RW {rw}" : "") + (w.Ranges is { } r ? $" · RW {r}" : ""))));

    [RelayCommand]
    private void AddArmor() => Add("Rüstung hinzufügen", editor.Rules.Armors.Select(a => (a.Id, a.Name, a.Category, a.Publications,
        Detail: $"RS {a.Protection} · BE {a.Encumbrance}" + (a.ExtraPenaltiesText is { } x ? $" · {x}" : ""))));

    private void Add(string title, IEnumerable<(string Id, string Name, string Group, IReadOnlyList<Publication> Publications, string Detail)> items)
    {
        var sources = SourceSelection.ForCharacter(editor.Character, editor.Rules.Books);
        editor.OpenPicker(title,
            items.Where(i => sources.IsAvailable(i.Publications)).Select(i => new PickerItemViewModel(i.Id, i.Name, i.Group, i.Detail)),
            null,
            id => editor.ChangeEquipment(list =>
            {
                // Erste Rüstung wird gleich angelegt, Waffen gelten als geführt
                var armor = editor.Rules.FindArmor(id) is not null;
                var equip = !armor || list.All(e => !(e.Equipped && editor.Rules.FindArmor(e.Id) is not null));
                list.Add(new EquipmentEntry { Id = id, Equipped = equip });
            }));
    }
}

/// <summary>Gemeinsames einer Ausrüstungszeile: Position, Entfernen, Details.</summary>
public abstract partial class EquipmentRowViewModel(EditorViewModel editor, int index) : ObservableObject
{
    protected EditorViewModel Editor { get; } = editor;

    public int Index { get; protected set; } = index;

    [ObservableProperty]
    public partial bool ShowDetails { get; set; }

    [ObservableProperty]
    public partial string? IssueText { get; protected set; }

    [RelayCommand]
    private void ToggleDetails() => ShowDetails = !ShowDetails;

    [RelayCommand]
    private void Remove()
    {
        var index = Index;
        Editor.ChangeEquipment(list => list.RemoveAt(index));
    }

    protected void SetIssues(CharacterState state)
    {
        var messages = state.IssuesFor(CharacterCalculator.EquipmentTarget(Index)).Select(i => i.Message).ToList();
        IssueText = messages.Count > 0 ? string.Join(Environment.NewLine, messages) : null;
    }

    protected static string Join(params string?[] parts) =>
        string.Join(Environment.NewLine + Environment.NewLine, parts.Where(p => !string.IsNullOrWhiteSpace(p)));
}

public sealed partial class ArmorRowViewModel(EditorViewModel editor, ArmorState state) : EquipmentRowViewModel(editor, state.Index)
{
    public string Name => state.Definition.Name;

    [ObservableProperty]
    public partial string Detail { get; private set; } = "";

    [ObservableProperty]
    public partial string Details { get; private set; } = "";

    /// <summary>Angelegt (nur eine Rüstung zugleich).</summary>
    public bool Worn
    {
        get => state.Entry.Equipped;
        set
        {
            if (value == state.Entry.Equipped) return;
            var index = Index;
            var rules = Editor.Rules;
            Editor.ChangeEquipment(list =>
            {
                if (value)
                    foreach (var other in list.Where(e => rules.FindArmor(e.Id) is not null)) other.Equipped = false;
                list[index].Equipped = value;
            });
        }
    }

    public void Update(ArmorState armor, EquipmentState equipment, CharacterState character)
    {
        state = armor;
        Index = armor.Index;
        var d = armor.Definition;
        Detail = (d.Category != d.Name ? $"{d.Category} · " : "") + $"RS {d.Protection} · BE {d.Encumbrance}" +
                 (d.ExtraPenaltiesText is { } x ? $" · {x}" : "");
        Details = Join(d.Note, d.AdvantageText is { } a ? "Rüstungsvorteil: " + a : null,
            d.DisadvantageText is { } n ? "Rüstungsnachteil: " + n : null, d.Weight is { } w ? "Gewicht: " + w : null,
            d.Price is { } p ? "Preis: " + p : null);
        OnPropertyChanged(nameof(Worn));
        SetIssues(character);
    }
}

public sealed partial class WeaponRowViewModel(EditorViewModel editor, WeaponState state) : EquipmentRowViewModel(editor, state.Index)
{
    public string Name => state.Definition.Name;

    /// <summary>„Schwerter · RW mittel“.</summary>
    [ObservableProperty]
    public partial string Detail { get; private set; } = "";

    /// <summary>„AT 14 · PA 8 · TP 1W6+5“ bzw. „FK 13 · TP 1W6+8“.</summary>
    [ObservableProperty]
    public partial string ValuesText { get; private set; } = "";

    [ObservableProperty]
    public partial string Details { get; private set; } = "";

    public void Update(WeaponState weapon, CharacterState character)
    {
        state = weapon;
        Index = weapon.Index;
        var d = weapon.Definition;
        var ct = Editor.Rules.FindCombatTechnique(d.CombatTechnique)?.Name ?? d.Category;
        Detail = string.Join(" · ", new[]
        {
            ct, d.TwoHanded ? "zweihändig" : null, d.Improvised ? "improvisiert" : null,
            d.Reach is { Length: > 0 } rw ? "RW " + rw : null, d.Ranges is { } r ? "RW " + r : null,
            d.LoadTime is { } lz ? "LZ " + lz : null,
        }.OfType<string>());
        ValuesText = string.Join(" · ", new[]
        {
            weapon.Attack is { } at ? $"AT {at}" : null, weapon.Parry is { } pa ? $"PA {pa}" : null,
            weapon.RangedAttack is { } fk ? $"FK {fk}" : null, $"TP {weapon.DamageText}",
        }.OfType<string>());
        Details = Join(
            d.DamageThresholds is { Count: > 0 } s
                ? $"Schaden: {d.DamageText}, Schadensbonus +{weapon.DamageBonus} (Schadensschwelle " +
                  string.Join(", ", s.Select(t => $"{t.Key} {t.Value}")) + ")" +
                  (d.AttackModifier is { } am ? $" · AT/PA-Mod {am:+0;−0}/" + (d.NoParry ? "–" : $"{d.ParryModifier ?? 0:+0;−0}") : "")
                : null,
            d.Note, d.AdvantageText is { } a ? "Waffenvorteil: " + a : null,
            d.DisadvantageText is { } n ? "Waffennachteil: " + n : null, d.Ammunition is { } m ? "Munition: " + m : null,
            d.Weight is { } w ? "Gewicht: " + w : null, d.Price is { } p ? "Preis: " + p : null);
        SetIssues(character);
    }
}
