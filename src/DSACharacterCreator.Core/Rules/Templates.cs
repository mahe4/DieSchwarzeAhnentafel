using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Core.Rules;

/// <summary>
/// Kultur und Profession als Vorlagen: Beim Wechsel werden eigene Talent-/Kampftechnik-Anpassungen verworfen und die
/// Sonderfertigkeiten der alten Vorlage durch die der neuen ersetzt. Selbst hinzugefügte Sonderfertigkeiten bleiben.
/// </summary>
public static class Templates
{
    public const string CultureSource = "culture";
    public const string ProfessionSource = "profession";

    public static void ChangeCulture(CreationLayer creation, string? cultureId, RuleData rules)
    {
        if (cultureId == creation.Culture) return;
        creation.SetCulture(cultureId);
        creation.SpecialAbilities.RemoveAll(e => e.Source == CultureSource);
        if (rules.FindCulture(cultureId) is not { } culture) return;

        // Muttersprache auf Stufe III und Ortskenntnis sind kostenlos.
        if (rules.SpecialAbilities.FirstOrDefault(s => s.Name == "Sprache") is { } language)
            creation.SpecialAbilities.Add(new TraitEntry
            {
                Id = language.Id, Level = 3, Option = MotherTongue(culture, rules)?.Id, Source = CultureSource, Free = true,
            });
        if (rules.SpecialAbilities.FirstOrDefault(s => s.Name == "Ortskenntnis") is { } local)
            creation.SpecialAbilities.Add(new TraitEntry { Id = local.Id, Source = CultureSource, Free = true });
    }

    public static void ChangeProfession(CreationLayer creation, string? professionId, RuleData rules)
    {
        if (professionId == creation.Profession) return;
        creation.SetProfession(professionId);
        creation.SpecialAbilities.RemoveAll(e => e.Source == ProfessionSource);
        if (rules.FindProfession(professionId) is not { } profession) return;
        foreach (var t in profession.SpecialAbilities)
            creation.SpecialAbilities.Add(new TraitEntry
            {
                Id = t.Id, Level = t.Level, Option = t.Option, Specification = t.Specification, Source = ProfessionSource,
            });
    }

    /// <summary>
    /// Muttersprache aus dem Kulturtext („Garethi (je nach Provinz)“). Bei mehreren Möglichkeiten („… oder …“) bleibt
    /// die Wahl offen.
    /// </summary>
    public static LanguageDefinition? MotherTongue(CultureDefinition culture, RuleData rules)
    {
        var text = culture.LanguageText ?? "";
        if (text.Contains(" oder ", StringComparison.Ordinal)) return null;
        var first = text.Split('(', ',', ';')[0].Trim();
        return rules.Languages.FirstOrDefault(l => l.Name == first && l.Region == "Aventurien")
               ?? rules.Languages.FirstOrDefault(l => l.Name == first);
    }
}
