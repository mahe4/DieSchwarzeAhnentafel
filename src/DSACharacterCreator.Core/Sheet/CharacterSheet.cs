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

public sealed record SheetArmor(string Name, int Protection, int Encumbrance, string? Extra, bool Worn);

/// <param name="Details">„8 AsP · 2 Aktionen · 16 Schritt · sofort“.</param>
public sealed record SheetSpell(string Name, string? Check, string Value, string? Factor, string? Property, string Details);

public sealed record SheetItem(string Name, int Quantity, string? Weight, bool Carried);

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
    };

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
            a.Definition.Encumbrance, a.Definition.ExtraPenaltiesText, a.Entry.Equipped)).ToList();

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

    private static readonly (SpellGroup Group, string Title)[] SpellGroups =
    [
        (SpellGroup.Spell, "Zauber"), (SpellGroup.Ritual, "Rituale"), (SpellGroup.MagicalAction, "Magische Handlungen"),
        (SpellGroup.Cantrip, "Zaubertricks"), (SpellGroup.Liturgy, "Liturgien"), (SpellGroup.Ceremony, "Zeremonien"),
        (SpellGroup.Blessing, "Segnungen"),
    ];

    private IReadOnlyList<SheetGroup<SheetSpell>> Spells() =>
        SpellGroups.Select(g => new SheetGroup<SheetSpell>(g.Title, state.Spells
                .Where(s => s.Definition.Group == g.Group)
                .OrderBy(s => s.Definition.Name, StringComparer.CurrentCulture)
                .Select(Spell).ToList()))
            .Where(g => g.Items.Count > 0)
            .ToList();

    private SheetSpell Spell(SpellState s)
    {
        var d = s.Definition;
        var factor = d.FactorFor(s.Entry.Option);
        return new SheetSpell(d.Name + (s.Entry.Option is { } o ? $" ({o})" : ""),
            d.Check is { } check ? CheckText(check) : d.CheckText,
            s.Value?.ToString() ?? "–", factor?.ToString(), d.Property ?? d.Aspect, SpellDetails(d.DetailsText));
    }

    /// <summary>Kosten, Zauberdauer, Reichweite und Wirkungsdauer aus den Wiki-Feldern, kurz hintereinander.</summary>
    public static string SpellDetails(string? details)
    {
        if (details is null) return "";
        var fields = details.Split('\n')
            .Select(l => l.Split(':', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.Ordinal);
        string? Get(params string[] keys) => keys.Select(k => fields.GetValueOrDefault(k)).FirstOrDefault(v => !string.IsNullOrEmpty(v));
        var cost = Get("AsP-Kosten", "KaP-Kosten");
        var unit = fields.ContainsKey("KaP-Kosten") ? "KaP" : "AsP";
        return string.Join(" · ", new[]
        {
            cost is null ? null : WithUnit(cost, unit),
            Get("Zauberdauer", "Liturgiedauer", "Ritualdauer", "Zeremoniedauer"),
            Get("Reichweite") is { } rw ? "RW " + rw : null,
            Get("Wirkungsdauer") is { } wd ? "WD " + wd : null,
        }.OfType<string>());
    }

    /// <summary>„8 (Kosten sind nicht modifizierbar)“ → „8 AsP (Kosten …)“; „8 AsP …“ bleibt.</summary>
    private static string WithUnit(string cost, string unit) =>
        cost.Contains(unit, StringComparison.Ordinal) ? cost
        : System.Text.RegularExpressions.Regex.IsMatch(cost, @"^\d+") ? System.Text.RegularExpressions.Regex.Replace(cost, @"^\d+", $"$0 {unit}")
        : $"{unit}: {cost}";

    private IReadOnlyList<SheetItem> Items() =>
        state.Equipment.Lines.Where(l => l.Definition is not (WeaponDefinition or ArmorDefinition))
            .Select(l => new SheetItem(l.Name, l.Quantity,
                l.Weight is { } w ? CharacterCalculator.FormatWeight(w * l.Quantity) : null, l.Entry.Carried))
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
