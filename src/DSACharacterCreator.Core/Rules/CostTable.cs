using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Core.Rules;

/// <summary>
/// Kostentabelle für Steigerungen. Eine Zeile gibt die Kosten an, um einen Wert <em>auf</em> den Zeilenwert zu steigern
/// (Zeile „bis 12“ gilt für alle Zielwerte bis 12).
/// </summary>
public sealed class CostTable
{
    private readonly IReadOnlyDictionary<ImprovementCost, int> _activation;
    private readonly IReadOnlyList<CostRow> _rows;

    public CostTable(IReadOnlyDictionary<ImprovementCost, int> activation, IReadOnlyList<CostRow> rows)
    {
        _activation = activation;
        _rows = rows.OrderBy(r => r.UpTo).ToList();
    }

    /// <summary>Höchster Zielwert, für den die Tabelle Kosten angibt.</summary>
    public int MaxValue => _rows[^1].UpTo;

    /// <summary>Kosten der Aktivierung auf 0 (nicht für Eigenschaften).</summary>
    public int Activation(ImprovementCost cost) =>
        _activation.TryGetValue(cost, out var ap)
            ? ap
            : throw new ArgumentException($"Spalte {cost} hat keine Aktivierungskosten.", nameof(cost));

    /// <summary>Kosten, um von <paramref name="value"/> − 1 auf <paramref name="value"/> zu steigern.</summary>
    public int StepCost(ImprovementCost cost, int value)
    {
        foreach (var row in _rows)
        {
            if (value <= row.UpTo) return row.Costs[cost];
        }
        throw new ArgumentOutOfRangeException(nameof(value), value, $"Die Kostentabelle reicht nur bis {MaxValue}.");
    }

    /// <summary>
    /// Kosten für eine Steigerung Stufe um Stufe von <paramref name="from"/> auf <paramref name="to"/>.
    /// Senkungen ergeben den negativen Betrag (Rückerstattung während der Erschaffung).
    /// </summary>
    public int Cost(ImprovementCost cost, int from, int to)
    {
        if (to < from) return -Cost(cost, to, from);
        var sum = 0;
        for (var value = from + 1; value <= to; value++) sum += StepCost(cost, value);
        return sum;
    }
}

public sealed record CostRow(int UpTo, IReadOnlyDictionary<ImprovementCost, int> Costs);
