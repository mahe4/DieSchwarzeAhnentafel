using DSACharacterCreator.Core.Sheet;
using SkiaSharp;

namespace DSACharacterCreator.Pdf;

/// <summary>
/// Charakterbogen als PDF (A4). Aufbau nach dem frei erhältlichen Heldendokument von Ulisses (Persönliche Daten,
/// Spielwerte, Kampf, Besitz, Zauber, Liturgien) mit eigener Gestaltung; leere Felder und Linien bleiben zum
/// Ausfüllen am Spieltisch (aktuelle LeP, Zustände …). Alle Texte kommen aus <see cref="CharacterSheet"/>.
/// </summary>
public static class SheetPdf
{
    /// <summary>Farben der Eigenschaften wie auf den offiziellen Bögen.</summary>
    private static readonly Dictionary<string, SKColor> AttributeColors = new()
    {
        ["MU"] = new SKColor(0xD0, 0x1F, 0x2A), ["KL"] = new SKColor(0x86, 0x3F, 0xB0), ["IN"] = new SKColor(0x2F, 0x9A, 0x4C),
        ["CH"] = new SKColor(0x22, 0x22, 0x22), ["FF"] = new SKColor(0xE2, 0xB4, 0x00), ["GE"] = new SKColor(0x1F, 0x5C, 0xC4),
        ["KO"] = new SKColor(0xA8, 0xA8, 0xA8), ["KK"] = new SKColor(0xE3, 0x6E, 0x1B),
    };

    /// <summary>Zustände (vier Stufen) laut Regel-Wiki, Grundregeln › Zustände.</summary>
    private static readonly string[] Conditions =
        ["Animosität", "Belastung", "Berauscht", "Betäubung", "Entrückung", "Furcht", "Paralyse", "Schmerz", "Trance", "Verwirrung"];

    /// <summary>Allgemeine Status laut Regel-Wiki, Grundregeln › Status.</summary>
    private static readonly string[] States =
    [
        "Bewegungsunfähig", "Bewusstlos", "Blind", "Blutrausch", "Brennend", "Eingeengt", "Fixiert", "Handlungsunfähig", "Hörigkeit",
        "Krank", "Liegend", "Pechmagnet", "Stumm", "Taub", "Überrascht", "Übler Geruch", "Unsichtbar", "Vergiftet", "Versteinert",
    ];

    public static void Write(CharacterSheet sheet, Stream output, PdfFonts fonts, DateTime? created = null)
    {
        var metadata = new SKDocumentPdfMetadata
        {
            Title = $"Heldendokument {sheet.Name}",
            Creator = "DSA5 Character Creator",
            Producer = "DSA5 Character Creator",
            Creation = created ?? DateTime.Now,
            Modified = created ?? DateTime.Now,
        };
        using var pdf = new PdfLayout(output, fonts, $"{sheet.Name} · Heldendokument DSA5", metadata);
        PersonalPage(pdf, sheet);
        SkillsPage(pdf, sheet);
        CombatPage(pdf, sheet);
        PossessionsPage(pdf, sheet);
        if (sheet.MagicTradition is not null || HasSpells(sheet, "Zauber", "Rituale", "Magische Handlungen", "Zaubertricks"))
            MagicPage(pdf, sheet);
        if (sheet.KarmaTradition is not null || HasSpells(sheet, "Liturgien", "Zeremonien", "Segnungen"))
            KarmaPage(pdf, sheet);
        foreach (var companion in sheet.Companions) CompanionPage(pdf, sheet, companion);
    }

    // ---- Kopf -------------------------------------------------------------------------------------------------

    /// <summary>Titel links, Eigenschaften (und weitere Werte) als Kästchen rechts.</summary>
    private static Action<PdfLayout, bool> Header(CharacterSheet sheet, string subtitle,
        IReadOnlyList<(string Label, string Value)>? extra = null) => Header("Heldendokument", sheet.Name, sheet.Attributes, subtitle, extra);

    internal static Action<PdfLayout, bool> Header(string title, string name, IReadOnlyList<SheetValue> attributes, string subtitle,
        IReadOnlyList<(string Label, string Value)>? extra = null) => (pdf, continued) =>
    {
        const float box = 36f, gap = 4f, height = 30f;
        var top = pdf.Y;
        pdf.Text(title, PdfLayout.Margin, top + 17, true, 17f, PdfLayout.Ink);
        pdf.Text(subtitle + (continued ? " (Fortsetzung)" : ""), PdfLayout.Margin, top + 30, false, 9.5f, PdfLayout.Muted);
        pdf.Text(pdf.Fit(name, 200, true, 9.5f), PdfLayout.Margin, top + 43, true, 9.5f, PdfLayout.Ink);
        var right = PdfLayout.Margin + PdfLayout.FullWidth;
        var y = top;
        if (extra is { Count: > 0 })
        {
            var x = right - extra.Count * (box + gap) + gap;
            foreach (var (label, value) in extra)
            {
                pdf.ValueBox(x, y, box, height, label, value);
                x += box + gap;
            }
            y += height + gap;
        }
        var ax = right - attributes.Count * (box + gap) + gap;
        foreach (var a in attributes)
        {
            pdf.ValueBox(ax, y, box, height, a.Short, a.Value, AttributeColors.GetValueOrDefault(a.Short, PdfLayout.Muted));
            ax += box + gap;
        }
        pdf.Y = Math.Max(y + height, top + 46) + 8;
    };

    // ---- Seite 1: Persönliche Daten ---------------------------------------------------------------------------

    private static void PersonalPage(PdfLayout pdf, CharacterSheet sheet)
    {
        pdf.StartPage(Header(sheet, "Persönliche Daten"));
        string? Get(string label) =>
            sheet.Header.Concat(sheet.Personal).FirstOrDefault(f => f.Label == label)?.Value is { } v && v != "–" ? v : null;

        pdf.Columns([64, 36], 16,
            () =>
            {
                pdf.FieldRow(("Name", sheet.Name, 1));
                pdf.FieldRow(("Familie", Get("Familie"), 1), ("Geburtsort", Get("Geburtsort"), 1));
                pdf.FieldRow(("Geburtsdatum", Get("Geburtsdatum"), 1.2f), ("Alter", Get("Alter"), 0.6f), ("Geschlecht", Get("Geschlecht"), 0.9f));
                pdf.FieldRow(("Spezies", Get("Spezies"), 1.2f), ("Größe", Get("Größe"), 0.6f), ("Gewicht", Get("Gewicht"), 0.9f));
                pdf.FieldRow(("Haarfarbe", Get("Haarfarbe"), 1), ("Augenfarbe", Get("Augenfarbe"), 1));
                pdf.FieldRow(("Kultur", Get("Kultur"), 1));
                pdf.FieldRow(("Profession", Get("Profession"), 1));
                pdf.FieldRow(("Titel", Get("Titel"), 1), ("Sozialstatus", Get("Sozialstatus"), 1));
                pdf.FieldRow(("Charakteristika", Get("Charakteristika"), 1));
                pdf.FieldRow(("Sonstiges", null, 1));
            },
            () =>
            {
                pdf.FieldRow(("Erfahrungsgrad", sheet.Ap.ExperienceLevel, 1));
                const float gap = 6f;
                var w = (pdf.W - 2 * gap) / 3;
                pdf.ValueBox(pdf.X, pdf.Y, w, 32, "AP gesamt", sheet.Ap.Total.ToString());
                pdf.ValueBox(pdf.X + w + gap, pdf.Y, w, 32, "AP verfügbar", sheet.Ap.Remaining.ToString());
                pdf.ValueBox(pdf.X + 2 * (w + gap), pdf.Y, w, 32, "AP ausgegeben", sheet.Ap.Spent.ToString());
                pdf.Y += 38;
                pdf.Box("Porträt/Wappen", 82);
            });

        pdf.Columns([54, 46], 16,
            () =>
            {
                pdf.Heading("Vorteile");
                pdf.Lines(sheet.Advantages, 6);
                pdf.Heading("Nachteile");
                pdf.Lines(sheet.Disadvantages, 6);
                pdf.Heading("Allgemeine Sonderfertigkeiten");
                pdf.Lines(Group(sheet, "Allgemeine Sonderfertigkeiten"), 8, fill: true);
            },
            () =>
            {
                pdf.Heading("Energien und Basiswerte");
                pdf.Table(
                    [new("Wert (Rechenweg)", 46), new("Max", 9, true), new("Zukauf", 11, true), new("perm. gebunden/ verloren", 14, true), new("Aktuell", 14)],
                    sheet.Energies.Select(e => (IReadOnlyList<string>)
                    [
                        $"{e.Name} ({e.Formula})" + (e.Note is { } n ? $", {n}" : ""), e.Value.ToString(),
                        e.Bought?.ToString() ?? "", e.Lost?.ToString() ?? "", "",
                    ]));
                if (sheet.Derived.FirstOrDefault(v => v.Short == "SchiP") is { } fate)
                {
                    pdf.Heading("Schicksalspunkte", keepWith: 34);
                    const float gap = 6f;
                    var w = (pdf.W - 2 * gap) / 3;
                    pdf.ValueBox(pdf.X, pdf.Y, w, 30, "Wert", fate.Value);
                    pdf.ValueBox(pdf.X + w + gap, pdf.Y, w, 30, "Max", fate.Value);
                    pdf.ValueBox(pdf.X + 2 * (w + gap), pdf.Y, w, 30, "Aktuell", "");
                    pdf.Y += 36;
                }
            });
    }

    // ---- Seite 2: Spielwerte ----------------------------------------------------------------------------------

    private static void SkillsPage(PdfLayout pdf, CharacterSheet sheet)
    {
        pdf.StartPage(Header(sheet, "Spielwerte"));
        pdf.Heading("Fertigkeiten");
        void Groups(IEnumerable<SheetGroup<SheetSkill>> groups)
        {
            foreach (var g in groups)
            {
                pdf.Table(
                    [new(g.Title, 33), new("Probe", 29), new("BE", 9), new("Sf.", 5), new("FW", 6, true)],
                    g.Items.Select(s => (IReadOnlyList<string>)[s.Name, s.Check, s.Encumbrance, s.Factor, s.Value.ToString()]),
                    size: 7.4f, stripes: true);
                pdf.Y += 3;
            }
        }
        // Wie auf dem Heldenbogen: Körper, Gesellschaft, Natur links; Wissen, Handwerk rechts
        pdf.Columns([1, 1], 14, () => Groups(sheet.Skills.Take(3)), () => Groups(sheet.Skills.Skip(3)));

        pdf.Columns([1, 1], 14,
            () =>
            {
                pdf.Heading("Sprachen");
                pdf.Lines(sheet.Languages, 4);
                pdf.Heading("Schriften");
                pdf.Lines(sheet.Scripts, 3, fill: true);
            },
            () =>
            {
                QualityLevels(pdf);
            });
    }

    // ---- Seite 3: Kampf ---------------------------------------------------------------------------------------

    private static void CombatPage(PdfLayout pdf, CharacterSheet sheet)
    {
        var energy = sheet.Energies.ToDictionary(e => e.Short);
        var top = new List<(string, string)>();
        foreach (var key in new[] { "LeP", "AW", "INI", "SK", "ZK", "GS" })
            if (energy.TryGetValue(key, out var e)) top.Add((key, e.Value.ToString()));
        foreach (var key in new[] { "RS", "BE" })
            if (sheet.Derived.FirstOrDefault(v => v.Short == key) is { } v) top.Add((key, v.Value));
        pdf.StartPage(Header(sheet, "Kampf", top));

        pdf.Columns([60, 40], 16,
            () =>
            {
                pdf.Heading("Kampftechniken");
                pdf.Table([new("Kampftechnik", 34), new("Leiteig.", 13), new("Sf.", 6), new("KtW", 8, true), new("AT/FK", 10, true), new("PA", 8, true)],
                    sheet.CombatTechniques.Select(c => (IReadOnlyList<string>)
                    [
                        c.Name, c.PrimaryAttributes, c.Factor, c.Value.ToString(),
                        c.Parry.StartsWith("FK", StringComparison.Ordinal) ? c.Parry[3..] : c.Attack,
                        c.Parry.StartsWith("FK", StringComparison.Ordinal) ? "–" : c.Parry,
                    ]), size: 7.4f, stripes: true);
            },
            () =>
            {
                pdf.Heading("Lebensenergie", keepWith: 40);
                var lep = energy.TryGetValue("LeP", out var l) ? l.Value.ToString() : "";
                pdf.ValueBox(pdf.X, pdf.Y, 44, 34, "Max", lep);
                pdf.ValueBox(pdf.X + 50, pdf.Y, pdf.W - 50, 34, "Aktuell", "");
                pdf.Y += 40;
                pdf.Table([new("LeP", 12, true), new("Auswirkung", 50)], sheet.PainLevels.Select(p => (IReadOnlyList<string>)[p.Label, p.Value]),
                    size: 7.4f);
                if (sheet.EncumbranceText is { } encumbrance) pdf.Paragraph(encumbrance, PdfLayout.SmallSize, gray: true);
            });

        pdf.Heading("Nahkampfwaffen");
        pdf.Table(
            [new("Waffe", 20), new("Kampftechnik", 15), new("Schadensb.", 10), new("TP", 9), new("Mod. AT/PA", 9), new("RW", 8),
             new("AT", 5, true), new("PA", 5, true), new("Gewicht", 9), new("Anmerkung", 18)],
            sheet.MeleeWeapons.Select(w => (IReadOnlyList<string>)
                [w.Name, w.CombatTechnique, w.DamageThreshold ?? "", w.Damage, w.Modifiers, w.Reach ?? "", w.Attack, w.Parry, w.Weight ?? "", w.Note ?? ""]),
            minRows: 3, size: 7.4f);
        pdf.Heading("Fernkampfwaffen");
        pdf.Table(
            [new("Waffe", 20), new("Kampftechnik", 15), new("Ladezeit", 11), new("TP", 9), new("Munition", 11), new("Reichweite", 11),
             new("FK", 5, true), new("Gewicht", 9), new("Anmerkung", 17)],
            sheet.RangedWeapons.Select(w => (IReadOnlyList<string>)
                [w.Name, w.CombatTechnique, w.LoadTime ?? "", w.Damage, w.Ammunition ?? "", w.Ranges ?? "", w.RangedAttack, w.Weight ?? "", w.Note ?? ""]),
            minRows: 2, size: 7.4f);

        pdf.Columns([55, 45], 16,
            () =>
            {
                pdf.Heading("Rüstungen");
                pdf.Table([new("Rüstung", 26), new("RS", 5, true), new("BE", 5, true), new("Zus. Abzüge", 22), new("Gewicht", 12), new("angelegt", 11)],
                    sheet.Armors.Select(a => (IReadOnlyList<string>)
                        [a.Name, a.Protection.ToString(), a.Encumbrance.ToString(), a.Extra ?? "", a.Weight ?? "", a.Worn ? "ja" : ""]),
                    minRows: 2, size: 7.4f);
            },
            () =>
            {
                pdf.Heading("Schild/Parierwaffe");
                pdf.Table([new("Name", 24), new("Mod. AT/PA", 13), new("AT", 6, true), new("PA", 6, true), new("Bonus", 9, true), new("Gewicht", 12)],
                    sheet.Shields.Select(s => (IReadOnlyList<string>)
                        [s.Name, s.Modifiers, s.Attack, s.Parry, s.ParryBonus ?? "", s.Weight ?? ""]),
                    minRows: 2, size: 7.4f);
            });

        pdf.Columns([42, 58], 16,
            () =>
            {
                pdf.Heading("Kampfsonderfertigkeiten");
                pdf.Lines(Group(sheet, "Kampfsonderfertigkeiten"), 8, fill: true);
            },
            () =>
            {
                pdf.Heading("Zustände und Status", keepWith: 110);
                var half = (States.Length + 1) / 2;
                pdf.Columns([1.15f, 1, 1], 10, () => pdf.CheckRows(Conditions, 4), () => pdf.CheckRows(States[..half], 1),
                    () => pdf.CheckRows(States[half..], 1));
            });
    }

    // ---- Seite 4: Besitz --------------------------------------------------------------------------------------

    private static void PossessionsPage(PdfLayout pdf, CharacterSheet sheet)
    {
        pdf.StartPage(Header(sheet, "Besitz"));
        pdf.Columns([68, 32], 16,
            () =>
            {
                pdf.Heading("Ausrüstung");
                pdf.Table([new("Gegenstand", 40), new("#", 5, true), new("Wert", 10, true), new("Gewicht", 12, true), new("getragen", 10)],
                    sheet.Items.Select(i => (IReadOnlyList<string>)
                        [i.Name, i.Quantity.ToString(), i.Price ?? "", i.Weight ?? "", i.Carried ? "ja" : "nein"]),
                    minRows: 28, size: 7.4f, stripes: true);
            },
            () =>
            {
                pdf.Heading("Geldbeutel", keepWith: 80);
                const float gap = 6f;
                var w = (pdf.W - gap) / 2;
                for (var i = 0; i < 4; i++)
                {
                    var coin = i < sheet.Coins.Count ? sheet.Coins[i] : null;
                    var label = coin?.Name ?? new[] { "Dukaten", "Silbertaler", "Heller", "Kreuzer" }[i];
                    pdf.ValueBox(pdf.X + i % 2 * (w + gap), pdf.Y + i / 2 * 36, w, 30, label, coin?.Value ?? "");
                }
                pdf.Y += 72;
                if (sheet.Possessions.FirstOrDefault(p => p.Label == "Geld") is { } money)
                    pdf.Paragraph("Zusammen " + money.Value, PdfLayout.SmallSize, gray: true);
                pdf.Box("Edelsteine", 46);
                pdf.Box("Schmuck", 46);
                pdf.Box("Sonstiges", 46);
                pdf.Heading("Traglast", keepWith: 40);
                pdf.ValueBox(pdf.X, pdf.Y, 60, 32, "Stein (KK × 2)", sheet.CarryCapacity.ToString());
                pdf.Y += 36;
                if (sheet.Possessions.FirstOrDefault(p => p.Label == "Traglast") is { } load)
                    pdf.Paragraph("Getragen: " + load.Value, PdfLayout.SmallSize, gray: true);
            });

        pdf.Heading("Notizen");
        pdf.Lines(sheet.Notes?.Split('\n') ?? [], 8, fill: true);
    }

    // ---- Tier/Vertrauter (je Tier eine Seite) ------------------------------------------------------------------

    private static void CompanionPage(PdfLayout pdf, CharacterSheet sheet, SheetCompanion c)
    {
        var lep = c.Values.FirstOrDefault(v => v.Label == "LeP")?.Value ?? "–";
        var top = new List<(string, string)> { ("LeP Max", lep), ("Aktuell", "") };
        if (c.Values.FirstOrDefault(v => v.Label == "AsP") is { } asp) top.AddRange([("AsP Max", asp.Value), ("Aktuell", "")]);
        pdf.StartPage(Header("Tier/Vertrauter", c.Name, c.Attributes, $"{c.Kind} von {sheet.Name} · {c.Species}", top));

        pdf.Columns([50, 50], 16,
            () =>
            {
                foreach (var f in c.Personal) pdf.FieldRow((f.Label, f.Value, 1));
                pdf.Heading("Werte");
                pdf.Table([new("Wert", 60), new("", 40, true)],
                    c.Values.Where(v => v.Label is not ("LeP" or "AsP")).Select(v => (IReadOnlyList<string>)[v.Label, v.Value]),
                    size: 7.6f, stripes: true);
                if (c.PainText is { } pain)
                {
                    pdf.Heading("Schmerz +1 bei", keepWith: 20);
                    pdf.Paragraph(pain);
                }
                pdf.Heading("Angriffe");
                pdf.Table([new("Angriff", 44), new("AT", 14, true), new("TP", 24, true), new("RW", 18, true)],
                    c.Attacks.Select(a => (IReadOnlyList<string>)[a.Name, a.Attack, a.Damage, a.Reach]), minRows: 3);
                pdf.Heading("Talente");
                pdf.Lines(c.Skills, 4);
            },
            () =>
            {
                pdf.Heading(c.IsFamiliar ? "Vertrautentricks und Tricks" : "Tricks");
                pdf.Lines(c.Tricks, 6);
                pdf.Heading("Ausbildungsaufsätze");
                pdf.Lines(c.Trainings, 2);
                pdf.Heading("Vorteile");
                pdf.Lines(c.Advantages, 2);
                pdf.Heading("Nachteile");
                pdf.Lines(c.Disadvantages, 2);
                pdf.Heading("Sonderfertigkeiten");
                pdf.Lines(c.SpecialAbilities, 2);
                pdf.Heading("Abenteuerpunkte des Tiers", keepWith: 36);
                const float gap = 6f;
                var w = (pdf.W - 2 * gap) / 3;
                pdf.ValueBox(pdf.X, pdf.Y, w, 30, "AP gesamt", c.Ap.Total.ToString());
                pdf.ValueBox(pdf.X + w + gap, pdf.Y, w, 30, "AP verfügbar", c.Ap.Remaining.ToString());
                pdf.ValueBox(pdf.X + 2 * (w + gap), pdf.Y, w, 30, "AP ausgegeben", c.Ap.Spent.ToString());
                pdf.Y += 36;
            });

        if (c.SpecialRules is { } rules)
        {
            pdf.Heading("Sonderregeln");
            pdf.Paragraph(rules, PdfLayout.SmallSize);
        }
        pdf.Heading("Notizen");
        pdf.Lines(c.Notes?.Split('\n') ?? [], 4, fill: true);
    }

    // ---- Seiten 5 und 6: Zauber, Liturgien --------------------------------------------------------------------

    private static void MagicPage(PdfLayout pdf, CharacterSheet sheet)
    {
        var asp = sheet.Energies.FirstOrDefault(e => e.Short == "AsP");
        pdf.StartPage(Header(sheet, "Zauber und Rituale", [("AsP Max", asp?.Value.ToString() ?? "–"), ("Aktuell", "")]));
        SpellPage(pdf, sheet, "Zauber/Ritual", "Merkmal", ["Zauber", "Rituale", "Magische Handlungen"], sheet.MagicTradition,
            "Merkmale", "Magische Sonderfertigkeiten", "Zaubertricks");
    }

    private static void KarmaPage(PdfLayout pdf, CharacterSheet sheet)
    {
        var kap = sheet.Energies.FirstOrDefault(e => e.Short == "KaP");
        pdf.StartPage(Header(sheet, "Liturgien und Zeremonien", [("KaP Max", kap?.Value.ToString() ?? "–"), ("Aktuell", "")]));
        SpellPage(pdf, sheet, "Liturgie/Zeremonie", "Aspekt", ["Liturgien", "Zeremonien"], sheet.KarmaTradition,
            "Aspekte", "Karmale Sonderfertigkeiten", "Segnungen");
    }

    private static void SpellPage(PdfLayout pdf, CharacterSheet sheet, string nameHeader, string propertyHeader, string[] groups,
        SheetTradition? tradition, string propertiesLabel, string abilitiesTitle, string tricksTitle)
    {
        var spells = sheet.Spells.Where(g => groups.Contains(g.Title))
            .SelectMany(g => g.Items.Select(s => (Group: g.Title, Spell: s))).ToList();
        pdf.Heading(string.Join(", ", groups.Where(g => spells.Any(s => s.Group == g)).DefaultIfEmpty(groups[0])));
        pdf.Table(
            [new(nameHeader, 22), new("Probe", 19), new("FW", 5, true), new("Kosten", 16), new("Dauer", 12), new("Reichweite", 11),
             new("Wirkungsdauer", 15), new(propertyHeader, 11), new("Sf.", 4)],
            spells.Select(s => (IReadOnlyList<string>)
            [
                s.Spell.Name + (groups.Length > 1 && s.Group != groups[0] ? $" ({Singular(s.Group)})" : ""), s.Spell.Check ?? "",
                s.Spell.Value, s.Spell.Cost ?? "", s.Spell.CastingTime ?? "", s.Spell.Range ?? "", s.Spell.Duration ?? "",
                s.Spell.Property ?? "", s.Spell.Factor ?? "",
            ]),
            minRows: 14, size: 7f, stripes: true);

        pdf.FieldRow(("Tradition", tradition?.Name, 2), ("Leiteigenschaft", tradition?.PrimaryAttribute, 1));
        pdf.FieldRow((propertiesLabel, tradition is { Properties.Count: > 0 } t ? string.Join(", ", t.Properties) : null, 1));
        pdf.Columns([1, 1], 16,
            () =>
            {
                pdf.Heading(abilitiesTitle);
                pdf.Lines(Group(sheet, abilitiesTitle), 8, fill: true);
            },
            () =>
            {
                pdf.Heading(tricksTitle);
                pdf.Lines(sheet.Spells.FirstOrDefault(g => g.Title == tricksTitle)?.Items.Select(s => s.Name) ?? [], 5);
                // QS gelten auch für Zauber und Liturgien
                QualityLevels(pdf);
            });
    }

    // ---- Hilfen -----------------------------------------------------------------------------------------------

    /// <summary>Qualitätsstufen nach übrigen Fertigkeitspunkten (Regel-Wiki, Fertigkeitsprobe).</summary>
    private static void QualityLevels(PdfLayout pdf)
    {
        pdf.Heading("Qualitätsstufen", keepWith: 90);
        pdf.Table([new("Fertigkeitspunkte", 1), new("Qualitätsstufe", 1, true)],
            [["0–3", "1"], ["4–6", "2"], ["7–9", "3"], ["10–12", "4"], ["13–15", "5"], ["16+", "6"]], size: 7.4f);
    }

    private static IReadOnlyList<string> Group(CharacterSheet sheet, string title) =>
        sheet.SpecialAbilities.FirstOrDefault(g => g.Title == title)?.Items ?? [];

    private static bool HasSpells(CharacterSheet sheet, params string[] groups) =>
        sheet.Spells.Any(g => groups.Contains(g.Title) && g.Items.Count > 0);

    private static string Singular(string group) => group switch
    {
        "Rituale" => "Ritual",
        "Magische Handlungen" => "mag. Handlung",
        "Zeremonien" => "Zeremonie",
        _ => group,
    };
}
