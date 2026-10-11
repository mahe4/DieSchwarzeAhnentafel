using DSACharacterCreator.Core.Sheet;
using SkiaSharp;

namespace DSACharacterCreator.Pdf;

/// <summary>
/// Charakterbogen als PDF (A4). Alle Texte kommen aus <see cref="CharacterSheet"/>; hier wird nur angeordnet,
/// in derselben Reihenfolge wie die Ansicht in der App.
/// </summary>
public static class SheetPdf
{
    public static void Write(CharacterSheet sheet, Stream output, PdfFonts fonts, DateTime? created = null)
    {
        var metadata = new SKDocumentPdfMetadata
        {
            Title = $"Charakterbogen {sheet.Name}",
            Creator = "DSA5 Character Creator",
            Producer = "DSA5 Character Creator",
            Creation = created ?? DateTime.Now,
            Modified = created ?? DateTime.Now,
        };
        using var pdf = new PdfLayout(output, fonts, $"{sheet.Name} · DSA5-Charakterbogen", metadata);

        pdf.Title(sheet.Name);
        pdf.Flow(sheet.Header.Select(Field));
        if (sheet.Personal.Count > 0) pdf.Flow(sheet.Personal.Select(Field));

        pdf.Section("Eigenschaften");
        pdf.Boxes(sheet.Attributes.Select(a => (a.Short, a.Value)).ToList(), boxWidth: 58f);
        pdf.Section("Basiswerte");
        pdf.Boxes(sheet.Derived.Select(v => (v.Short, v.Value)).ToList());
        var notes = sheet.Derived.Where(v => v.Note is not null).Select(v => $"{v.Short} {v.Note}").ToList();
        if (notes.Count > 0) pdf.Paragraph(string.Join(" · ", notes), PdfLayout.SmallSize, gray: true);
        if (sheet.PainText is { } pain) pdf.Paragraph(pain, PdfLayout.SmallSize, gray: true);
        if (sheet.EncumbranceText is { } encumbrance) pdf.Paragraph(encumbrance, PdfLayout.SmallSize, gray: true);

        pdf.Section("Vorteile, Nachteile und Sonderfertigkeiten");
        pdf.Labeled("Vorteile", List(sheet.Advantages));
        pdf.Labeled("Nachteile", List(sheet.Disadvantages));
        foreach (var group in sheet.SpecialAbilities) pdf.Labeled(group.Title, List(group.Items));
        pdf.Labeled("Sprachen", List(sheet.Languages));
        pdf.Labeled("Schriften", List(sheet.Scripts));

        pdf.Section("Kampf");
        pdf.Table(
            [new("Kampftechnik", 30), new("Leiteigenschaft", 16), new("SF", 5), new("KtW", 6, true), new("AT", 6, true), new("PA / FK", 8, true)],
            sheet.CombatTechniques.Select(c => (IReadOnlyList<string>)[c.Name, c.PrimaryAttributes, c.Factor, c.Value.ToString(), c.Attack, c.Parry]));
        if (sheet.Weapons.Count > 0)
            pdf.Table(
                [new("Waffe", 22), new("Kampftechnik", 18), new("Werte", 16), new("TP", 10), new("RW", 14), new("Anmerkung", 26)],
                sheet.Weapons.Select(w => (IReadOnlyList<string>)[w.Name, w.CombatTechnique, w.Values, w.Damage, w.Reach ?? "", w.Note ?? ""]));
        if (sheet.Armors.Count > 0)
            pdf.Table(
                [new("Rüstung", 30), new("RS", 5, true), new("BE", 5, true), new("Zusätzliche Abzüge", 40), new("Angelegt", 10)],
                sheet.Armors.Select(a => (IReadOnlyList<string>)[a.Name, a.Protection.ToString(), a.Encumbrance.ToString(), a.Extra ?? "", a.Worn ? "ja" : ""]));

        pdf.Section("Talente", keepWith: 60);
        pdf.TwoColumns(
            [new("", 34), new("Probe", 38), new("BE", 10), new("SF", 7), new("FW", 8, true)],
            sheet.Skills.SelectMany(g => new[] { new PdfLayout.ListRow([g.Title, "Probe", "BE", "SF", "FW"], true) }
                    .Concat(g.Items.Select(s => new PdfLayout.ListRow([s.Name, s.Check, s.Encumbrance, s.Factor, s.Value.ToString()], false))))
                .ToList());

        foreach (var group in sheet.Spells)
        {
            pdf.Section(group.Title);
            pdf.Table(
                [new("Name", 24), new("Probe", 19), new("FW", 5, true), new("SF", 5), new("Merkmal", 13), new("Kosten · Dauer · RW · WD", 34)],
                group.Items.Select(s => (IReadOnlyList<string>)[s.Name, s.Check ?? "", s.Value, s.Factor ?? "", s.Property ?? "", s.Details]),
                PdfLayout.SmallSize);
        }

        pdf.Section("Ausrüstung");
        if (sheet.Items.Count > 0)
            pdf.Table(
                [new("Gegenstand", 60), new("Anzahl", 10, true), new("Gewicht", 15, true), new("Dabei", 10)],
                sheet.Items.Select(i => (IReadOnlyList<string>)[i.Name, i.Quantity.ToString(), i.Weight ?? "", i.Carried ? "ja" : "nein"]));
        pdf.Flow(sheet.Possessions.Select(Field));

        if (sheet.Notes is { } text)
        {
            pdf.Section("Notizen");
            pdf.Paragraph(text);
        }
    }

    private static string List(IReadOnlyList<string> items) => items.Count > 0 ? string.Join(", ", items) : "–";

    private static IReadOnlyList<PdfRun> Field(SheetField field) =>
        [new(field.Label, Gray: true), new(" "), new(field.Value, Bold: true)];
}
