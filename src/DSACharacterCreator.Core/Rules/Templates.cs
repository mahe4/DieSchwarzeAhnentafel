using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Core.Rules;

/// <summary>
/// Kultur und Profession als Vorlagen: Beim Wechsel werden eigene Talent-/Kampftechnik-Anpassungen verworfen und die
/// Sonderfertigkeiten (sowie die Voraussetzungen der Profession, z. B. Zauberer und Tradition) der alten Vorlage durch
/// die der neuen ersetzt. Selbst hinzugefügte Einträge bleiben.
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
        creation.Traits.RemoveAll(e => e.Source == ProfessionSource);
        creation.Spells.RemoveAll(e => e.Source == ProfessionSource);
        if (rules.FindProfession(professionId) is not { } found) return;
        // Werte je Erfahrungsgrad (z. B. Elementarist): passend zum Erfahrungsgrad des Helden
        var profession = rules.ProfessionForLevel(found, rules.ExperienceLevel(creation.ExperienceLevel).Id);
        // Voraussetzungen (Zauberer, Tradition, Prinzipientreue …) nur, wenn der Held sie nicht schon selbst gewählt hat.
        foreach (var t in profession.PrerequisiteEntries)
        {
            var list = t.Kind == TemplateEntryKind.SpecialAbility ? creation.SpecialAbilities : creation.Traits;
            if (list.All(e => e.Id != t.Id)) list.Add(Entry(t));
        }
        foreach (var t in profession.SpecialAbilities)
            creation.SpecialAbilities.Add(Entry(t));
        // Traditionsartefakte der Vorlage sind bei der Erschaffung gebunden und die permanenten AsP zurückgekauft
        // (so rechnen die AP-Werte der Professionen im Wiki, z. B. Bindung des Stabes 10 AP + 2 × 2 AP).
        var bound = profession.SpecialAbilities.Sum(t => rules.FindSpecialAbility(t.Id) is { } def ? TraitRules.PermanentAstral(def, Entry(t)) : 0);
        if (bound > 0) creation.BoughtEnergies[Energies.AstralRepurchased] = bound;
        else creation.BoughtEnergies.Remove(Energies.AstralRepurchased);
        // Zauber und Liturgien; schon selbst aktivierte erhalten den Wert der Vorlage nicht doppelt.
        foreach (var t in profession.Spells.Where(t => creation.Spells.All(e => e.Id != t.Id)))
            creation.Spells.Add(new SpellEntry { Id = t.Id, Value = t.Value, Source = ProfessionSource });
    }

    /// <summary>Zauber-Auswahl der Profession: ersetzt den bisher gewählten Eintrag dieser Auswahl.</summary>
    /// <param name="value">Wert laut Vorlage („Archofaxius oder Ignifaxius 7“); <c>null</c> bei Zaubertricks.</param>
    public static void ChooseProfessionSpell(CreationLayer creation, int index, string? spellId, int? value = null)
    {
        while (creation.ProfessionSpellChoices.Count <= index) creation.ProfessionSpellChoices.Add(null);
        if (creation.ProfessionSpellChoices[index] is { } old)
            creation.Spells.RemoveAll(e => e.Id == old && e.Source == ProfessionSource);
        creation.ProfessionSpellChoices[index] = spellId;
        if (spellId is not null && creation.Spells.All(e => e.Id != spellId))
            creation.Spells.Add(new SpellEntry { Id = spellId, Value = value, Source = ProfessionSource });
    }

    private static TraitEntry Entry(TemplateEntry t) => new()
    {
        Id = t.Id, Level = t.Level, Option = t.Option, Specification = t.Specification, Source = ProfessionSource,
    };

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

    /// <summary>
    /// Gleicht beim Import (Optolith, Dark Aid) die Einträge der Vorlagen mit den importierten ab: Passende Einträge
    /// behalten ihre Herkunft, fehlende der Profession entfallen, die übrigen kommen als eigene Einträge hinzu. Die
    /// Einträge der Kultur (Muttersprache, Ortskenntnis) bleiben; die Muttersprache übernimmt die importierte Sprache.
    /// </summary>
    /// <param name="imported">Einträge mit Kennzeichen „Muttersprache“ (kostenlose Sprache der Kultur).</param>
    public static void MergeImported(List<TraitEntry> list, IEnumerable<(TraitEntry Entry, bool Native)> imported)
    {
        var matched = new HashSet<TraitEntry>();
        foreach (var (entry, native) in imported)
        {
            var existing = native
                ? list.FirstOrDefault(e => !matched.Contains(e) && e.Source == CultureSource && e.Id == entry.Id &&
                                           (e.Option is null || e.Option == entry.Option))
                // Kostenlose Einträge der Kultur (Ortskenntnis) stehen nicht in den importierten Daten; eine dort
                // gekaufte zweite Ortskenntnis ist ein eigener Eintrag.
                : list.FirstOrDefault(e => !matched.Contains(e) && e.Id == entry.Id && e.Option == entry.Option &&
                                           !(e.Source == CultureSource && e.Free));
            if (existing is null)
            {
                if (native)
                {
                    entry.Source = CultureSource;
                    entry.Free = true;
                }
                list.Add(entry);
                matched.Add(entry);
                continue;
            }
            matched.Add(existing);
            existing.Option = entry.Option;
            existing.Level = entry.Level;
            existing.Specification = entry.Specification ?? existing.Specification;
            existing.ManualAp = entry.ManualAp;
        }
        list.RemoveAll(e => e.Source == ProfessionSource && !matched.Contains(e));
    }
}
