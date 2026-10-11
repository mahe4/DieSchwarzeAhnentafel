using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.Core.Sheet;

/// <summary>
/// Charakterbogen: alle Werte des Helden als fertige Texte, gemeinsame Grundlage für die Ansicht in der App und den
/// PDF-Export. Reine Funktion über Held, Berechnung und Regeldaten.
/// </summary>
public sealed record CharacterSheet(
    string Name,
    IReadOnlyList<SheetField> Header,
    IReadOnlyList<SheetValue> Attributes,
    IReadOnlyList<SheetValue> Derived,
    IReadOnlyList<string> Advantages,
    IReadOnlyList<string> Disadvantages,
    IReadOnlyList<SheetGroup<string>> SpecialAbilities,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> Scripts,
    IReadOnlyList<SheetGroup<SheetSkill>> Skills,
    IReadOnlyList<SheetCombatTechnique> CombatTechniques,
    IReadOnlyList<SheetWeapon> Weapons,
    IReadOnlyList<SheetArmor> Armors,
    string? EncumbranceText,
    IReadOnlyList<SheetGroup<SheetSpell>> Spells,
    IReadOnlyList<SheetItem> Items,
    IReadOnlyList<SheetField> Possessions)
{
    /// <summary>Persönliche Angaben (nur ausgefüllte) und Sozialstatus.</summary>
    public IReadOnlyList<SheetField> Personal { get; init; } = [];

    /// <summary>„Schmerz I bei 24 LeP oder weniger, II bei 16, III bei 8, eine weitere Stufe bei 5“.</summary>
    public string? PainText { get; init; }

    public string? Notes { get; init; }

    /// <summary>Abenteuerpunkte für Kästchen (gesamt, verfügbar, ausgegeben) und Erfahrungsgrad.</summary>
    public SheetAp Ap { get; init; } = new("", 0, 0, 0);

    /// <summary>Lebens-, Astral- und Karmaenergie mit Zukauf; Seelenkraft bis Geschwindigkeit mit Rechenweg.</summary>
    public IReadOnlyList<SheetEnergy> Energies { get; init; } = [];

    /// <summary>LeP-Grenzen der Schmerzstufen: „≤ 16“ → „¼ verloren: Schmerz I“.</summary>
    public IReadOnlyList<SheetField> PainLevels { get; init; } = [];

    public IReadOnlyList<SheetMeleeWeapon> MeleeWeapons { get; init; } = [];

    public IReadOnlyList<SheetRangedWeapon> RangedWeapons { get; init; } = [];

    /// <summary>Schilde und Parierwaffen (mit passivem Parade-Bonus).</summary>
    public IReadOnlyList<SheetShield> Shields { get; init; } = [];

    public SheetTradition? MagicTradition { get; init; }

    public SheetTradition? KarmaTradition { get; init; }

    /// <summary>Geld in Münzen: Dukaten, Silbertaler, Heller, Kreuzer; leer bei Schulden.</summary>
    public IReadOnlyList<SheetValue> Coins { get; init; } = [];

    /// <summary>Traglast in Stein (KK × 2).</summary>
    public int CarryCapacity { get; init; }

    public static CharacterSheet Create(Character character, CharacterState state, RuleData rules) =>
        new SheetBuilder(character, state, rules).Build();
}

/// <summary>Beschriftetes Feld: „Spezies: Mensch“.</summary>
public sealed record SheetField(string Label, string Value);

/// <summary>Wert mit Kurzname: „MU 14“, „LeP 32“; <paramref name="Note"/> z. B. „mit Rüstung 5“.</summary>
public sealed record SheetValue(string Short, string Name, string Value, string? Note = null);

public sealed record SheetGroup<T>(string Title, IReadOnlyList<T> Items);

/// <param name="Check">„MU/IN/GE (14/13/12)“.</param>
/// <param name="Encumbrance">„ja“, „nein“, „evtl.“.</param>
public sealed record SheetSkill(string Name, string Check, string Encumbrance, string Factor, int Value);

/// <param name="Parry">PA bzw. FK; „–“ ohne Parade.</param>
public sealed record SheetCombatTechnique(string Name, string PrimaryAttributes, string Factor, int Value, string Attack, string Parry);

/// <param name="Values">„AT 14 · PA 8“ bzw. „FK 13“.</param>
public sealed record SheetWeapon(string Name, string CombatTechnique, string Values, string Damage, string? Reach, string? Note);

public sealed record SheetArmor(string Name, int Protection, int Encumbrance, string? Extra, bool Worn)
{
    public string? Weight { get; init; }
}

/// <param name="Details">„8 AsP · 2 Aktionen · 16 Schritt · sofort“.</param>
public sealed record SheetSpell(string Name, string? Check, string Value, string? Factor, string? Property, string Details)
{
    public string? Cost { get; init; }

    public string? CastingTime { get; init; }

    public string? Range { get; init; }

    public string? Duration { get; init; }
}

public sealed record SheetItem(string Name, int Quantity, string? Weight, bool Carried)
{
    /// <summary>Preis je Stück, z. B. „45 S“.</summary>
    public string? Price { get; init; }
}

/// <param name="ExperienceLevel">Name des Erfahrungsgrads.</param>
public sealed record SheetAp(string ExperienceLevel, int Total, int Remaining, int Spent);

/// <param name="Formula">Rechenweg wie auf dem Heldenbogen, z. B. „GW der Spezies + KO + KO“.</param>
/// <param name="Bought">Zugekaufte Punkte; <c>null</c>, wenn es keinen Zukauf gibt.</param>
/// <param name="Lost">Permanent gebundene (z. B. Bindung des Stabes) oder verlorene AsP/KaP; <c>null</c>, wenn nicht zutreffend.</param>
public sealed record SheetEnergy(string Short, string Name, string Formula, int Value, int? Bought = null, int? Lost = null,
    string? Note = null);

/// <param name="DamageThreshold">Leiteigenschaft und Schadensschwelle, z. B. „GE 14“.</param>
/// <param name="Modifiers">AT/PA-Modifikator der Waffe, z. B. „0/−1“.</param>
public sealed record SheetMeleeWeapon(string Name, string CombatTechnique, string? DamageThreshold, string Damage,
    string Modifiers, string? Reach, string Attack, string Parry, string? Weight, string? Note);

public sealed record SheetRangedWeapon(string Name, string CombatTechnique, string? LoadTime, string Damage,
    string? Ammunition, string? Ranges, string RangedAttack, string? Weight, string? Note);

/// <param name="ParryBonus">Passiver Parade-Bonus für die Hauptwaffe, z. B. „+1“.</param>
public sealed record SheetShield(string Name, string Modifiers, string Attack, string Parry, string? ParryBonus,
    string? Weight, string? Note);

/// <param name="Properties">Merkmale (Merkmalskenntnis) bzw. Aspekte (Aspektkenntnis).</param>
public sealed record SheetTradition(string Name, string? PrimaryAttribute, IReadOnlyList<string> Properties);

/// <summary>Baut den Bogen aus dem berechneten Zustand.</summary>
public sealed class SheetBuilder(Character character, CharacterState state, RuleData rules)
{
    public CharacterSheet Build() => new(
        string.IsNullOrWhiteSpace(character.Name) ? "Unbenannter Held" : character.Name,
        Header(), Attributes(), Derived(),
        TraitNames(state.Traits.Where(t => !t.Definition.IsDisadvantage)),
        TraitNames(state.Traits.Where(t => t.Definition.IsDisadvantage)),
        SpecialAbilities(), Languages(), Scripts(), Skills(), CombatTechniques(), Weapons(), Armors(), EncumbranceText(),
        Spells(), Items(), Possessions())
    {
        Personal = Personal(),
        PainText = PainText(),
        Notes = string.IsNullOrWhiteSpace(character.Personal.Notes) ? null : character.Personal.Notes.Trim(),
        Ap = new SheetAp(state.ExperienceLevel.Name, state.Ap.Total, state.Ap.Remaining, state.Ap.Spent),
        Energies = Energies(),
        PainLevels = PainLevels(),
        MeleeWeapons = MeleeWeapons(),
        RangedWeapons = RangedWeapons(),
        Shields = Shields(),
        MagicTradition = Tradition(AbilityDomain.Magical, TraitOptionTarget.Property),
        KarmaTradition = Tradition(AbilityDomain.Karmal, TraitOptionTarget.Aspect),
        Coins = Coins(state.Equipment.Money),
        CarryCapacity = state.Equipment.CarryCapacity,
    };

    private IReadOnlyList<SheetEnergy> Energies()
    {
        var d = state.Derived;
        var eq = state.Equipment;
        int? Energy(string id) => state.Energies.FirstOrDefault(e => e.Id == id) is { Available: true } e ? e.Value : null;
        var list = new List<SheetEnergy>();
        if (d.LifePoints is { } lep)
            list.Add(new("LeP", "Lebensenergie", "GW der Spezies + KO + KO", lep, Energy(Characters.Energies.LifePoints) ?? 0));
        if (d.Astral is { } asp)
            list.Add(new("AsP", "Astralenergie", "20 durch Zauberer + Leiteigenschaft", asp, Energy(Characters.Energies.Astral) ?? 0,
                Energy(Characters.Energies.AstralLost) ?? 0));
        if (d.Karma is { } kap)
            list.Add(new("KaP", "Karmaenergie", "20 durch Geweihter + Leiteigenschaft", kap, Energy(Characters.Energies.Karma) ?? 0,
                Energy(Characters.Energies.KarmaLost) ?? 0));
        if (d.Spirit is { } sk) list.Add(new("SK", "Seelenkraft", "GW der Spezies + (MU + KL + IN)/6", sk));
        if (d.Toughness is { } zk) list.Add(new("ZK", "Zähigkeit", "GW der Spezies + (KO + KO + KK)/6", zk));
        list.Add(new("AW", "Ausweichen", "GE/2", d.Dodge, Note: eq.Dodge != d.Dodge ? $"mit Belastung {eq.Dodge}" : null));
        list.Add(new("INI", "Initiative", "(MU + GE)/2", d.Initiative,
            Note: eq.Initiative != d.Initiative ? $"mit Belastung {eq.Initiative}" : null));
        if (d.Speed is { } gs)
            list.Add(new("GS", "Geschwindigkeit", "GW der Spezies", gs, Note: eq.Speed is { } s && s != gs ? $"mit Belastung {s}" : null));
        return list;
    }

    private IReadOnlyList<SheetField> PainLevels()
    {
        if (state.Derived.LifePoints is not { } lep) return [];
        var t = PainRules.Thresholds(lep);
        return
        [
            new($"≤ {t[0]}", "¼ verloren: Schmerz I"),
            new($"≤ {t[1]}", "½ verloren: Schmerz II"),
            new($"≤ {t[2]}", "¾ verloren: Schmerz III"),
            new($"≤ {t[3]}", "Schmerz IV"),
            new("≤ 0", "Held liegt im Sterben"),
        ];
    }

    private static string Modifiers(WeaponDefinition d) =>
        $"{Signed(d.AttackModifier ?? 0)}/{(d.NoParry ? "–" : Signed(d.ParryModifier ?? 0))}";

    private static string Signed(int value) => value > 0 ? $"+{value}" : value < 0 ? $"−{-value}" : "0";

    private static bool IsShield(WeaponDefinition d) => d.Shield || d.ParryBonus is not null;

    private string CombatTechniqueName(WeaponDefinition d) => rules.FindCombatTechnique(d.CombatTechnique)?.Name ?? d.Category;

    private static string? WeaponNote(WeaponState w)
    {
        var notes = new List<string>();
        if (w.ParryBonusFrom is { } off && w.Parry is not null) notes.Add($"PA +{off.ParryBonus} durch {off.Name}");
        if (w.ShieldAttackModifier != 0) notes.Add($"AT {Signed(w.ShieldAttackModifier)} durch Schild");
        if (!w.Entry.Equipped) notes.Add("nicht geführt");
        return notes.Count > 0 ? string.Join(", ", notes) : null;
    }

    private IReadOnlyList<SheetMeleeWeapon> MeleeWeapons() =>
        state.Equipment.Weapons.Where(w => !w.Definition.Ranged && !IsShield(w.Definition)).Select(w =>
        {
            var d = w.Definition;
            var threshold = d.DamageThresholds is { Count: > 0 } t ? string.Join("/", t.Keys) + " " + string.Join("/", t.Values) : null;
            return new SheetMeleeWeapon(w.Entry.Name ?? d.Name, CombatTechniqueName(d), threshold, w.DamageText, Modifiers(d), d.Reach,
                w.Attack?.ToString() ?? "–", w.Parry?.ToString() ?? "–", d.Weight, WeaponNote(w));
        }).ToList();

    private IReadOnlyList<SheetRangedWeapon> RangedWeapons() =>
        state.Equipment.Weapons.Where(w => w.Definition.Ranged).Select(w =>
        {
            var d = w.Definition;
            return new SheetRangedWeapon(w.Entry.Name ?? d.Name, CombatTechniqueName(d), d.LoadTime, w.DamageText, d.Ammunition,
                d.Ranges, w.RangedAttack?.ToString() ?? "–", d.Weight, WeaponNote(w));
        }).ToList();

    private IReadOnlyList<SheetShield> Shields() =>
        state.Equipment.Weapons.Where(w => !w.Definition.Ranged && IsShield(w.Definition)).Select(w =>
        {
            var d = w.Definition;
            return new SheetShield(w.Entry.Name ?? d.Name, Modifiers(d), w.Attack?.ToString() ?? "–", w.Parry?.ToString() ?? "–",
                d.ParryBonus is { } b ? Signed(b) : null, d.Weight, WeaponNote(w));
        }).ToList();

    private SheetTradition? Tradition(AbilityDomain domain, TraitOptionTarget knowledge)
    {
        var traditions = state.SpecialAbilities.Where(t => t.Definition.Tradition?.Kind == domain).ToList();
        if (traditions.Count == 0) return null;
        var primary = traditions.Select(t => t.Definition.Tradition!).FirstOrDefault(t => t.PrimaryAttribute is not null);
        var properties = state.SpecialAbilities.Where(t => t.Definition.OptionTarget == knowledge && t.Entry.Option is not null)
            .Select(t => t.Entry.Option!).Distinct().ToList();
        return new SheetTradition(string.Join(", ", traditions.Select(TraitName)),
            primary is null ? null : primary.PrimaryAttribute + (primary.HalfPrimaryAttribute ? " (halb)" : ""), properties);
    }

    /// <summary>Wiki (Währung): 1 Dukat = 10 Silbertaler = 100 Heller = 1000 Kreuzer.</summary>
    public static IReadOnlyList<SheetValue> Coins(decimal silver)
    {
        if (silver < 0) return [];
        var kreuzer = (long)Math.Round(silver * 100);
        return
        [
            new("D", "Dukaten", (kreuzer / 1000).ToString()),
            new("S", "Silbertaler", (kreuzer / 100 % 10).ToString()),
            new("H", "Heller", (kreuzer / 10 % 10).ToString()),
            new("K", "Kreuzer", (kreuzer % 10).ToString()),
        ];
    }

    private IReadOnlyList<SheetField> Personal()
    {
        var p = character.Personal;
        var fields = new List<SheetField>();
        void Add(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) fields.Add(new SheetField(label, value.Trim()));
        }
        Add("Familie", p.Family);
        Add("Geburtsort", p.Birthplace);
        Add("Geburtsdatum", p.Birthdate);
        Add("Alter", p.Age);
        Add("Geschlecht", p.Gender);
        Add("Größe", p.Height);
        Add("Gewicht", p.Weight);
        Add("Haarfarbe", p.HairColor);
        Add("Augenfarbe", p.EyeColor);
        Add("Titel", p.Title);
        Add("Sozialstatus", SocialStatus());
        Add("Charakteristika", p.Characteristics);
        return fields;
    }

    /// <summary>Wiki (Sozialer Stand): ohne Vorteil Adel und Nachteil Unfrei gilt ein Held als frei.</summary>
    private string SocialStatus()
    {
        var noble = state.Traits.FirstOrDefault(t => t.Definition.Id == "adv.adel");
        if (noble is not null) return TraitName(noble);
        return state.Traits.Any(t => t.Definition.Id == "disadv.unfrei") ? "Unfrei" : "Frei";
    }

    private string? PainText()
    {
        if (state.Derived.LifePoints is not { } lep) return null;
        var t = PainRules.Thresholds(lep);
        return $"Schmerz I bei {t[0]} LeP oder weniger, II bei {t[1]}, III bei {t[2]}, eine weitere Stufe bei {t[3]}";
    }

    private IReadOnlyList<SheetField> Header()
    {
        var o = state.Origin;
        var ap = state.Ap;
        var fields = new List<SheetField>
        {
            new("Spezies", o.Species?.Name ?? "–"),
            new("Kultur", o.Culture?.Name ?? "–"),
            new("Profession", o.Profession is null ? "–" : o.Variant is { } v ? $"{o.Profession.Name} ({v.Name})" : o.Profession.Name),
            new("Erfahrungsgrad", state.ExperienceLevel.Name),
            new("Abenteuerpunkte", $"{ap.Total} gesamt · {ap.Spent} ausgegeben · {ap.Remaining} verfügbar"),
        };
        if (character.Phase != CharacterPhase.Creation) fields.Add(new("Phase", "Erschaffung abgeschlossen"));
        return fields;
    }

    private IReadOnlyList<SheetValue> Attributes() =>
        state.Attributes.Select(a => new SheetValue(a.Definition.Id, a.Definition.Name, a.Value.ToString())).ToList();

    private IReadOnlyList<SheetValue> Derived()
    {
        var d = state.Derived;
        var eq = state.Equipment;
        var list = new List<SheetValue>();
        void Add(string shortName, string name, int? value, int? withGear = null)
        {
            if (value is null) return;
            list.Add(new SheetValue(shortName, name, value.ToString()!,
                withGear is { } g && g != value ? $"mit Belastung {g}" : null));
        }
        Add("LeP", "Lebensenergie", d.LifePoints);
        Add("AsP", "Astralenergie", d.Astral);
        Add("KaP", "Karmaenergie", d.Karma);
        Add("SK", "Seelenkraft", d.Spirit);
        Add("ZK", "Zähigkeit", d.Toughness);
        Add("AW", "Ausweichen", d.Dodge, eq.Dodge);
        Add("INI", "Initiative", d.Initiative, eq.Initiative);
        Add("GS", "Geschwindigkeit", d.Speed, eq.Speed);
        Add("SchiP", "Schicksalspunkte", d.FatePoints);
        list.Add(new SheetValue("RS", "Rüstungsschutz", eq.Protection.ToString()));
        list.Add(new SheetValue("BE", "Belastung", eq.Encumbrance.ToString(),
            eq.Encumbrance != eq.ArmorEncumbrance && eq.WornArmor is not null ? $"Rüstung {eq.ArmorEncumbrance}" : null));
        return list;
    }

    /// <summary>„Begabung (Klettern)“, „Hohe Lebenskraft III“, „Angst vor … (Spinnen)“.</summary>
    private string TraitName(TraitState t)
    {
        var def = t.Definition;
        var option = OptionName(def, t.Entry.Option);
        var extras = new[] { option, t.Entry.Specification }.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        return def.Name + (extras.Count > 0 ? $" ({string.Join(", ", extras)})" : "") +
               (def.MaxLevel is not null ? " " + TraitRules.Roman(t.Level) : "");
    }

    private string? OptionName(TraitDefinition def, string? option) => option is null ? null : def.OptionTarget switch
    {
        TraitOptionTarget.Skill => rules.FindSkill(option)?.Name ?? rules.FindSpell(option)?.Name ?? option,
        TraitOptionTarget.CombatTechnique => rules.FindCombatTechnique(option)?.Name ?? option,
        TraitOptionTarget.Language => rules.FindLanguage(option)?.Name ?? option,
        TraitOptionTarget.Script => rules.FindScript(option)?.Name ?? option,
        _ => option,
    };

    private IReadOnlyList<string> TraitNames(IEnumerable<TraitState> traits) => traits.Select(TraitName).ToList();

    private static bool IsLanguage(TraitDefinition d) => d.OptionTarget == TraitOptionTarget.Language;

    private static bool IsScript(TraitDefinition d) => d.OptionTarget == TraitOptionTarget.Script;

    private IReadOnlyList<SheetGroup<string>> SpecialAbilities() =>
        state.SpecialAbilities.Where(t => !IsLanguage(t.Definition) && !IsScript(t.Definition))
            .GroupBy(t => t.Definition.Domain switch
            {
                AbilityDomain.Magical => "Magische Sonderfertigkeiten",
                AbilityDomain.Karmal => "Karmale Sonderfertigkeiten",
                _ => t.Definition.Category?.Contains("Kampf", StringComparison.Ordinal) == true
                    ? "Kampfsonderfertigkeiten" : "Allgemeine Sonderfertigkeiten",
            })
            .OrderBy(g => g.Key switch
            {
                "Allgemeine Sonderfertigkeiten" => 0, "Kampfsonderfertigkeiten" => 1, "Magische Sonderfertigkeiten" => 2, _ => 3,
            })
            .Select(g => new SheetGroup<string>(g.Key, g.Select(TraitName).ToList()))
            .ToList();

    /// <summary>„Garethi III“, „Tulamidya I“.</summary>
    private IReadOnlyList<string> Languages() =>
        state.SpecialAbilities.Where(t => IsLanguage(t.Definition))
            .Select(t => (OptionName(t.Definition, t.Entry.Option) ?? "Sprache ohne Auswahl") + " " + TraitRules.Roman(t.Level))
            .ToList();

    private IReadOnlyList<string> Scripts() =>
        state.SpecialAbilities.Where(t => IsScript(t.Definition))
            .Select(t => OptionName(t.Definition, t.Entry.Option) ?? t.Definition.Name)
            .ToList();

    private string CheckText(IReadOnlyList<string> check) =>
        $"{string.Join("/", check)} ({string.Join("/", check.Select(a => state.Attribute(a)))})";

    private IReadOnlyList<SheetGroup<SheetSkill>> Skills() =>
        state.Skills.GroupBy(s => s.Definition.Group)
            .OrderBy(g => g.Key)
            .Select(g => new SheetGroup<SheetSkill>(g.Key switch
            {
                SkillGroup.Body => "Körpertalente",
                SkillGroup.Social => "Gesellschaftstalente",
                SkillGroup.Nature => "Naturtalente",
                SkillGroup.Knowledge => "Wissenstalente",
                _ => "Handwerkstalente",
            }, g.Select(s => new SheetSkill(s.Definition.Name, CheckText(s.Definition.Check),
                s.Definition.Encumbrance switch { Encumbrance.Yes => "ja", Encumbrance.No => "nein", _ => "evtl." },
                s.Definition.ImprovementCost.ToString(), s.Value)).ToList()))
            .ToList();

    /// <summary>Nahkampf vor Fernkampf, jeweils alphabetisch.</summary>
    private IReadOnlyList<SheetCombatTechnique> CombatTechniques() =>
        state.CombatTechniques.OrderBy(c => c.RangedAttack is not null)
            .ThenBy(c => c.Definition.Name, StringComparer.CurrentCulture)
            .Select(c => new SheetCombatTechnique(c.Definition.Name,
                string.Join("/", c.Definition.PrimaryAttributes), c.Definition.ImprovementCost.ToString(), c.Value,
                c.Attack?.ToString() ?? "–",
                c.RangedAttack is { } fk ? $"FK {fk}" : c.Parry?.ToString() ?? "–"))
            .ToList();

    private IReadOnlyList<SheetWeapon> Weapons() =>
        state.Equipment.Weapons.Select(w =>
        {
            var d = w.Definition;
            var values = string.Join(" · ", new[]
            {
                w.Attack is { } at ? $"AT {at}" : null, w.Parry is { } pa ? $"PA {pa}" : null,
                w.RangedAttack is { } fk ? $"FK {fk}" : null,
            }.OfType<string>());
            var notes = new List<string>();
            if (w.ParryBonusFrom is { } off && w.Parry is not null) notes.Add($"PA +{off.ParryBonus} durch {off.Name}");
            if (w.ShieldAttackModifier != 0) notes.Add($"AT {w.ShieldAttackModifier:+0;−0} durch Schild");
            if (!w.Entry.Equipped) notes.Add("nicht geführt");
            return new SheetWeapon(w.Entry.Name ?? d.Name, rules.FindCombatTechnique(d.CombatTechnique)?.Name ?? d.Category,
                values, w.DamageText, d.Reach is { Length: > 0 } rw ? rw : d.Ranges,
                notes.Count > 0 ? string.Join(", ", notes) : null);
        }).ToList();

    private IReadOnlyList<SheetArmor> Armors() =>
        state.Equipment.Armors.Select(a => new SheetArmor(a.Entry.Name ?? a.Definition.Name, a.Definition.Protection,
            a.Definition.Encumbrance, a.Definition.ExtraPenaltiesText, a.Entry.Equipped) { Weight = a.Definition.Weight }).ToList();

    private string? EncumbranceText()
    {
        var eq = state.Equipment;
        if (eq.Encumbrance == 0 && eq.WornArmor is null) return null;
        var parts = new List<string>();
        if (eq.WornArmor is not null)
            parts.Add($"Rüstung {eq.ArmorEncumbrance}" + (eq.Habituation > 0 ? $", Belastungsgewöhnung {TraitRules.Roman(eq.Habituation)}" : ""));
        if (eq.LoadLevel > 0) parts.Add($"Traglast {eq.LoadLevel}");
        return $"Belastung {eq.Encumbrance}" + (parts.Count > 0 ? $" ({string.Join(", ", parts)})" : "") +
               (eq.Encumbrance > 0 ? $": AT, Verteidigung, INI, GS und Talente mit Belastung −{eq.Encumbrance}" : "");
    }

    internal static readonly (SpellGroup Group, string Title)[] SpellGroupTitles =
    [
        (SpellGroup.Spell, "Zauber"), (SpellGroup.Ritual, "Rituale"), (SpellGroup.MagicalAction, "Magische Handlungen"),
        (SpellGroup.Cantrip, "Zaubertricks"), (SpellGroup.Liturgy, "Liturgien"), (SpellGroup.Ceremony, "Zeremonien"),
        (SpellGroup.Blessing, "Segnungen"),
    ];

    private IReadOnlyList<SheetGroup<SheetSpell>> Spells() =>
        SpellGroupTitles.Select(g => new SheetGroup<SheetSpell>(g.Title, state.Spells
                .Where(s => s.Definition.Group == g.Group)
                .OrderBy(s => s.Definition.Name, StringComparer.CurrentCulture)
                .Select(Spell).ToList()))
            .Where(g => g.Items.Count > 0)
            .ToList();

    private SheetSpell Spell(SpellState s)
    {
        var d = s.Definition;
        var factor = d.FactorFor(s.Entry.Option);
        var fields = SpellFields(d.DetailsText);
        return new SheetSpell(d.Name + (s.Entry.Option is { } o ? $" ({o})" : ""),
            d.Check is { } check ? CheckText(check) : d.CheckText,
            s.Value?.ToString() ?? "–", factor?.ToString(), d.Property ?? d.Aspect, SpellDetails(d.DetailsText))
        {
            Cost = fields.Cost, CastingTime = fields.CastingTime, Range = fields.Range, Duration = fields.Duration,
        };
    }

    /// <summary>Kosten, Zauberdauer, Reichweite und Wirkungsdauer aus den Wiki-Feldern, kurz hintereinander.</summary>
    public static string SpellDetails(string? details)
    {
        var f = SpellFields(details);
        return string.Join(" · ", new[]
        {
            f.Cost, f.CastingTime, f.Range is { } rw ? "RW " + rw : null, f.Duration is { } wd ? "WD " + wd : null,
        }.OfType<string>());
    }

    /// <summary>Kosten (mit Einheit), Zauber-/Liturgiedauer, Reichweite und Wirkungsdauer aus den Wiki-Feldern.</summary>
    public static (string? Cost, string? CastingTime, string? Range, string? Duration) SpellFields(string? details)
    {
        if (details is null) return default;
        var fields = details.Split('\n')
            .Select(l => l.Split(':', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.Ordinal);
        string? Get(params string[] keys) => keys.Select(k => fields.GetValueOrDefault(k)).FirstOrDefault(v => !string.IsNullOrEmpty(v));
        var cost = Get("AsP-Kosten", "KaP-Kosten");
        var unit = fields.ContainsKey("KaP-Kosten") ? "KaP" : "AsP";
        return (cost is null ? null : WithUnit(cost, unit), Get("Zauberdauer", "Liturgiedauer", "Ritualdauer", "Zeremoniedauer"),
            Get("Reichweite"), Get("Wirkungsdauer"));
    }

    /// <summary>„8 (Kosten sind nicht modifizierbar)“ → „8 AsP (Kosten …)“; „8 AsP …“ bleibt.</summary>
    private static string WithUnit(string cost, string unit) =>
        cost.Contains(unit, StringComparison.Ordinal) ? cost
        : System.Text.RegularExpressions.Regex.IsMatch(cost, @"^\d+") ? System.Text.RegularExpressions.Regex.Replace(cost, @"^\d+", $"$0 {unit}")
        : $"{unit}: {cost}";

    private IReadOnlyList<SheetItem> Items() =>
        state.Equipment.Lines.Where(l => l.Definition is not (WeaponDefinition or ArmorDefinition))
            .Select(l => new SheetItem(l.Name, l.Quantity,
                l.Weight is { } w ? CharacterCalculator.FormatWeight(w * l.Quantity) : null, l.Entry.Carried)
            {
                Price = l.Price is { } p ? CharacterCalculator.FormatMoney(p) : null,
            })
            .ToList();

    private IReadOnlyList<SheetField> Possessions()
    {
        var eq = state.Equipment;
        return
        [
            new("Geld", CharacterCalculator.FormatMoney(eq.Money)),
            new("Traglast", $"{CharacterCalculator.FormatWeight(eq.CarriedWeight)} von {CharacterCalculator.FormatWeight(eq.CarryCapacity)}" +
                            (eq.LoadLevel > 0 ? $" (Belastung {TraitRules.Roman(eq.LoadLevel)})" : "")),
        ];
    }
}
