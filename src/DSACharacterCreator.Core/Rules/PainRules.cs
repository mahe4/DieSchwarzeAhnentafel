namespace DSACharacterCreator.Core.Rules;

/// <summary>
/// Zustand Schmerz durch LeP-Verlust (Wiki: Zustand Schmerz): je eine Stufe, wenn die Lebenspunkte auf drei Viertel,
/// die Hälfte und ein Viertel der Gesamt-Lebensenergie gesunken sind, eine weitere bei 5 LeP oder weniger.
/// </summary>
public static class PainRules
{
    public const int LowLifePoints = 5;

    /// <summary>LeP-Werte, ab denen (gleich oder weniger) eine Stufe Schmerz hinzukommt: ¾, ½, ¼ (abgerundet), 5.</summary>
    public static IReadOnlyList<int> Thresholds(int lifePoints) =>
        [lifePoints * 3 / 4, lifePoints / 2, lifePoints / 4, LowLifePoints];

    /// <summary>Stufen Schmerz bei <paramref name="current"/> von <paramref name="lifePoints"/> LeP (höchstens IV).</summary>
    public static int Level(int current, int lifePoints) =>
        Math.Min(4, Thresholds(lifePoints).Count(t => current <= t));
}
