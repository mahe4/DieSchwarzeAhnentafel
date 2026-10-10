using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Core.Rules;

/// <summary>
/// Kosten von Kultur- und Professionspaketen. Ein Paket kostet immer genau so viel wie sein Inhalt; der AP-Wert im
/// Wiki dient nur zum Vergleich. Weicht er ab, liegt vermutlich ein Fehler im Wiki vor.
/// </summary>
/// <remarks>
/// Sonderfertigkeiten, Sprachen, Zauber und Liturgien der Professionen sind noch nicht strukturiert erfasst. Ihr Anteil
/// wird vorläufig als Rest „Wiki-AP minus berechnete Talente/Kampftechniken“ angesetzt (<see cref="ProfessionCost.Unmodeled"/>)
/// und mit Meilenstein 4/6 durch berechnete Kosten ersetzt.
/// </remarks>
public sealed class PackageCosts(RuleData rules)
{
    private int SkillCost(string skill, int value) =>
        rules.Costs.Cost(rules.Skills.First(s => s.Id == skill).ImprovementCost, 0, value);

    private int CombatCost(string id, int value) =>
        rules.Costs.Cost(rules.CombatTechniques.First(c => c.Id == id).ImprovementCost,
            rules.Creation.CombatTechniqueStartValue, value);

    public int CultureCost(CulturePackage package) => package.Skills.Sum(v => SkillCost(v.Skill, v.Value));

    /// <summary>Talentwerte des Professionspakets (Variante ersetzt Werte).</summary>
    public static Dictionary<string, int> ProfessionSkills(ProfessionDefinition profession, ProfessionVariant? variant)
    {
        var result = profession.Skills.ToDictionary(v => v.Skill, v => v.Value);
        foreach (var v in variant?.Skills ?? []) result[v.Skill] = v.Value;
        return result;
    }

    /// <summary>
    /// Kampftechnikwerte des Professionspakets: feste Werte, gewählte Optionen (jeweils das Maximum), dann Ersetzungen der
    /// Variante. Ohne Wahl (<c>null</c>) wird für die Kostenbasis die günstigste Option angesetzt.
    /// </summary>
    public Dictionary<string, int> ProfessionCombatTechniques(ProfessionDefinition profession, ProfessionVariant? variant,
        IReadOnlyList<string?>? choices)
    {
        var result = new Dictionary<string, int>();
        void Raise(string id, int value) => result[id] = Math.Max(result.GetValueOrDefault(id), value);

        foreach (var v in profession.CombatTechniques) Raise(v.CombatTechnique, v.Value);
        var list = profession.CombatTechniqueChoices ?? [];
        for (var i = 0; i < list.Count; i++)
        {
            var choice = list[i];
            var pick = choices?.ElementAtOrDefault(i);
            if (pick is null || !choice.Options.Contains(pick))
                pick = choice.Options.MinBy(o => CombatCost(o, choice.Value))!;
            Raise(pick, choice.Value);
        }
        foreach (var v in variant?.CombatTechniques ?? []) result[v.CombatTechnique] = v.Value;
        return result;
    }

    /// <param name="choices">Gewählte Kampftechniken; <c>null</c> = günstigste Optionen (Kostenbasis).</param>
    public ProfessionCost ProfessionCost(ProfessionDefinition profession, ProfessionVariant? variant,
        IReadOnlyList<string?>? choices)
    {
        int Skills(ProfessionVariant? v) => ProfessionSkills(profession, v).Sum(x => SkillCost(x.Key, x.Value));
        int Combat(ProfessionVariant? v, IReadOnlyList<string?>? c) =>
            ProfessionCombatTechniques(profession, v, c).Sum(x => CombatCost(x.Key, x.Value));

        // Rest für noch nicht strukturierte Teile, bezogen auf die günstigsten Kampftechnik-Optionen.
        // Nennt das Wiki für das Paket keinen eigenen Wert (nur je Variante), gibt es keinen Paket-Rest.
        int? baseRest = profession.WikiAp - Skills(null) - Combat(null, null);
        var rest = Math.Max(0, baseRest ?? 0);
        int? wikiAp = variant?.WikiAp ?? profession.WikiAp;
        string? mismatch = baseRest < 0
            ? $"Das Wiki nennt {profession.WikiAp} AP, allein Talente und Kampftechniken kosten {profession.WikiAp - baseRest} AP."
            : null;

        if (variant is not null)
        {
            var computed = Skills(variant) + Combat(variant, null);
            if (variant.Text is not null || baseRest is null)
            {
                // Variante ändert auch Nicht-Strukturiertes (z. B. Sonderfertigkeiten) bzw. das Paket hat keinen eigenen
                // Wiki-Wert: eigener Rest der Variante.
                rest = Math.Max(0, variant.WikiAp - computed);
                if (variant.WikiAp < computed)
                    mismatch = $"Das Wiki nennt für die Variante {variant.WikiAp} AP, allein Talente und Kampftechniken kosten {computed} AP.";
            }
            else if (computed + rest != variant.WikiAp)
            {
                mismatch = $"Das Wiki nennt für die Variante {variant.WikiAp} AP, die Änderungen ergeben {computed + rest} AP.";
            }
        }

        return new ProfessionCost(Skills(variant), Combat(variant, choices), rest, wikiAp, mismatch);
    }
}

/// <param name="Unmodeled">Vorläufiger Anteil für Sonderfertigkeiten, Sprachen, Zauber, Liturgien (Wiki-Rest).</param>
/// <param name="WikiMismatch">Beschreibung, wenn Wiki-AP und Inhalt nicht zusammenpassen; sonst <c>null</c>.</param>
/// <param name="WikiAp">AP-Wert laut Wiki (Variante bzw. Paket); <c>null</c>, wenn das Wiki keinen einzelnen Wert nennt.</param>
public sealed record ProfessionCost(int Skills, int CombatTechniques, int Unmodeled, int? WikiAp, string? WikiMismatch)
{
    public int Total => Skills + CombatTechniques + Unmodeled;
}
