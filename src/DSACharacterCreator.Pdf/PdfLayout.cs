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

/// <summary>
/// Seitenlayout für den Heldenbogen (A4). Gezeichnet wird in einen Rahmen (<see cref="X"/>, <see cref="W"/>) ab
/// <see cref="Y"/>; <see cref="Columns"/> teilt den Rahmen in Spalten, die jeweils auf derselben Höhe beginnen. Seiten
/// werden erst am Ende ins PDF geschrieben, deshalb kann eine Spalte auf einer Folgeseite weiterlaufen, während die
/// nächste Spalte wieder oben auf der Ausgangsseite beginnt.
/// </summary>
internal sealed class PdfLayout : IDisposable
{
    public const float PageWidth = 595.28f;
    public const float PageHeight = 841.89f;
    public const float Margin = 28f;
    private const float FooterHeight = 12f;
    public const float BodySize = 8f;
    public const float SmallSize = 6.8f;
    public const float RowHeight = 12.5f;
    private const float CellPadding = 2.5f;

    public static readonly SKColor Ink = new(0x1E, 0x1E, 0x1E);
    public static readonly SKColor Muted = new(0x6E, 0x6E, 0x6E);
    private static readonly SKColor RuleColor = new(0xC4, 0xC4, 0xC4);
    private static readonly SKColor HeaderFill = new(0xE9, 0xE9, 0xE9);
    private static readonly SKColor Stripe = new(0xF5, 0xF5, 0xF5);

    private readonly Stream _output;
    private readonly SKDocumentPdfMetadata _metadata;
    private readonly PdfFonts _fonts;
    private readonly string _footer;
    private readonly Dictionary<(bool, float), SKFont> _fontCache = [];
    private readonly SKPaint _paint = new() { IsAntialias = true };
    private readonly List<SKPictureRecorder> _pages = [];
    private readonly List<float> _contentTop = [];
    private Action<PdfLayout, bool>? _header;

    public PdfLayout(Stream output, PdfFonts fonts, string footer, SKDocumentPdfMetadata metadata)
    {
        _output = output;
        _fonts = fonts;
        _footer = footer;
        _metadata = metadata;
    }

    /// <summary>Aktuelle Seite (Index).</summary>
    public int Page { get; private set; } = -1;

    /// <summary>Oberkante des nächsten Elements.</summary>
    public float Y { get; set; }

    /// <summary>Linker Rand des aktuellen Rahmens.</summary>
    public float X { get; private set; } = Margin;

    /// <summary>Breite des aktuellen Rahmens.</summary>
    public float W { get; private set; } = PageWidth - 2 * Margin;

    public static float FullWidth => PageWidth - 2 * Margin;

    public static float Bottom => PageHeight - Margin - FooterHeight;

    private float Remaining => Bottom - Y;

    public SKCanvas Canvas => _pages[Page].RecordingCanvas;

    public void Dispose()
    {
        using (var document = SKDocument.CreatePdf(_output, _metadata))
        {
            foreach (var recorder in _pages)
            {
                using var picture = recorder.EndRecording();
                var canvas = document.BeginPage(PageWidth, PageHeight);
                canvas.DrawPicture(picture);
                document.EndPage();
                recorder.Dispose();
            }
            document.Close();
        }
        foreach (var font in _fontCache.Values) font.Dispose();
        _paint.Dispose();
    }

    // ---- Seiten -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Neue Seite mit Kopf. Der Kopf zeichnet sich ab <see cref="Y"/> und setzt <see cref="Y"/> darunter; das
    /// zweite Argument ist <c>true</c> auf Fortsetzungsseiten.
    /// </summary>
    public void StartPage(Action<PdfLayout, bool> header)
    {
        _header = header;
        AddPage(continued: false);
    }

    private void AddPage(bool continued)
    {
        var recorder = new SKPictureRecorder();
        recorder.BeginRecording(new SKRect(0, 0, PageWidth, PageHeight));
        _pages.Add(recorder);
        Page = _pages.Count - 1;
        var (x, w) = (X, W);
        (X, W) = (Margin, FullWidth);
        var baseline = PageHeight - Margin + 2;
        Text(_footer, Margin, baseline, false, SmallSize, Muted);
        Text($"Seite {_pages.Count}", Margin + FullWidth, baseline, false, SmallSize, Muted, alignRight: true);
        Y = Margin;
        _header?.Invoke(this, continued);
        _contentTop.Add(Y);
        (X, W) = (x, w);
    }

    /// <summary>Weiter auf der nächsten Seite (vorhandene Folgeseite oder neue Fortsetzungsseite), im selben Rahmen.</summary>
    private void NextPage()
    {
        if (Page + 1 < _pages.Count)
        {
            Page++;
            Y = _contentTop[Page];
        }
        else AddPage(continued: true);
    }

    /// <summary>Nächste Seite, falls weniger als <paramref name="height"/> Platz bleibt und hier schon etwas steht.</summary>
    public void EnsureSpace(float height)
    {
        if (height > Remaining && Y > _contentTop[Page] + 0.1f) NextPage();
    }

    /// <summary>
    /// Spalten mit den Breitenanteilen <paramref name="weights"/> und <paramref name="gap"/> Abstand; jede beginnt an
    /// der aktuellen Position. Danach steht die Position unter der längsten Spalte.
    /// </summary>
    public void Columns(float[] weights, float gap, params Action[] columns)
    {
        var (x0, w0, page0, y0) = (X, W, Page, Y);
        var widths = weights.Select(f => f / weights.Sum() * (w0 - gap * (weights.Length - 1))).ToArray();
        var (endPage, endY) = (page0, y0);
        var x = x0;
        for (var i = 0; i < columns.Length; i++)
        {
            (X, W, Page, Y) = (x, widths[i], page0, y0);
            columns[i]();
            if (Page > endPage || (Page == endPage && Y > endY)) (endPage, endY) = (Page, Y);
            x += widths[i] + gap;
        }
        (X, W, Page, Y) = (x0, w0, endPage, endY);
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

    public float Measure(string text, bool bold, float size) => Font(bold, size).MeasureText(text);

    public void Text(string text, float x, float baseline, bool bold, float size, SKColor color, bool alignRight = false,
        bool center = false)
    {
        _paint.Color = color;
        _paint.Style = SKPaintStyle.Fill;
        var align = center ? SKTextAlign.Center : alignRight ? SKTextAlign.Right : SKTextAlign.Left;
        Canvas.DrawText(text, x, baseline, align, Font(bold, size), _paint);
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
    public string Fit(string text, float width, bool bold, float size)
    {
        if (Measure(text, bold, size) <= width) return text;
        var cut = text.Length;
        while (cut > 0 && Measure(text[..cut] + "…", bold, size) > width) cut--;
        return text[..cut].TrimEnd() + "…";
    }

    // ---- Grundformen ------------------------------------------------------------------------------------------

    public void Line(float x1, float y1, float x2, float y2, SKColor color, float width = 0.5f)
    {
        _paint.Color = color;
        _paint.Style = SKPaintStyle.Stroke;
        _paint.StrokeWidth = width;
        Canvas.DrawLine(x1, y1, x2, y2, _paint);
    }

    public void Fill(SKRect rect, SKColor color)
    {
        _paint.Color = color;
        _paint.Style = SKPaintStyle.Fill;
        Canvas.DrawRect(rect, _paint);
    }

    public void Frame(SKRect rect, SKColor color, float width = 0.7f, float radius = 2f)
    {
        _paint.Color = color;
        _paint.Style = SKPaintStyle.Stroke;
        _paint.StrokeWidth = width;
        Canvas.DrawRoundRect(rect, radius, radius, _paint);
    }

    /// <summary>Wertkästchen: Beschriftung klein oben, Wert groß darunter (leer = zum Ausfüllen).</summary>
    public void ValueBox(float x, float y, float w, float h, string label, string value, SKColor? border = null, float valueSize = 13f)
    {
        var rect = new SKRect(x, y, x + w, y + h);
        Frame(rect, border ?? Muted, border is null ? 0.7f : 1.6f);
        Text(label, x + 3, y + 8, false, SmallSize - 0.6f, Muted);
        if (value.Length > 0) Text(value, x + w / 2, y + h - (h - valueSize) / 2 + 1, true, valueSize, Ink, center: true);
    }

    // ---- Bausteine im Rahmen ----------------------------------------------------------------------------------

    /// <summary>Abschnittsüberschrift mit Linie; rechts optional ein kleiner Zusatz.</summary>
    public void Heading(string text, string? right = null, float keepWith = 3 * RowHeight)
    {
        const float size = 10.5f;
        Y += 6;
        EnsureSpace(LineHeight(size) + 3 + keepWith);
        Text(text, X, Y + Ascent(true, size), true, size, Ink);
        if (right is not null) Text(right, X + W, Y + Ascent(true, size), false, SmallSize, Muted, alignRight: true);
        Y += LineHeight(size);
        Line(X, Y, X + W, Y, Ink, 0.8f);
        Y += 2;
    }

    /// <summary>
    /// Einträge je auf einer Linie (lange umbrochen), dann leere Linien bis <paramref name="minLines"/> bzw. mit
    /// <paramref name="fill"/> bis zum Seitenende.
    /// </summary>
    public void Lines(IEnumerable<string> entries, int minLines, bool fill = false)
    {
        var count = 0;
        foreach (var entry in entries)
        {
            foreach (var line in Wrap(entry, W - 2 * CellPadding, false, BodySize))
            {
                EnsureSpace(RowHeight);
                Text(line, X + CellPadding, Y + RowHeight - 3.5f, false, BodySize, Ink);
                Y += RowHeight;
                Line(X, Y, X + W, Y, RuleColor);
                count++;
            }
        }
        for (; count < minLines || fill; count++)
        {
            if (RowHeight > Remaining) break;
            Y += RowHeight;
            Line(X, Y, X + W, Y, RuleColor);
        }
        Y += 2;
    }

    /// <summary>Beschriftetes Feld auf einer Linie: „Familie ________“ (Wert fett, leer = zum Ausfüllen).</summary>
    public void Field(float x, float w, string label, string? value)
    {
        var baseline = Y + RowHeight - 3.5f;
        Text(label, x, baseline, false, SmallSize, Muted);
        var start = x + Measure(label, false, SmallSize) + 4;
        if (!string.IsNullOrEmpty(value)) Text(Fit(value, x + w - start - 1, true, BodySize), start, baseline, true, BodySize, Ink);
        Line(start, Y + RowHeight - 1, x + w, Y + RowHeight - 1, RuleColor);
    }

    /// <summary>Eine Zeile mit mehreren Feldern nebeneinander (Breitenanteile).</summary>
    public void FieldRow(params (string Label, string? Value, float Weight)[] fields)
    {
        EnsureSpace(RowHeight);
        const float gap = 8f;
        var total = fields.Sum(f => f.Weight);
        var x = X;
        foreach (var (label, value, weight) in fields)
        {
            var w = weight / total * (W - gap * (fields.Length - 1));
            Field(x, w, label, value);
            x += w + gap;
        }
        Y += RowHeight + 1;
    }

    private static float[] ColumnWidths(IReadOnlyList<PdfColumn> columns, float width)
    {
        var total = columns.Sum(c => c.Weight);
        return columns.Select(c => c.Weight / total * width).ToArray();
    }

    private float MeasureRow(IReadOnlyList<string> cells, float[] widths, bool bold, float size) =>
        Math.Max(RowHeight, cells.Select((c, i) => Wrap(c, widths[i] - 2 * CellPadding, bold, size).Count).DefaultIfEmpty(1).Max()
            * LineHeight(size) + 3);

    private void DrawRow(IReadOnlyList<PdfColumn> columns, IReadOnlyList<string> cells, float[] widths, bool header,
        float size, float height, bool stripe)
    {
        var right = X + widths.Sum();
        if (header) Fill(new SKRect(X, Y, right, Y + height), HeaderFill);
        else if (stripe) Fill(new SKRect(X, Y, right, Y + height), Stripe);
        var at = X;
        var textSize = header ? SmallSize : size;
        for (var i = 0; i < columns.Count; i++)
        {
            var text = i < cells.Count ? cells[i] : "";
            var lines = Wrap(text, widths[i] - 2 * CellPadding, header, textSize);
            var baseline = Y + (lines.Count == 1 ? (height + textSize * 0.72f) / 2 : 1.5f + Ascent(header, textSize));
            foreach (var line in lines)
            {
                if (columns[i].AlignRight) Text(line, at + widths[i] - CellPadding, baseline, header, textSize, Ink, alignRight: true);
                else Text(line, at + CellPadding, baseline, header, textSize, header ? Muted : Ink);
                baseline += LineHeight(textSize);
            }
            at += widths[i];
        }
        Line(X, Y + height, right, Y + height, header ? Muted : RuleColor);
    }

    /// <summary>
    /// Tabelle im Rahmen; danach leere Zeilen bis <paramref name="minRows"/> (zum Ausfüllen). Bei einem
    /// Seitenumbruch wird der Kopf wiederholt.
    /// </summary>
    public void Table(IReadOnlyList<PdfColumn> columns, IEnumerable<IReadOnlyList<string>> rows, int minRows = 0,
        float size = BodySize, bool stripes = false)
    {
        var widths = ColumnWidths(columns, W);
        var headers = columns.Select(c => c.Header).ToList();
        var headerHeight = MeasureRow(headers, widths, true, SmallSize);
        var needHeader = true;
        var count = 0;

        void Row(IReadOnlyList<string> row)
        {
            var height = MeasureRow(row, widths, false, size);
            if (height + (needHeader ? headerHeight : 0) > Remaining)
            {
                NextPage();
                needHeader = true;
            }
            if (needHeader)
            {
                DrawRow(columns, headers, widths, true, size, headerHeight, false);
                Y += headerHeight;
                needHeader = false;
            }
            DrawRow(columns, row, widths, false, size, height, stripes && count % 2 == 1);
            Y += height;
            count++;
        }

        foreach (var row in rows) Row(row);
        while (count < minRows && (needHeader ? headerHeight : 0) + RowHeight <= Remaining) Row([]);
        if (count == 0) Row([]);
        Y += 3;
    }

    /// <summary>Kästchenreihe zum Ankreuzen: Name und <paramref name="boxes"/> Kästchen (Zustandsstufen I–IV).</summary>
    public void CheckRows(IReadOnlyList<string> names, int boxes)
    {
        const float box = 7f;
        foreach (var name in names)
        {
            const float height = 10.5f;
            EnsureSpace(height);
            Text(name, X, Y + 8, false, SmallSize, Ink);
            for (var i = 0; i < boxes; i++)
            {
                var x = X + W - (boxes - i) * (box + 2);
                Frame(new SKRect(x, Y + 1.5f, x + box, Y + 1.5f + box), Muted, 0.6f, 0.5f);
            }
            Y += height;
        }
    }

    /// <summary>Leerer Rahmen mit Beschriftung oben links (Porträt, Notizen …), optional mit Text.</summary>
    public void Box(string label, float height, string? text = null)
    {
        EnsureSpace(Math.Min(height, Bottom - _contentTop[Page]));
        height = Math.Min(height, Remaining);
        var rect = new SKRect(X, Y, X + W, Y + height);
        Frame(rect, Muted);
        Text(label, X + 3, Y + 8, false, SmallSize - 0.4f, Muted);
        if (text is not null)
        {
            var baseline = Y + 12 + Ascent(false, BodySize);
            foreach (var line in Wrap(text, W - 8, false, BodySize))
            {
                if (baseline > Y + height - 3) break;
                Text(line, X + 4, baseline, false, BodySize, Ink);
                baseline += LineHeight(BodySize);
            }
        }
        Y += height + 4;
    }

    /// <summary>Fließtext über die Rahmenbreite.</summary>
    public void Paragraph(string text, float size = BodySize, bool gray = false)
    {
        foreach (var line in Wrap(text, W, false, size))
        {
            EnsureSpace(LineHeight(size));
            Text(line, X, Y + Ascent(false, size), false, size, gray ? Muted : Ink);
            Y += LineHeight(size);
        }
        Y += 2;
    }
}
