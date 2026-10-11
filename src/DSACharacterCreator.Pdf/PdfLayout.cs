using SkiaSharp;

namespace DSACharacterCreator.Pdf;

/// <summary>Schriften für das PDF; die App gibt Inter mit (wie in der Oberfläche), sonst die Systemschrift.</summary>
public sealed record PdfFonts(SKTypeface Regular, SKTypeface Bold)
{
    public static PdfFonts System() =>
        new(SKTypeface.Default, SKTypeface.FromFamilyName(null, SKFontStyle.Bold) ?? SKTypeface.Default);
}

/// <summary>Spalte einer Tabelle; die Breite ist ein Anteil an der Tabellenbreite.</summary>
internal sealed record PdfColumn(string Header, float Weight, bool AlignRight = false);

/// <summary>Textstück im Fließtext.</summary>
internal sealed record PdfRun(string Text, bool Bold = false, bool Gray = false);

/// <summary>
/// Einfaches Seitenlayout von oben nach unten auf A4: Text mit Umbruch, Kästchen, Tabellen mit Seitenumbruch und
/// wiederholtem Kopf, zweispaltige Zeilenlisten. <see cref="Y"/> ist die Oberkante des nächsten Elements in Punkt.
/// </summary>
internal sealed class PdfLayout : IDisposable
{
    public const float PageWidth = 595.28f;
    public const float PageHeight = 841.89f;
    private const float Margin = 36f;
    private const float FooterHeight = 14f;
    public const float BodySize = 8.5f;
    public const float SmallSize = 7.5f;
    private const float CellPadding = 3f;

    private static readonly SKColor Text = new(0x20, 0x20, 0x20);
    private static readonly SKColor Muted = new(0x6B, 0x6B, 0x6B);
    private static readonly SKColor Rule = new(0xD0, 0xD0, 0xD0);
    private static readonly SKColor HeaderFill = new(0xEE, 0xEE, 0xEE);

    private readonly SKDocument _document;
    private readonly PdfFonts _fonts;
    private readonly string _footer;
    private readonly Dictionary<(bool, float), SKFont> _fontCache = [];
    private readonly SKPaint _paint = new() { IsAntialias = true };
    private SKCanvas? _canvas;
    private int _page;

    public PdfLayout(Stream output, PdfFonts fonts, string footer, SKDocumentPdfMetadata metadata)
    {
        _document = SKDocument.CreatePdf(output, metadata);
        _fonts = fonts;
        _footer = footer;
        NewPage();
    }

    public float Y { get; set; }

    public static float Left => Margin;

    public static float Width => PageWidth - 2 * Margin;

    private static float Bottom => PageHeight - Margin - FooterHeight;

    private float Remaining => Bottom - Y;

    private SKCanvas Canvas => _canvas!;

    public void Dispose()
    {
        _document.EndPage();
        _document.Close();
        _document.Dispose();
        foreach (var font in _fontCache.Values) font.Dispose();
        _paint.Dispose();
    }

    private void NewPage()
    {
        if (_canvas is not null) _document.EndPage();
        _canvas = _document.BeginPage(PageWidth, PageHeight);
        _page++;
        Y = Margin;
        var baseline = PageHeight - Margin + 2;
        DrawText(_footer, Left, baseline, false, SmallSize, Muted);
        DrawText($"Seite {_page}", Left + Width, baseline, false, SmallSize, Muted, alignRight: true);
    }

    /// <summary>Neue Seite, falls weniger als <paramref name="height"/> Platz bleibt (und die Seite nicht leer ist).</summary>
    public void EnsureSpace(float height)
    {
        if (height > Remaining && Y > Margin) NewPage();
    }

    // ---- Text -------------------------------------------------------------------------------------------------

    private SKFont Font(bool bold, float size)
    {
        if (!_fontCache.TryGetValue((bold, size), out var font))
            _fontCache[(bold, size)] = font = new SKFont(bold ? _fonts.Bold : _fonts.Regular, size)
            {
                // Ohne Hinting: Laufweiten wie in der Schrift, nicht auf ganze Pixel gerundet (PDF ist skalierbar)
                Subpixel = true, LinearMetrics = true, Hinting = SKFontHinting.None,
            };
        return font;
    }

    private static float LineHeight(float size) => size * 1.3f;

    private float Ascent(bool bold, float size) => -Font(bold, size).Metrics.Ascent;

    private float Measure(string text, bool bold, float size) => Font(bold, size).MeasureText(text);

    private void DrawText(string text, float x, float baseline, bool bold, float size, SKColor color, bool alignRight = false)
    {
        _paint.Color = color;
        _paint.Style = SKPaintStyle.Fill;
        Canvas.DrawText(text, x, baseline, alignRight ? SKTextAlign.Right : SKTextAlign.Left, Font(bold, size), _paint);
    }

    /// <summary>Bricht an Leerzeichen und Zeilenwechseln um; zu lange Wörter werden zeichenweise geteilt.</summary>
    private List<string> Wrap(string text, float width, bool bold, float size)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Split('\n'))
        {
            var line = "";
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = line.Length == 0 ? word : line + " " + word;
                if (Measure(candidate, bold, size) <= width)
                {
                    line = candidate;
                    continue;
                }
                if (line.Length > 0) lines.Add(line);
                line = word;
                while (line.Length > 1 && Measure(line, bold, size) > width)
                {
                    var cut = line.Length - 1;
                    while (cut > 1 && Measure(line[..cut], bold, size) > width) cut--;
                    lines.Add(line[..cut]);
                    line = line[cut..];
                }
            }
            lines.Add(line);
        }
        return lines;
    }

    /// <summary>Kürzt einen Text mit „…“ auf die Breite.</summary>
    private string Fit(string text, float width, bool bold, float size)
    {
        if (Measure(text, bold, size) <= width) return text;
        var cut = text.Length;
        while (cut > 0 && Measure(text[..cut] + "…", bold, size) > width) cut--;
        return text[..cut].TrimEnd() + "…";
    }

    public void Title(string text)
    {
        const float size = 18f;
        DrawText(text, Left, Y + Ascent(true, size), true, size, Text);
        Y += LineHeight(size) + 2;
    }

    /// <summary>Abschnittsüberschrift mit Linie; bleibt mit <paramref name="keepWith"/> Punkt Folgeinhalt zusammen.</summary>
    public void Section(string text, float keepWith = 40)
    {
        const float size = 11f;
        Y += 8;
        EnsureSpace(LineHeight(size) + 4 + keepWith);
        DrawText(text, Left, Y + Ascent(true, size), true, size, Text);
        Y += LineHeight(size);
        Line(Left, Left + Width, Y, Muted, 0.75f);
        Y += 4;
    }

    private void Line(float x1, float x2, float y, SKColor color, float width = 0.5f)
    {
        _paint.Color = color;
        _paint.Style = SKPaintStyle.Stroke;
        _paint.StrokeWidth = width;
        Canvas.DrawLine(x1, y, x2, y, _paint);
    }

    private void Fill(SKRect rect, SKColor color)
    {
        _paint.Color = color;
        _paint.Style = SKPaintStyle.Fill;
        Canvas.DrawRect(rect, _paint);
    }

    /// <summary>
    /// Fließtext aus Bausteinen, die möglichst nicht getrennt werden (z. B. „Kultur Mittelreich“), mit
    /// <paramref name="gap"/> Abstand dazwischen. Passt ein Baustein nicht in eine ganze Zeile, wird er an Wörtern
    /// umbrochen.
    /// </summary>
    public void Flow(IEnumerable<IReadOnlyList<PdfRun>> chunks, float size = BodySize, float gap = 14f)
    {
        var lineHeight = LineHeight(size);
        var space = Measure(" ", false, size);
        var x = Left;
        var first = true;
        EnsureSpace(lineHeight);

        void NewLine()
        {
            Y += lineHeight;
            EnsureSpace(lineHeight);
            x = Left;
            first = true;
        }

        void Place(IReadOnlyList<PdfRun> runs, float before)
        {
            var baseline = Y + Ascent(false, size);
            var at = x + before;
            foreach (var run in runs)
            {
                DrawText(run.Text, at, baseline, run.Bold, size, run.Gray ? Muted : Text);
                at += Measure(run.Text, run.Bold, size);
            }
            x = at;
            first = false;
        }

        foreach (var chunk in chunks)
        {
            var width = chunk.Sum(r => Measure(r.Text, r.Bold, size));
            var before = first ? 0 : gap;
            if (x + before + width <= Left + Width)
            {
                Place(chunk, before);
                continue;
            }
            if (width <= Width)
            {
                NewLine();
                Place(chunk, 0);
                continue;
            }
            // Zu lang für eine Zeile: wortweise, Wörter durch Leerzeichen getrennt
            var words = chunk.SelectMany(r => r.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => new PdfRun(w, r.Bold, r.Gray))).ToList();
            foreach (var word in words)
            {
                var w = Measure(word.Text, word.Bold, size);
                var b = first ? 0 : (word == words[0] ? gap : space);
                if (x + b + w > Left + Width && !first)
                {
                    NewLine();
                    b = 0;
                }
                Place([word], b);
            }
        }
        Y += lineHeight;
    }

    /// <summary>Absatz mit fett vorangestelltem Titel: „Vorteile: Begabung (Klettern), …“.</summary>
    public void Labeled(string label, string text, float size = BodySize)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<IReadOnlyList<PdfRun>> { new[] { new PdfRun(label + ":", Bold: true) } };
        chunks.AddRange(words.Select(w => (IReadOnlyList<PdfRun>)[new PdfRun(w)]));
        Flow(chunks, size, Measure(" ", false, size));
        Y += 2;
    }

    public void Paragraph(string text, float size = BodySize, bool gray = false)
    {
        var lineHeight = LineHeight(size);
        foreach (var line in Wrap(text, Width, false, size))
        {
            EnsureSpace(lineHeight);
            DrawText(line, Left, Y + Ascent(false, size), false, size, gray ? Muted : Text);
            Y += lineHeight;
        }
        Y += 2;
    }

    /// <summary>Werte in Kästchen nebeneinander: Kurzname oben, Wert groß darunter.</summary>
    public void Boxes(IReadOnlyList<(string Label, string Value)> values, float boxWidth = 44f, float spacing = 4f)
    {
        const float height = 30f;
        var x = Left;
        EnsureSpace(height);
        foreach (var (label, value) in values)
        {
            if (x + boxWidth > Left + Width + 0.1f)
            {
                x = Left;
                Y += height + spacing;
                EnsureSpace(height);
            }
            var rect = new SKRect(x, Y, x + boxWidth, Y + height);
            _paint.Color = Muted;
            _paint.Style = SKPaintStyle.Stroke;
            _paint.StrokeWidth = 0.6f;
            Canvas.DrawRoundRect(rect, 3, 3, _paint);
            var center = x + boxWidth / 2;
            _paint.Color = Muted;
            _paint.Style = SKPaintStyle.Fill;
            Canvas.DrawText(label, center, Y + 9.5f, SKTextAlign.Center, Font(false, SmallSize), _paint);
            _paint.Color = Text;
            Canvas.DrawText(value, center, Y + 24.5f, SKTextAlign.Center, Font(true, 13f), _paint);
            x += boxWidth + spacing;
        }
        Y += height + 6;
    }

    // ---- Tabellen ---------------------------------------------------------------------------------------------

    private static float[] ColumnWidths(IReadOnlyList<PdfColumn> columns, float width)
    {
        var total = columns.Sum(c => c.Weight);
        return columns.Select(c => c.Weight / total * width).ToArray();
    }

    /// <summary>Zeilenhöhe; mit <paramref name="wrap"/> wachsen Zellen mit ihrem Inhalt, sonst wird gekürzt.</summary>
    private float RowHeight(IReadOnlyList<string> cells, float[] widths, bool bold, float size, bool wrap)
    {
        var lines = !wrap ? 1 : cells.Select((c, i) => Wrap(c, widths[i] - 2 * CellPadding, bold, size).Count).DefaultIfEmpty(1).Max();
        return lines * LineHeight(size) + 2;
    }

    private void DrawRow(IReadOnlyList<PdfColumn> columns, IReadOnlyList<string> cells, float x, float[] widths,
        bool header, float size, bool wrap, float height)
    {
        if (header) Fill(new SKRect(x, Y, x + widths.Sum(), Y + height), HeaderFill);
        var at = x;
        for (var i = 0; i < columns.Count; i++)
        {
            var text = i < cells.Count ? cells[i] : "";
            var inner = widths[i] - 2 * CellPadding;
            var lines = wrap ? Wrap(text, inner, header, size) : [Fit(text, inner, header, size)];
            var baseline = Y + 1 + Ascent(header, size);
            foreach (var line in lines)
            {
                if (columns[i].AlignRight) DrawText(line, at + widths[i] - CellPadding, baseline, header, size, Text, alignRight: true);
                else DrawText(line, at + CellPadding, baseline, header, size, Text);
                baseline += LineHeight(size);
            }
            at += widths[i];
        }
        if (!header) Line(x, x + widths.Sum(), Y + height, Rule);
    }

    /// <summary>Tabelle über die volle Breite; bei einem Seitenumbruch wird der Kopf wiederholt.</summary>
    public void Table(IReadOnlyList<PdfColumn> columns, IEnumerable<IReadOnlyList<string>> rows, float size = BodySize)
    {
        var widths = ColumnWidths(columns, Width);
        var headers = columns.Select(c => c.Header).ToList();
        var headerHeight = RowHeight(headers, widths, true, size, true);
        var needHeader = true;
        foreach (var row in rows)
        {
            var height = RowHeight(row, widths, false, size, true);
            if (height + (needHeader ? headerHeight : 0) > Remaining)
            {
                NewPage();
                needHeader = true;
            }
            if (needHeader)
            {
                DrawRow(columns, headers, Left, widths, true, size, true, headerHeight);
                Y += headerHeight;
                needHeader = false;
            }
            DrawRow(columns, row, Left, widths, false, size, true, height);
            Y += height;
        }
        Y += 6;
    }

    /// <summary>Zeile einer zweispaltigen Liste: Gruppenkopf (fett, hinterlegt) oder Datenzeile.</summary>
    public sealed record ListRow(IReadOnlyList<string> Cells, bool Header);

    /// <summary>
    /// Zeilen auf zwei Spalten, möglichst gleich lang; was nicht auf die Seite passt, geht auf der nächsten weiter.
    /// Ein Gruppenkopf bleibt mit seiner ersten Zeile zusammen. Zellen werden gekürzt, nicht umbrochen.
    /// </summary>
    public void TwoColumns(IReadOnlyList<PdfColumn> columns, IReadOnlyList<ListRow> rows, float size = SmallSize)
    {
        const float gutter = 14f;
        var columnWidth = (Width - gutter) / 2;
        var widths = ColumnWidths(columns, columnWidth);
        var rowHeight = LineHeight(size) + 2;
        float Height(ListRow r) => r.Header ? rowHeight + 3 : rowHeight;

        var next = 0;
        while (next < rows.Count)
        {
            EnsureSpace(3 * rowHeight);
            var rest = rows.Skip(next).Sum(Height);
            // Linke Spalte bis etwa zur Hälfte des Rests, höchstens bis zum Seitenende
            var limit = Math.Min(Remaining, (float)Math.Ceiling(rest / 2 / rowHeight) * rowHeight + rowHeight);
            var top = Y;
            var bottom = top;
            for (var column = 0; column < 2 && next < rows.Count; column++)
            {
                var x = Left + column * (columnWidth + gutter);
                Y = top;
                var max = column == 0 ? limit : Bottom - top;
                var used = 0f;
                while (next < rows.Count)
                {
                    var row = rows[next];
                    var needed = Height(row) + (row.Header && next + 1 < rows.Count ? Height(rows[next + 1]) : 0);
                    if (used + needed > max && used > 0) break;
                    if (row.Header) Y += 3;
                    DrawRow(columns, row.Cells, x, widths, row.Header, size, false, rowHeight);
                    Y += rowHeight;
                    used += Height(row);
                    next++;
                }
                bottom = Math.Max(bottom, Y);
            }
            Y = bottom;
            if (next < rows.Count) NewPage();
        }
        Y += 6;
    }
}
