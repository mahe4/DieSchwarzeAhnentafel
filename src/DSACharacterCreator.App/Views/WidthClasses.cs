using System.Globalization;
using Avalonia;
using Avalonia.Controls;

namespace DSACharacterCreator.Views;

/// <summary>
/// Breakpoints als Klassen: <c>views:WidthClasses.Rules="shellNarrow:..800 sheetWide:820.."</c> setzt die Klasse,
/// solange die eigene Breite im Bereich liegt (wie C#-Ranges: Anfang einschließlich, Ende ausschließlich).
/// Ersetzt Container Queries: Die kosteten am Handy beim Aufbau großer Seiten (Charakterbogen, Ausrüstung) mit allen
/// geöffneten Seiten Hunderte MB Allokationen und sekundenlange Hänger („App reagiert nicht“). Die Klassen ändern sich
/// nur beim Überschreiten einer Schwelle.
/// </summary>
public static class WidthClasses
{
    public static readonly AttachedProperty<string?> RulesProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("Rules", typeof(WidthClasses));

    public static string? GetRules(Control control) => control.GetValue(RulesProperty);

    public static void SetRules(Control control, string? value) => control.SetValue(RulesProperty, value);

    static WidthClasses()
    {
        RulesProperty.Changed.AddClassHandler<Control>((control, _) =>
        {
            control.SizeChanged -= OnSizeChanged;
            control.SizeChanged += OnSizeChanged;
            Apply(control, control.Bounds.Width);
        });
    }

    private static void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged) Apply((Control)sender!, e.NewSize.Width);
    }

    private static void Apply(Control control, double width)
    {
        foreach (var (name, from, to) in Parse(GetRules(control)))
            control.Classes.Set(name, width >= from && width < to);
    }

    /// <summary>„name:..800“ (schmaler als 800), „name:820..“ (ab 820), „name:800..1200“.</summary>
    public static IEnumerable<(string Name, double From, double To)> Parse(string? rules)
    {
        foreach (var rule in (rules ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = rule.Split(':');
            var range = parts.Length == 2 ? parts[1].Split("..") : [];
            if (parts[0].Length == 0 || range.Length != 2) throw new FormatException($"Ungültige Breitenregel „{rule}“.");
            yield return (parts[0], Bound(range[0], 0), Bound(range[1], double.PositiveInfinity));
        }
    }

    private static double Bound(string text, double open) =>
        text.Length == 0 ? open : double.Parse(text, CultureInfo.InvariantCulture);
}
