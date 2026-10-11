using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.ViewModels;

/// <summary>Waffen, Rüstungen und sonstige Gegenstände mit Kampfwerten, Rüstungsschutz, Belastung, Traglast und Geld.</summary>
public sealed partial class EquipmentPageViewModel(EditorViewModel editor) : ViewModelBase
{
    public ObservableCollection<ArmorRowViewModel> Armors { get; } = [];

    public ObservableCollection<WeaponRowViewModel> Weapons { get; } = [];

    public ObservableCollection<ItemRowViewModel> Items { get; } = [];

    public ObservableCollection<MoneyRowViewModel> Bookings { get; } = [];

    /// <summary>„RS 4 · Belastung II“.</summary>
    [ObservableProperty]
    public partial string ArmorText { get; private set; } = "";

    /// <summary>Belastung: abgeleiteter Wert mit Ausgangswert der Rüstung, Belastungsgewöhnung und Traglast.</summary>
    [ObservableProperty]
    public partial string EncumbranceText { get; private set; } = "";

    /// <summary>Auswirkungen der Belastung, z. B. „AT −2, Verteidigung −2, INI −2, GS −2“.</summary>
    [ObservableProperty]
    public partial string? EffectsText { get; private set; }

    /// <summary>Basiswerte ohne und mit Rüstung: „INI 12 → 9 · GS 8 → 5 · AW 6 → 4“.</summary>
    [ObservableProperty]
    public partial string ValuesText { get; private set; } = "";

    /// <summary>„Getragen 23,6 Stein von 26 Stein (KK 13 × 2)“.</summary>
    [ObservableProperty]
    public partial string LoadText { get; private set; } = "";

    /// <summary>„Belastung durch Traglast I“; <c>null</c> ohne Überladung.</summary>
    [ObservableProperty]
    public partial string? LoadEffectText { get; private set; }

    /// <summary>„174,5 S“.</summary>
    [ObservableProperty]
    public partial string MoneyText { get; private set; } = "";

    /// <summary>„Startkapital 750 S · Ausrüstung −575,5 S · Einnahmen/Ausgaben +0 S“.</summary>
    [ObservableProperty]
    public partial string MoneyDetailText { get; private set; } = "";

    [ObservableProperty]
    public partial bool MoneyNegative { get; private set; }

    /// <summary>Eingabe für eine neue Buchung: Betrag in Silbertalern (negativ = Ausgabe).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BookCommand))]
    public partial string BookingAmount { get; set; } = "";

    [ObservableProperty]
    public partial string BookingText { get; set; } = "";

    public void Update(CharacterState state)
    {
        var eq = state.Equipment;
        Sync(Armors, eq.Armors.Select(a => (a.Index, a)).ToList(), (row, a) => row.Update(a, eq.Lines[a.Index], state),
            a => new ArmorRowViewModel(editor, a));
        Sync(Weapons, eq.Weapons.Select(w => (w.Index, w)).ToList(), (row, w) => row.Update(w, eq.Lines[w.Index], state),
            w => new WeaponRowViewModel(editor, w));
        var items = eq.Lines.Where(l => l.Definition is not (WeaponDefinition or ArmorDefinition)).Select(l => (l.Index, l)).ToList();
        Sync(Items, items, (row, l) => row.Update(l, state), l => new ItemRowViewModel(editor, l.Index));

        ArmorText = eq.WornArmor is { } armor ? $"{armor.Name} · RS {eq.Protection}" : "Keine Rüstung angelegt · RS 0";
        var sources = new List<string>();
        if (eq.WornArmor is not null)
        {
            sources.Add($"Rüstung {Stufe(eq.ArmorEncumbrance)}");
            if (eq.Habituation > 0) sources.Add($"Belastungsgewöhnung {TraitRules.Roman(eq.Habituation)}");
        }
        if (eq.LoadLevel > 0) sources.Add($"Traglast {TraitRules.Roman(eq.LoadLevel)}");
        EncumbranceText = $"Belastung {Stufe(eq.Encumbrance)}" + (sources.Count > 0 ? $" ({string.Join(", ", sources)})" : "");
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

        LoadText = $"Getragen {CharacterCalculator.FormatWeight(eq.CarriedWeight)} von {CharacterCalculator.FormatWeight(eq.CarryCapacity)} " +
                   $"(KK {eq.CarryCapacity / CharacterCalculator.CarryPerStrength} × {CharacterCalculator.CarryPerStrength})";
        LoadEffectText = eq.LoadLevel > 0
            ? $"Belastung durch Traglast {TraitRules.Roman(eq.LoadLevel)} (je {CharacterCalculator.StonesPerLoadLevel} volle Stein darüber eine Stufe)"
            : null;

        MoneyText = CharacterCalculator.FormatMoney(eq.Money);
        MoneyNegative = eq.Money < 0;
        MoneyDetailText = $"Startkapital {CharacterCalculator.FormatMoney(eq.StartingMoney)}" +
                          (eq.WealthLevel > 0 ? $" (Reich {TraitRules.Roman(eq.WealthLevel)})" :
                              eq.WealthLevel < 0 ? $" (Arm {TraitRules.Roman(-eq.WealthLevel)})" : "") +
                          $" · Gekauft −{CharacterCalculator.FormatMoney(eq.Spent)}" +
                          (eq.Booked != 0 ? $" · Einnahmen/Ausgaben {(eq.Booked > 0 ? "+" : "")}{CharacterCalculator.FormatMoney(eq.Booked)}" : "");
        var money = editor.Character.Money;
        if (Bookings.Count != money.Count || Bookings.Zip(money).Any(p => p.First.Entry != p.Second))
        {
            Bookings.Clear();
            for (var i = 0; i < money.Count; i++) Bookings.Add(new MoneyRowViewModel(editor, money[i], i));
        }
    }

    private static string Stufe(int level) => level == 0 ? "0" : TraitRules.Roman(level);

    /// <summary>Zeilen bleiben erhalten, solange sich die Einträge nicht ändern.</summary>
    private static void Sync<TRow, TState>(ObservableCollection<TRow> rows, IReadOnlyList<(int Index, TState State)> items,
        Action<TRow, TState> update, Func<TState, TRow> create) where TRow : EquipmentRowViewModel
    {
        var same = rows.Count == items.Count && rows.Zip(items).All(p => p.First.Index == p.Second.Index);
        if (!same)
        {
            rows.Clear();
            foreach (var item in items) rows.Add(create(item.State));
        }
        foreach (var (row, item) in rows.Zip(items)) update(row, item.State);
    }

    private static string Values(IPricedItem item) =>
        string.Join(" · ", new[] { item.Weight, item.Price }.Where(t => !string.IsNullOrWhiteSpace(t)));

    [RelayCommand]
    private void AddWeapon() => Add("Waffe hinzufügen", editor.Rules.Weapons.Select(w => (w.Id, w.Name, w.Category, w.Publications,
        Detail: $"TP {w.DamageText}" + (w.AttackModifier is { } at ? $" · AT/PA {at:+0;−0}/" + (w.NoParry ? "–" : $"{w.ParryModifier ?? 0:+0;−0}") : "") +
                (w.Reach is { Length: > 0 } rw ? $" · RW {rw}" : "") + (w.Ranges is { } r ? $" · RW {r}" : "") +
                (w.Price is { } p ? " · " + p : ""))));

    [RelayCommand]
    private void AddArmor() => Add("Rüstung hinzufügen", editor.Rules.Armors.Select(a => (a.Id, a.Name, a.Category, a.Publications,
        Detail: $"RS {a.Protection} · BE {a.Encumbrance}" + (a.ExtraPenaltiesText is { } x ? $" · {x}" : "") +
                (a.Price is { } p ? " · " + p : ""))));

    [RelayCommand]
    private void AddItem() => Add("Gegenstand hinzufügen",
        editor.Rules.Items.Select(i => (i.Id, i.Name, i.Category, i.Publications, Detail: Values(i))));

    [RelayCommand]
    private void AddPackage()
    {
        var sources = SourceSelection.ForCharacter(editor.Character, editor.Rules.Books);
        editor.OpenPicker("Ausrüstungspaket hinzufügen",
            editor.Rules.EquipmentPackages.Where(p => sources.IsAvailable(p.Publications)).Select(p => new PickerItemViewModel(p.Id, p.Name,
                "Ausrüstungspakete", $"{p.Items.Count} Gegenstände · {CharacterCalculator.FormatWeight(p.Weight)} · {CharacterCalculator.FormatMoney(p.Price)}" +
                                     (p.WikiPrice is { } w && w != p.Price ? $" (Wiki: {CharacterCalculator.FormatMoney(w)})" : ""),
                () => string.Join(Environment.NewLine, p.Items.Select(i => "• " + i.Name +
                    (i.PriceSilver is { } price ? $" ({CharacterCalculator.FormatMoney(price)})" : ""))))),
            null,
            id => editor.ChangeEquipment(list =>
            {
                // Jede Paketzeile wird ein eigener Eintrag mit Gewicht und Preis laut Paketliste
                foreach (var line in editor.Rules.FindEquipmentPackage(id)!.Items)
                {
                    var def = editor.Rules.FindAnyItem(line.Ref);
                    list.Add(new EquipmentEntry
                    {
                        Id = def?.Id ?? "", Name = def?.Name == line.Name ? null : line.Name,
                        Weight = line.WeightStone ?? 0, Price = line.PriceSilver ?? 0, Equipped = Equip(list, def),
                    });
                }
            }), multiple: true);
    }

    [RelayCommand]
    private void AddCustomItem() =>
        editor.ChangeEquipment(list => list.Add(new EquipmentEntry { Name = "Eigener Gegenstand", Weight = 0, Price = 0 }));

    private void Add(string title, IEnumerable<(string Id, string Name, string Group, IReadOnlyList<Publication> Publications, string Detail)> items)
    {
        var sources = SourceSelection.ForCharacter(editor.Character, editor.Rules.Books);
        editor.OpenPicker(title,
            items.Where(i => sources.IsAvailable(i.Publications)).Select(i => new PickerItemViewModel(i.Id, i.Name, i.Group, i.Detail,
                () => Describe(editor.Rules.FindAnyItem(i.Id)))),
            null,
            id => editor.ChangeEquipment(list => list.Add(new EquipmentEntry { Id = id, Equipped = Equip(list, editor.Rules.FindAnyItem(id)) })),
            multiple: true);
    }

    /// <summary>Wiki-Texte eines Gegenstands für die Auswahlliste.</summary>
    private static string? Describe(IPricedItem? item)
    {
        string? Field(string label, string? value) => string.IsNullOrWhiteSpace(value) ? null : $"{label}: {value}";
        var parts = item switch
        {
            WeaponDefinition w => new[]
            {
                w.Note, Field("Waffenvorteil", w.AdvantageText), Field("Waffennachteil", w.DisadvantageText),
                Field("Munition", w.Ammunition), Field("Gewicht", w.Weight), Field("Preis", w.Price),
            },
            ArmorDefinition a => new[]
            {
                a.Note, Field("Rüstungsvorteil", a.AdvantageText), Field("Rüstungsnachteil", a.DisadvantageText),
                Field("Gewicht", a.Weight), Field("Preis", a.Price),
            },
            ItemDefinition i => new[]
            {
                i.RuleText, i.Note, Field("Gewicht", i.Weight), Field("Preis", i.Price),
                Field("Strukturpunkte", i.StructurePoints), Field("Komplexität", i.Complexity),
            },
            _ => [],
        };
        var text = string.Join(Environment.NewLine + Environment.NewLine, parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        return text.Length > 0 ? text : null;
    }

    /// <summary>Erste Rüstung wird gleich angelegt, Waffen gelten als geführt.</summary>
    private bool Equip(List<EquipmentEntry> list, IPricedItem? item) => item switch
    {
        WeaponDefinition => true,
        ArmorDefinition => list.All(e => !(e.Equipped && editor.Rules.FindArmor(e.Id) is not null)),
        _ => false,
    };

    private bool CanBook() => ParseMoney(BookingAmount) is not null and not 0;

    [RelayCommand(CanExecute = nameof(CanBook))]
    private void Book()
    {
        if (ParseMoney(BookingAmount) is not { } amount) return;
        var text = string.IsNullOrWhiteSpace(BookingText) ? null : BookingText.Trim();
        editor.Mutate(() => editor.Character.Money.Add(new MoneyEntry { Amount = amount, Text = text }));
        BookingAmount = "";
        BookingText = "";
    }

    /// <summary>„12,5“, „-3“, „−0,5“ → Silbertaler; leer oder unlesbar → <c>null</c>.</summary>
    public static decimal? ParseMoney(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim().Replace('−', '-').Replace(" ", "");
        if (t.EndsWith('S')) t = t[..^1];
        return decimal.TryParse(t, NumberStyles.Number, CultureInfo.GetCultureInfo("de-DE"), out var value) ? value : null;
    }

    public static string FormatNumber(decimal? value) =>
        value?.ToString("0.###", CultureInfo.GetCultureInfo("de-DE")) ?? "";
}

/// <summary>Einnahme oder Ausgabe außerhalb der Ausrüstungsliste.</summary>
public sealed partial class MoneyRowViewModel(EditorViewModel editor, MoneyEntry entry, int index) : ObservableObject
{
    public MoneyEntry Entry { get; } = entry;

    public string Text => (Entry.Amount > 0 ? "+" : "") + CharacterCalculator.FormatMoney(Entry.Amount) +
                          (Entry.Text is { } t ? " · " + t : "");

    [RelayCommand]
    private void Remove() => editor.Mutate(() => editor.Character.Money.RemoveAt(index));
}

/// <summary>Gemeinsames einer Ausrüstungszeile: Menge, dabei, bezahlt, Gewicht und Preis, Entfernen, Details.</summary>
public abstract partial class EquipmentRowViewModel(EditorViewModel editor, int index) : ObservableObject
{
    protected EditorViewModel Editor { get; } = editor;

    public int Index { get; protected set; } = index;

    [ObservableProperty]
    public partial bool ShowDetails { get; set; }

    [ObservableProperty]
    public partial string? IssueText { get; protected set; }

    /// <summary>„2 × 0,5 Stein · 10 S“ bzw. „1,5 Stein · 140 S“.</summary>
    [ObservableProperty]
    public partial string WeightPriceText { get; private set; } = "";

    [ObservableProperty]
    public partial int Quantity { get; private set; } = 1;

    /// <summary>Eigenes Gewicht je Stück (leer = Regelwert); wird beim Verlassen des Felds übernommen.</summary>
    public string WeightInput
    {
        get => EquipmentPageViewModel.FormatNumber(Line?.Weight);
        set => Change(e => e.Weight = Override(EquipmentPageViewModel.ParseMoney(value), Line?.Definition?.WeightStone));
    }

    /// <summary>Eigener Preis je Stück (leer = Regelwert).</summary>
    public string PriceInput
    {
        get => EquipmentPageViewModel.FormatNumber(Line?.Price);
        set => Change(e => e.Price = Override(EquipmentPageViewModel.ParseMoney(value), Line?.Definition?.PriceSilver));
    }

    /// <summary>Hinweis zu Gewicht/Preis laut Regeln, z. B. „Regelwert 0,5 Stein · 10 S“.</summary>
    public string? RuleValuesText => Line?.Definition is { } d && (d.Weight is not null || d.Price is not null)
        ? "Laut Regeln: " + string.Join(" · ", new[] { d.Weight, d.Price }.OfType<string>())
        : null;

    public bool Carried
    {
        get => Line?.Entry.Carried ?? true;
        set { if (value != Carried) Change(e => e.Carried = value); }
    }

    public bool Paid
    {
        get => Line?.Entry.Paid ?? true;
        set { if (value != Paid) Change(e => e.Paid = value); }
    }

    protected EquipmentLine? Line { get; private set; }

    /// <summary>Gleicher Wert wie in den Regeln → kein eigener Wert.</summary>
    private static decimal? Override(decimal? value, decimal? rule) => value == rule ? null : value;

    protected void Change(Action<EquipmentEntry> change)
    {
        var index = Index;
        Editor.ChangeEquipment(list => change(list[index]));
    }

    [RelayCommand]
    private void ToggleDetails() => ShowDetails = !ShowDetails;

    [RelayCommand]
    private void Increase() => Change(e => e.Quantity++);

    [RelayCommand]
    private void Decrease() => Change(e => e.Quantity = Math.Max(1, e.Quantity - 1));

    [RelayCommand]
    private void Remove()
    {
        var index = Index;
        Editor.ChangeEquipment(list => list.RemoveAt(index));
    }

    protected void UpdateLine(EquipmentLine line, CharacterState state)
    {
        Line = line;
        Index = line.Index;
        Quantity = line.Quantity;
        var parts = new List<string>();
        if (line.Weight is { } w) parts.Add(CharacterCalculator.FormatWeight(w));
        if (line.Price is { } p) parts.Add(CharacterCalculator.FormatMoney(p));
        if (!line.Entry.Carried) parts.Add("nicht dabei");
        if (!line.Entry.Paid) parts.Add("nicht gekauft");
        WeightPriceText = (line.Quantity != 1 ? $"{line.Quantity} × " : "") + string.Join(" · ", parts);
        OnPropertyChanged(nameof(WeightInput));
        OnPropertyChanged(nameof(PriceInput));
        OnPropertyChanged(nameof(RuleValuesText));
        OnPropertyChanged(nameof(Carried));
        OnPropertyChanged(nameof(Paid));
        var messages = state.IssuesFor(CharacterCalculator.EquipmentTarget(Index)).Select(i => i.Message).ToList();
        IssueText = messages.Count > 0 ? string.Join(Environment.NewLine, messages) : null;
    }

    protected static string Join(params string?[] parts) =>
        string.Join(Environment.NewLine + Environment.NewLine, parts.Where(p => !string.IsNullOrWhiteSpace(p)));
}

/// <summary>Sonstiger Gegenstand, Paketzeile oder eigener Gegenstand.</summary>
public sealed partial class ItemRowViewModel(EditorViewModel editor, int index) : EquipmentRowViewModel(editor, index)
{
    [ObservableProperty]
    public partial string Name { get; private set; } = "";

    /// <summary>Kategorie bzw. Gegenstand laut Regeln, wenn der Name abweicht („Kleidung“, „Kletterseil“).</summary>
    [ObservableProperty]
    public partial string? Detail { get; private set; }

    [ObservableProperty]
    public partial string Details { get; private set; } = "";

    /// <summary>Name änderbar (beim Verlassen des Felds übernommen); leer = Name laut Regeln.</summary>
    public string NameInput
    {
        get => Name;
        set
        {
            var name = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (name == Name) return;
            Change(e => e.Name = name == Line?.Definition?.Name ? null : name);
        }
    }

    public void Update(EquipmentLine line, CharacterState state)
    {
        UpdateLine(line, state);
        Name = line.Name;
        OnPropertyChanged(nameof(NameInput));
        var d = line.Definition as ItemDefinition;
        Detail = d is null ? "Eigener Gegenstand" : d.Name != line.Name ? $"{d.Category} · {d.Name}" : d.Category;
        Details = Join(d?.RuleText, d?.Note,
            d?.StructurePoints is { } sp ? "Strukturpunkte: " + sp : null, d?.Complexity is { } c ? "Komplexität: " + c : null);
    }
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

    public void Update(ArmorState armor, EquipmentLine line, CharacterState character)
    {
        state = armor;
        UpdateLine(line, character);
        var d = armor.Definition;
        Detail = (d.Category != d.Name ? $"{d.Category} · " : "") + $"RS {d.Protection} · BE {d.Encumbrance}" +
                 (d.ExtraPenaltiesText is { } x ? $" · {x}" : "");
        Details = Join(d.Note, d.AdvantageText is { } a ? "Rüstungsvorteil: " + a : null,
            d.DisadvantageText is { } n ? "Rüstungsnachteil: " + n : null,
            line.Entry.Equipped ? "Angelegt: das Gewicht zählt nicht zur Traglast." : null);
        OnPropertyChanged(nameof(Worn));
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

    /// <summary>„PA +1 durch Linkhand“ bzw. beim Schild „Parade mit doppeltem Parade-Bonus (+2)“.</summary>
    [ObservableProperty]
    public partial string? BonusText { get; private set; }

    [ObservableProperty]
    public partial string Details { get; private set; } = "";

    /// <summary>Geführt: nur geführte Schilde und Parierwaffen geben ihren Bonus an die Hauptwaffe.</summary>
    public bool Wielded
    {
        get => state.Entry.Equipped;
        set { if (value != state.Entry.Equipped) Change(e => e.Equipped = value); }
    }

    public void Update(WeaponState weapon, EquipmentLine line, CharacterState character)
    {
        state = weapon;
        UpdateLine(line, character);
        OnPropertyChanged(nameof(Wielded));
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
        var bonus = new List<string>();
        if (weapon.ParryBonusFrom is { } off && weapon.Parry is not null)
            bonus.Add($"PA +{off.ParryBonus} durch {off.Name}");
        if (weapon.ShieldAttackModifier != 0) bonus.Add($"AT {weapon.ShieldAttackModifier:+0;−0} durch Schild");
        if (d.Shield && d.ParryModifier is > 0 and var pm) bonus.Add($"Parade mit doppeltem Parade-Bonus ({2 * pm:+0;−0})");
        if (d.ParryBonus is { } pb)
            bonus.Add($"zusätzlich geführt: PA {pb:+0;−0} für die Hauptwaffe" +
                      (d.MainWeaponAttackModifier is { } ma ? $", AT {ma:+0;−0}" : ""));
        BonusText = bonus.Count > 0 ? string.Join(" · ", bonus) : null;
        Details = Join(
            d.DamageThresholds is { Count: > 0 } s
                ? $"Schaden: {d.DamageText}, Schadensbonus +{weapon.DamageBonus} (Schadensschwelle " +
                  string.Join(", ", s.Select(t => $"{t.Key} {t.Value}")) + ")" +
                  (d.AttackModifier is { } am ? $" · AT/PA-Mod {am:+0;−0}/" + (d.NoParry ? "–" : $"{d.ParryModifier ?? 0:+0;−0}") : "")
                : null,
            d.Note, d.AdvantageText is { } a ? "Waffenvorteil: " + a : null,
            d.DisadvantageText is { } n ? "Waffennachteil: " + n : null, d.Ammunition is { } m ? "Munition: " + m : null,
            d.Length is { } len ? "Länge: " + len : null);
    }
}
