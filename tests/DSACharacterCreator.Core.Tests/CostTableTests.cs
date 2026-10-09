using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Core.Tests;

/// <summary>Beispiele aus dem Regel-Wiki, Heldenerschaffung Schritt 5 und 8.</summary>
public class CostTableTests
{
    private static readonly Rules.CostTable Costs = TestRules.Data.Costs;

    [Fact]
    public void SkillSteps_FollowColumn()
    {
        // Gassenwissen (C) von 7 auf 8 kostet 3 AP, auf 9 weitere 3 AP.
        Assert.Equal(3, Costs.Cost(ImprovementCost.C, 7, 8));
        Assert.Equal(6, Costs.Cost(ImprovementCost.C, 7, 9));
    }

    [Fact]
    public void Activation_ThenRaise()
    {
        // Ignifaxius (C): Aktivierung 3 AP, Steigerung auf 3 weitere 9 AP.
        Assert.Equal(3, Costs.Activation(ImprovementCost.C));
        Assert.Equal(9, Costs.Cost(ImprovementCost.C, 0, 3));
    }

    [Fact]
    public void AttributeCosts_UseColumnE()
    {
        Assert.Equal(15, Costs.StepCost(ImprovementCost.E, 14));
        Assert.Equal(30, Costs.StepCost(ImprovementCost.E, 15));
        Assert.Equal(90, Costs.StepCost(ImprovementCost.E, 19));
        // 100 Eigenschaftspunkte ohne Wert über 14 kosten 540 AP (36 Punkte über 8).
        Assert.Equal(540, 36 * Costs.StepCost(ImprovementCost.E, 14));
        // Ein Wert 15 statt zwei Werten 14/14 → 555 AP: 6×15 + 30 + …; hier: 8→15 = 7 Stufen.
        Assert.Equal(6 * 15 + 30, Costs.Cost(ImprovementCost.E, 8, 15));
    }

    [Fact]
    public void HighValues_UseRows()
    {
        Assert.Equal(1 + 2 + 3, Costs.Cost(ImprovementCost.A, 11, 14));
        Assert.Equal(56, Costs.StepCost(ImprovementCost.D, 25));
    }

    [Fact]
    public void Lowering_RefundsCost()
    {
        Assert.Equal(-6, Costs.Cost(ImprovementCost.C, 9, 7));
    }

    [Fact]
    public void BeyondTable_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Costs.StepCost(ImprovementCost.A, 26));
    }
}
