using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Core.Rules;

/// <summary>Permanente Verluste, Rückkauf und Zukauf von LeP/AsP/KaP.</summary>
public sealed partial class CharacterCalculator
{
    /// <summary>
    /// Zugekaufte Energien (Wiki, Erfahrung: Kostentabelle D; höchstens KO bzw. Leiteigenschaft). In der Erschaffung ist
    /// kein Zukauf vorgesehen (Heldenerschaffung, Schritt 8).
    /// </summary>
    private List<EnergyState> Energies(CreationLayer creation, bool inCreation, IReadOnlyDictionary<string, int> attr,
        TraditionInfo? magic, TraditionInfo? karma, Func<EffectTarget, int> effect, List<TraitState> specialAbilities,
        List<Issue> issues)
    {
        var result = new List<EnergyState>();
        int Stored(string id) => creation.BoughtEnergies.GetValueOrDefault(id);
        var hasAstral = effect(EffectTarget.AstralBase) > 0;
        var hasKarma = effect(EffectTarget.KarmaBase) > 0;

        // Permanente Verluste: Bindungen von Traditionsartefakten plus sonstige (von Hand eingetragen)
        var bindings = specialAbilities.Sum(t => TraitRules.PermanentAstral(t.Definition, t.Entry));
        var astralLost = bindings + Stored(Characters.Energies.AstralLost);
        var karmaLost = Stored(Characters.Energies.KarmaLost);
        result.Add(new EnergyState(Characters.Energies.AstralLost, "Permanent verlorene AsP", astralLost, hasAstral, null, 0,
            EnergyKind.Lost) { Fixed = bindings });
        result.Add(new EnergyState(Characters.Energies.KarmaLost, "Permanent verlorene KaP", karmaLost, hasKarma, null, 0, EnergyKind.Lost));

        // Rückkauf: 2 AP je Punkt, höchstens so viele wie verloren (auch schon bei der Erschaffung, z. B. bei Bindungen)
        void Repurchase(string id, string name, int lost)
        {
            var value = Stored(id);
            if (value > lost)
                issues.Add(new Issue(IssueSeverity.Error, "energy.repurchaseMax", RuleScope.Always,
                    $"Es sind nur {lost} {name} permanent verloren.", "energy:" + id));
            result.Add(new EnergyState(id, name + " zurückkaufen", value, lost > 0 || value > 0, lost,
                value * PackageCosts.AstralRepurchaseAp, EnergyKind.Repurchase));
        }
        Repurchase(Characters.Energies.AstralRepurchased, "AsP", astralLost);
        Repurchase(Characters.Energies.KarmaRepurchased, "KaP", karmaLost);

        // Zukauf (Kostentabelle D) nach der Erschaffung; vorher alle verlorenen Punkte zurückkaufen
        void Buy(string id, string name, bool available, int? max, int openLoss)
        {
            var value = Stored(id);
            var ap = rules.Costs.Cost(ImprovementCost.D, 0, Math.Min(value, rules.Costs.MaxValue));
            if (value > 0 && inCreation)
                issues.Add(new Issue(IssueSeverity.Error, "energy.creation", RuleScope.CreationOnly,
                    $"{name} lassen sich erst nach der Erschaffung zukaufen.", "energy:" + id));
            if (value > 0 && !available)
                issues.Add(new Issue(IssueSeverity.Error, "energy.unavailable", RuleScope.Always,
                    $"{name} kann nur zukaufen, wer über diese Energie verfügt.", "energy:" + id));
            else if (max is { } m && value > m)
                issues.Add(new Issue(IssueSeverity.Error, "energy.max", RuleScope.Always,
                    $"Höchstens {m} {name} zukaufen.", "energy:" + id));
            if (value > 0 && openLoss > 0)
                issues.Add(new Issue(IssueSeverity.Error, "energy.repurchaseFirst", RuleScope.Always,
                    $"Vor dem Zukauf erst die {openLoss} permanent verlorenen Punkte zurückkaufen.", "energy:" + id));
            result.Add(new EnergyState(id, name, value, available, max, ap, EnergyKind.Bought));
        }
        Buy(Characters.Energies.LifePoints, "Lebenspunkte", true, attr["KO"], 0);
        Buy(Characters.Energies.Astral, "Astralpunkte", hasAstral, magic?.PrimaryAttribute is { } ma ? attr[ma] : null,
            Math.Max(0, astralLost - Stored(Characters.Energies.AstralRepurchased)));
        Buy(Characters.Energies.Karma, "Karmapunkte", hasKarma, karma?.PrimaryAttribute is { } ka ? attr[ka] : null,
            Math.Max(0, karmaLost - Stored(Characters.Energies.KarmaRepurchased)));
        return result;
    }
}
