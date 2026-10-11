using DSACharacterCreator.Core.Sheet;
using SkiaSharp;

namespace DSACharacterCreator.Pdf;

/// <summary>Regeltexte der gewählten Zauber, Liturgien usw. als eigenes PDF (A4), getrennt vom Charakterbogen.</summary>
public static class SpellTextPdf
{
    public static void Write(SpellTextSheet sheet, Stream output, PdfFonts fonts, DateTime? created = null)
    {
        var metadata = new SKDocumentPdfMetadata
        {
            Title = $"Zauber und Liturgien {sheet.Name}",
            Creator = "DSA5 Character Creator",
            Producer = "DSA5 Character Creator",
            Creation = created ?? DateTime.Now,
            Modified = created ?? DateTime.Now,
        };
        using var pdf = new PdfLayout(output, fonts, $"{sheet.Name} · Regeltexte aus dem DSA Regel-Wiki", metadata);
        foreach (var group in sheet.Groups.DefaultIfEmpty(new SheetGroup<SpellText>("Zauber und Liturgien", [])))
        {
            pdf.StartPage(SheetPdf.Header(group.Title, sheet.Name, sheet.Attributes, "Regeltexte"));
            if (group.Items.Count == 0) pdf.Paragraph("Der Held kennt keine Zauber, Liturgien oder ähnliches.", gray: true);
            foreach (var spell in group.Items)
            {
                pdf.EntryTitle(spell.Name, spell.Summary, keepWith: 4 * 10.4f);
                foreach (var field in spell.Fields) pdf.Definition(field.Label, field.Value);
                pdf.Y += 3;
                pdf.Paragraph(spell.RuleText);
                if (spell.Enhancements.Count > 0)
                {
                    pdf.Y += 2;
                    pdf.Paragraph("Erlernte Erweiterungen", PdfLayout.SmallSize);
                    foreach (var e in spell.Enhancements) pdf.Definition(e.Label, e.Value);
                }
                if (spell.Note is { } note) pdf.Paragraph("Anmerkung: " + note, PdfLayout.SmallSize, gray: true);
                if (spell.Publications is { } publications) pdf.Paragraph("Publikation: " + publications, PdfLayout.SmallSize, gray: true);
                pdf.Y += 8;
            }
        }
    }
}
