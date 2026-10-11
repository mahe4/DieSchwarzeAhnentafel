using System.Text.RegularExpressions;

namespace DSACharacterCreator.Core.Rules;

/// <summary>
/// Würfelausdruck wie <c>160+2W20</c>, <c>G-110-2W6</c> oder <c>G-100±2W6</c>. <c>G</c> steht für einen Bezugswert
/// (beim Gewicht die Größe). <c>±</c>: gerade Ergebnisse werden addiert, ungerade subtrahiert.
/// </summary>
public sealed partial class DiceExpression
{
    private enum Sign { Plus, Minus, EvenPlusOddMinus }

    private sealed record Term(Sign Sign, int Count, int Sides, bool Reference);

    private readonly IReadOnlyList<Term> _terms;

    private DiceExpression(string text, IReadOnlyList<Term> terms)
    {
        Text = text;
        _terms = terms;
    }

    public string Text { get; }

    /// <summary>Enthält der Ausdruck <c>G</c>?</summary>
    public bool UsesReference => _terms.Any(t => t.Reference);

    [GeneratedRegex(@"\G\s*([+\-–±])?\s*(?:(\d+)\s*W\s*(\d+)|(\d+)|(G))\s*")]
    private static partial Regex TermPattern();

    public static bool TryParse(string? text, out DiceExpression expression)
    {
        expression = null!;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var terms = new List<Term>();
        var position = 0;
        foreach (Match m in TermPattern().Matches(text))
        {
            if (m.Index != position) return false;
            position = m.Index + m.Length;
            var sign = m.Groups[1].Value switch
            {
                "-" or "–" => Sign.Minus,
                "±" => Sign.EvenPlusOddMinus,
                "" when terms.Count > 0 => (Sign?)null,
                _ => Sign.Plus,
            };
            if (sign is null) return false;
            if (m.Groups[2].Success)
            {
                var count = int.Parse(m.Groups[2].Value);
                var sides = int.Parse(m.Groups[3].Value);
                if (count < 1 || sides < 2) return false;
                terms.Add(new Term(sign.Value, count, sides, false));
            }
            else if (sign == Sign.EvenPlusOddMinus) return false;
            else if (m.Groups[4].Success) terms.Add(new Term(sign.Value, int.Parse(m.Groups[4].Value), 1, false));
            else terms.Add(new Term(sign.Value, 0, 0, true));
        }
        if (position != text.Length || terms.Count == 0) return false;
        expression = new DiceExpression(text, terms);
        return true;
    }

    public static DiceExpression Parse(string text) =>
        TryParse(text, out var e) ? e : throw new FormatException($"Ungültiger Würfelausdruck '{text}'.");

    public int Roll(Random random, int reference = 0) => Evaluate(t => Enumerable.Range(0, t.Count).Sum(_ => random.Next(1, t.Sides + 1)), reference);

    public int Min(int reference = 0) => Evaluate(t => t.Count, reference, lowest: true);

    public int Max(int reference = 0) => Evaluate(t => t.Count * t.Sides, reference, lowest: false);

    private int Evaluate(Func<Term, int> dice, int reference, bool? lowest = null)
    {
        var total = 0;
        foreach (var t in _terms)
        {
            if (t.Reference) total += t.Sign == Sign.Minus ? -reference : reference;
            else if (t.Sides == 1) total += t.Sign == Sign.Minus ? -t.Count : t.Count;
            else if (t.Sign == Sign.EvenPlusOddMinus)
            {
                // Für Min/Max die größte ungerade bzw. gerade Augensumme
                var value = lowest switch
                {
                    true => -(t.Count * t.Sides - ((t.Count * t.Sides) % 2 == 0 ? 1 : 0)),
                    false => t.Count * t.Sides - ((t.Count * t.Sides) % 2),
                    null => dice(t) is var r && r % 2 == 0 ? r : -r,
                };
                total += value;
            }
            else
            {
                // Bei abgezogenen Würfeln liefert das höchste Ergebnis den kleinsten Wert
                var value = lowest is { } low && t.Sign == Sign.Minus
                    ? (low ? t.Count * t.Sides : t.Count)
                    : dice(t);
                total += t.Sign == Sign.Minus ? -value : value;
            }
        }
        return total;
    }

    public override string ToString() => Text;
}
