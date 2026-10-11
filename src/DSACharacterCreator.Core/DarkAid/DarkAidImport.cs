using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.Core.DarkAid;

/// <summary>Importierter Held und Hinweise, was nicht oder nur ungefähr übernommen werden konnte.</summary>
public sealed record DarkAidImportResult(Character Character, IReadOnlyList<string> Notes);

/// <summary>
/// Liest einen Helden von The Dark Aid (<c>*.tdc</c>, JSON, Version 6). Dark Aid speichert nur den Endstand; Kultur und
/// Profession werden als Vorlage gesetzt und alle Werte so angepasst, dass sie dem Dark-Aid-Stand entsprechen. Erschaffene
/// Helden (<c>iscreated</c>) kommen als abgeschlossene Erschaffung ins Protokoll, die AP-Boni als Gutschriften.
/// </summary>
public static class DarkAidImport
{
    public const string FileExtension = ".tdc";

    /// <summary>Größe in 1/10000 Schritt (= 1/100 Halbfinger).</summary>
    internal const int HeightUnitsPerHalfFinger = 100;

    /// <summary>Gewicht in 1/400000 Stein.</summary>
    internal const int WeightUnitsPerStone = 400_000;

    /// <summary>Sieht der Text wie ein Dark-Aid-Held aus?</summary>
    public static bool IsDarkAidHero(string json)
    {
        try
        {
            return JsonNode.Parse(json) is JsonObject o && o.ContainsKey("race") && o.ContainsKey("attributes") &&
                   o["rules"]?["rulesystem"] is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <exception cref="InvalidDataException">Die Datei ist kein lesbarer Dark-Aid-Held.</exception>
    public static DarkAidImportResult Read(string json, RuleData rules, DarkAidMapping map, SourceSelection sources)
    {
        JsonObject hero;
        try
        {
            hero = JsonNode.Parse(json) as JsonObject ?? throw new InvalidDataException("Die Datei ist kein Dark-Aid-Held.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Die Datei ist kein Dark-Aid-Held.", ex);
        }
        if (!hero.ContainsKey("attributes") || !hero.ContainsKey("race"))
            throw new InvalidDataException("Die Datei ist kein Dark-Aid-Held.");
        try
        {
            return new Importer(hero, rules, map, sources).Run();
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException or KeyNotFoundException)
        {
            throw new InvalidDataException("Der Dark-Aid-Held konnte nicht gelesen werden: " + ex.Message, ex);
        }
    }

    private sealed class Importer(JsonObject hero, RuleData rules, DarkAidMapping map, SourceSelection sources)
    {
        private readonly List<string> _notes = [];
        private readonly List<string> _skipped = [];
        private readonly HashSet<string> _books = new(sources.EnabledBookIds, StringComparer.Ordinal);
        private readonly Character _character = new();

        /// <summary>Zaubererweiterungen alter Dark-Aid-Fassungen als eigene SF „Zauber (Erweiterung)“.</summary>
        private readonly List<string> _enhancementNames = [];

        private CreationLayer Creation => _character.Creation;

        public DarkAidImportResult Run()
        {
            var c = _character;
            c.Name = Str(hero["name"])?.Trim() ?? "";
            Creation.ExperienceLevel = Map(map.ExperienceLevels, Str(hero["xp"]?["startinglevel"]), "Erfahrungsgrad");
            var level = rules.ExperienceLevel(Creation.ExperienceLevel);
            Origin(level);
            Attributes();
            Activatables();
            Spells();
            Energies();
            Personal();
            Equipment();
            Books();
            Adjustments();
            Money();
            Finish();

            if (_skipped.Count > 0)
                _notes.Insert(0, "Nicht übernommen (in der App nicht vorhanden): " + string.Join(", ", _skipped.Distinct()) + ".");
            return new DarkAidImportResult(c, _notes);
        }

        // ---------------------------------------------------------------- Herkunft

        private void Origin(ExperienceLevel level)
        {
            var race = Str(hero["race"]) is { } r ? map.Races.GetValueOrDefault(r) : null;
            if (race is null && Str(hero["race"]) is { } unknown) Skip(unknown, "Spezies");
            Creation.Species = race?.Species;
            if (race?.Table is { } table && rules.FindAppearance(race.Species)?.Choices.Any(x => x.Name == table) == true)
                _character.Personal.AppearanceTable = table;
            if (rules.FindSpecies(Creation.Species) is { } species)
            {
                Need(species.Publications);
                // Dark Aid speichert die gewählte Eigenschaft der Spezies (+1) nicht; erkennbar ist sie nur, wenn der Wert
                // über dem Höchstwert des Erfahrungsgrads liegt.
                foreach (var change in species.AttributeChanges.Where(a => a.IsChoice))
                {
                    var above = change.Value > 0
                        ? change.Options.FirstOrDefault(o => AttributeValue(o) > level.MaxAttribute)
                        : null;
                    Creation.SpeciesAttributeChoices.Add(above);
                }
            }

            var culture = Map(map.Cultures, Str(hero["culture"]), "Kultur");
            Templates.ChangeCulture(Creation, culture, rules);
            if (rules.FindCulture(culture) is { } cultureDef) Need(cultureDef.Publications);

            DarkAidProfession? profession = null;
            if (Str(hero["profession"]) is { Length: > 0 } key)
            {
                profession = map.Professions.GetValueOrDefault(key);
                if (profession is null)
                    _notes.Add($"Die Profession „{Str(hero["professionname"]) ?? key}“ gibt es in der App nicht: Die Werte sind " +
                               "übernommen, eine Vorlage ist nicht gesetzt.");
            }
            Templates.ChangeProfession(Creation, profession?.Id, rules);
            if (rules.FindProfession(profession?.Id) is { } professionDef) Need(professionDef.Publications);
            Creation.SetProfessionVariant(profession?.Variant);
        }

        private int? AttributeValue(string attribute) =>
            hero["attributes"] is JsonArray values
                ? values.Where(v => map.Attributes.GetValueOrDefault(Str(v?["id"]) ?? "") == attribute)
                    .Select(v => Int(v?["level"])).FirstOrDefault()
                : null;

        // ---------------------------------------------------------------- Eigenschaften

        private void Attributes()
        {
            if (hero["attributes"] is not JsonArray values) return;
            foreach (var v in values)
                if (map.Attributes.TryGetValue(Str(v?["id"]) ?? "", out var attribute) && Int(v?["level"]) is { } n)
                    Creation.Attributes[attribute] = n;
        }

        // ---------------------------------------------------------------- Vor-/Nachteile und Sonderfertigkeiten

        private void Activatables()
        {
            var traitRules = new TraitRules(rules);
            var species = rules.FindSpecies(Creation.Species);
            // Automatische Vor-/Nachteile der Spezies setzt die App selbst.
            var automatic = (species?.AutomaticAdvantages.Select(n => traitRules.Resolve(n, false)) ?? [])
                .Concat(species?.AutomaticDisadvantages.Select(n => traitRules.Resolve(n, true)) ?? [])
                .OfType<(TraitDefinition Definition, TraitEntry Entry)>()
                .Select(r => r.Definition.Id).ToHashSet();

            // Muttersprachen: kostenlose Sprachen der Kultur; Dark Aid führt sie zusätzlich unter den Sonderfertigkeiten.
            var native = Instances("mothertongues").Select(t => Str(t["variant"]?["ruleelement"]?["id"])).OfType<string>().ToList();

            var traits = new List<(TraitEntry, bool)>();
            var abilities = new List<(TraitEntry, bool)>();
            var instances = Instances("disadvantages").Concat(Instances("specialabilities")).Concat(Instances("enchantments")).ToList();
            foreach (var mother in Instances("mothertongues"))
                if (!instances.Any(i => Str(i["id"]) == "sprache" &&
                                        Str(i["variant"]?["ruleelement"]?["id"]) == Str(mother["variant"]?["ruleelement"]?["id"])))
                    instances.Add(mother);

            var firstLocal = true;
            foreach (var instance in instances)
            {
                var entry = Entry(instance, out var def);
                if (entry is null || def is null) continue;
                if (!def.IsSpecialAbility && automatic.Contains(def.Id)) continue;
                Need(def.Publications);
                var isNative = def.OptionTarget == TraitOptionTarget.Language && Str(instance["id"]) == "sprache" &&
                               native.Remove(Str(instance["variant"]?["ruleelement"]?["id"]) ?? "");
                // Bei Dark Aid ist die erste Ortskenntnis kostenlos: die der Kultur
                if (Str(instance["id"]) == "ortskenntnis" && firstLocal)
                {
                    isNative = true;
                    firstLocal = false;
                }
                (def.IsSpecialAbility ? abilities : traits).Add((entry, isNative));
            }
            Templates.MergeImported(Creation.Traits, traits);
            Templates.MergeImported(Creation.SpecialAbilities, abilities);
        }

        private TraitEntry? Entry(JsonObject instance, out TraitDefinition? def)
        {
            def = null;
            var key = Str(instance["id"]);
            var variant = instance["variant"] as JsonObject;

            // Selbst angelegte Einträge (GUID): alte Form der Zaubererweiterungen „Wolfstatze (Schnellverwandlung)“,
            // werden nach den Zaubern zugeordnet
            if (key is null)
            {
                if (Str(instance["ruleelement"]?["name"]) is { } name) _enhancementNames.Add(name);
                return null;
            }

            DarkAidActivatable? mapped;
            if (key is "magischetradition" or "geweihtentradition")
            {
                var tradition = Str(variant?["ruleelement"]?["id"]) ?? "";
                mapped = map.Traditions.TryGetValue(tradition, out var id) ? new DarkAidActivatable(id, null, null) : null;
                if (mapped is null)
                {
                    Skip(tradition, "Tradition");
                    return null;
                }
            }
            else mapped = map.Activatables.GetValueOrDefault(key);
            def = mapped is null ? null : rules.FindTrait(mapped.Id) ?? rules.FindSpecialAbility(mapped.Id);
            if (mapped is null || def is null)
            {
                Skip(key, "Eintrag");
                return null;
            }

            var entry = new TraitEntry { Id = def.Id, Option = mapped.Option, Specification = mapped.Text };
            if (def.MaxLevel is not null) entry.Level = Int(instance["level"]) ?? 1;
            if (variant is not null && key is not ("magischetradition" or "geweihtentradition") && !Variant(def, entry, variant))
            {
                Skip($"{key}:{Str(variant["ruleelement"]?["id"]) ?? Str(variant["name"])}", "Auswahl");
                return null;
            }
            return entry;
        }

        /// <summary>Auswahl eines Eintrags (Sprache, Talent, freier Text mit Kosten …) übernehmen.</summary>
        private bool Variant(TraitDefinition def, TraitEntry entry, JsonObject variant)
        {
            switch (Str(variant["type"]))
            {
                case "ruleelement":
                    if (!Reference(def, entry, Str(variant["ruleelement"]?["type"]) ?? "", Str(variant["ruleelement"]?["id"]) ?? ""))
                        return false;
                    if (Str(variant["text"]) is { Length: > 0 } dialect) entry.Specification = Join(entry.Specification, dialect);
                    return true;
                case "string" or "terrain":
                    Choice(def, entry, Str(variant["name"]));
                    return true;
                case "stringwithcost":
                    Choice(def, entry, Str(variant["name"]));
                    if (def.Cost.Type == TraitCostType.Manual) entry.ManualAp = Int(variant["apcost"]);
                    return true;
                case "tradesecret":
                    var secret = Str(variant["tradesecret"]?["id"]) ?? "";
                    entry.Specification = map.Names.GetValueOrDefault("tradesecret")?.GetValueOrDefault(secret) ?? secret;
                    if (def.Cost.Type == TraitCostType.Manual) entry.ManualAp = Int(variant["apcost"]);
                    return true;
                case "skillspecialization":
                    entry.Option = map.Skills.GetValueOrDefault(Str(variant["skill"]) ?? "");
                    entry.Specification = Str(variant["application"]);
                    return entry.Option is not null;
                case "specializedknowledge":
                    entry.Option = map.Skills.GetValueOrDefault(Str(variant["skill"]) ?? "");
                    entry.Specification = string.Join(", ", new[] { Str(variant["application1"]), Str(variant["application2"]) }
                        .OfType<string>().Distinct());
                    return entry.Option is not null;
                default:
                    if (Str(variant["name"]) is { } text) entry.Specification = text;
                    return true;
            }
        }

        /// <summary>Text als Ausprägung, wenn der Eintrag welche hat („Arroganz“), sonst als Präzisierung.</summary>
        private static void Choice(TraitDefinition def, TraitEntry entry, string? text)
        {
            if (text is null) return;
            if (def.Cost.Type == TraitCostType.ByOption &&
                def.Cost.Options!.FirstOrDefault(o => string.Equals(o.Name, text, StringComparison.OrdinalIgnoreCase)) is { } option)
                entry.Option = option.Name;
            else entry.Specification = text;
        }

        /// <summary>
        /// Gewählte Sprache, Schrift, Fertigkeit, Kampftechnik oder gewählter Zauber: als Auswahl, wenn der Eintrag eine
        /// verlangt; bei Ausprägungen nach Steigerungsfaktor („B-Zauber“ beim Lieblingszauber) die passende Ausprägung; sonst
        /// der Name als Präzisierung.
        /// </summary>
        private bool Reference(TraitDefinition def, TraitEntry entry, string type, string id)
        {
            string? ours = type switch
            {
                "language" => map.Languages.GetValueOrDefault(id),
                "script" => map.Scripts.GetValueOrDefault(id),
                "skill" => map.Skills.GetValueOrDefault(id),
                "combattechnique" => map.CombatTechniques.GetValueOrDefault(id),
                "spell" or "chant" => map.Spells.GetValueOrDefault(id),
                _ => null,
            };
            var name = ours is null
                ? map.Names.GetValueOrDefault(type)?.GetValueOrDefault(id)
                : rules.FindLanguage(ours)?.Name ?? rules.FindScript(ours)?.Name ?? rules.FindSkill(ours)?.Name ??
                  rules.FindCombatTechnique(ours)?.Name ?? rules.FindSpell(ours)?.Name;
            if (name is null) return false;

            switch (def.OptionTarget)
            {
                case TraitOptionTarget.Language when type == "language":
                case TraitOptionTarget.Script when type == "script":
                case TraitOptionTarget.CombatTechnique when type == "combattechnique":
                // „Fertigkeit“ umfasst auch Zauber und Liturgien (Begabung)
                case TraitOptionTarget.Skill when type is "skill" or "spell" or "chant":
                    entry.Option = ours;
                    return true;
                case TraitOptionTarget.Property or TraitOptionTarget.Aspect:
                    entry.Option = name;
                    return true;
            }
            entry.Specification = name;
            if (def.Cost.Type == TraitCostType.ByOption && rules.FindSpell(ours)?.ImprovementCost is { } factor)
                entry.Option = def.Cost.Options!.FirstOrDefault(o => o.Name.StartsWith(factor + "-", StringComparison.Ordinal))?.Name;
            return true;
        }

        // ---------------------------------------------------------------- Zauber und Liturgien

        private void Spells()
        {
            var imported = new List<(string Id, int? Value, List<string> Enhancements)>();
            foreach (var instance in Instances("spells").Concat(Instances("chants")))
            {
                var key = Str(instance["id"]) ?? "";
                if (!map.Spells.TryGetValue(key, out var id) || rules.FindSpell(id) is not { } def)
                {
                    Skip(key, "Zauber");
                    continue;
                }
                Need(def.Publications);
                var enhancements = new List<string>();
                if (instance["enhancements"] is JsonArray list)
                    foreach (var e in list.Select(Str).OfType<string>())
                    {
                        if (map.Enhancements.GetValueOrDefault(key)?.GetValueOrDefault(e) is { } enhancement)
                            enhancements.Add(enhancement);
                        else Skip($"{key}/{e}", "Erweiterung");
                    }
                imported.Add((id, Int(instance["level"]), enhancements));
            }

            var matched = new HashSet<SpellEntry>();
            foreach (var (id, value, enhancements) in imported)
            {
                var existing = Creation.Spells.FirstOrDefault(e => e.Id == id && !matched.Contains(e));
                if (existing is null) Creation.Spells.Add(existing = new SpellEntry { Id = id });
                existing.Value = rules.FindSpell(id)?.ImprovementCost is null ? null : value ?? 0;
                existing.Enhancements = enhancements;
                matched.Add(existing);
            }
            Creation.Spells.RemoveAll(e => e.Source == Templates.ProfessionSource && !matched.Contains(e));

            // Alte Form: eigene Sonderfertigkeit „Wolfstatze (Schnellverwandlung)“, „Mindergeist (Humuspfeil)“ oder
            // „Katzenaugen - Sehen in der Dunkelheit“
            foreach (var text in _enhancementNames)
            {
                if (OwnEnhancement(text) is var (spell, enhancement))
                {
                    if (!spell.Enhancements.Contains(enhancement)) spell.Enhancements.Add(enhancement);
                }
                else _skipped.Add(text);
            }
        }

        private (SpellEntry Spell, string Enhancement)? OwnEnhancement(string text)
        {
            string[] parts = text.Contains(" - ", StringComparison.Ordinal)
                ? text.Split(" - ", 2)
                : text.LastIndexOf(" (", StringComparison.Ordinal) is > 0 and var cut
                    ? [text[..cut], text[(cut + 2)..].TrimEnd(')')]
                    : [];
            if (parts.Length != 2) return null;
            foreach (var (spellName, enhancementName) in new[] { (parts[0], parts[1]), (parts[1], parts[0]) })
            {
                var spell = Creation.Spells.FirstOrDefault(s => Compact(rules.FindSpell(s.Id)?.Name) == Compact(spellName));
                // auch mit abweichendem Ende („Sehen in der Dunkelhei“, „Zielkategorie Objekte“)
                var enhancements = spell is null ? [] : rules.FindSpell(spell.Id)?.Enhancements ?? [];
                var key = Compact(enhancementName);
                var enhancement = enhancements.FirstOrDefault(e => Compact(e.Name) == key) ?? enhancements.FirstOrDefault(e =>
                    Math.Min(key.Length, Compact(e.Name).Length) >= 8 &&
                    (Compact(e.Name).StartsWith(key, StringComparison.Ordinal) || key.StartsWith(Compact(e.Name), StringComparison.Ordinal)));
                if (spell is not null && enhancement is not null) return (spell, enhancement.Id);
            }
            return null;
        }

        // ---------------------------------------------------------------- Energien

        /// <summary>
        /// Zugekaufte LeP/AsP/KaP (<c>improved</c>) und zurückgekaufte Verluste (<c>bought</c>). Die Verluste selbst
        /// (<c>losses</c>, z. B. Bindung des Stabes) ergeben sich in der App aus den Sonderfertigkeiten.
        /// </summary>
        private void Energies()
        {
            foreach (var value in Instances("basevalues"))
            {
                var (bought, repurchased) = Str(value["id"]) switch
                {
                    "lebensenergie" => (Characters.Energies.LifePoints, null),
                    "astralenergie" => (Characters.Energies.Astral, Characters.Energies.AstralRepurchased),
                    "karmaenergie" => (Characters.Energies.Karma, Characters.Energies.KarmaRepurchased),
                    _ => ((string?)null, (string?)null),
                };
                if (bought is not null && Int(value["improved"]) is > 0 and var improved) Creation.BoughtEnergies[bought] = improved;
                if (repurchased is not null && Int(value["bought"]) is > 0 and var back) Creation.BoughtEnergies[repurchased] = back;
                if (value["losses"] is JsonArray losses)
                    foreach (var source in losses.Select(l => Str(l?["source"])).OfType<string>())
                        if (map.Activatables.GetValueOrDefault(source) is not { } a || rules.FindSpecialAbility(a.Id) is not { PermanentAstralCost: > 0 })
                            _notes.Add($"Permanenter Verlust durch „{map.Unmapped.GetValueOrDefault(source) ?? source}“ ist nicht " +
                                       "übernommen; bitte unter Energien als sonstigen Verlust eintragen.");
            }
        }

        // ---------------------------------------------------------------- Talente und Kampftechniken

        /// <summary>
        /// Talente und Kampftechniken speichert die App als Abweichung vom Wert der Vorlagen; Dark Aid speichert den Wert.
        /// </summary>
        private void Adjustments()
        {
            var state = new CharacterCalculator(rules).Calculate(_character);
            var skills = new Dictionary<string, int>();
            foreach (var s in Instances("skills"))
            {
                if (map.Skills.TryGetValue(Str(s["id"]) ?? "", out var id)) skills[id] = Int(s["level"]) ?? 0;
                else Skip(Str(s["id"]) ?? "", "Talent");
            }
            foreach (var skill in state.Skills)
            {
                var delta = skills.GetValueOrDefault(skill.Definition.Id) - skill.PackageValue;
                if (delta != 0) Creation.SkillAdjustments[skill.Definition.Id] = delta;
            }

            var start = rules.Creation.CombatTechniqueStartValue;
            var combat = new Dictionary<string, int>();
            foreach (var ct in Instances("combattechniques"))
            {
                if (map.CombatTechniques.TryGetValue(Str(ct["id"]) ?? "", out var id)) combat[id] = Int(ct["level"]) ?? start;
                else Skip(Str(ct["id"]) ?? "", "Kampftechnik");
            }
            foreach (var ct in state.CombatTechniques)
            {
                var delta = combat.GetValueOrDefault(ct.Definition.Id, start) - ct.PackageValue;
                if (delta != 0) Creation.CombatTechniqueAdjustments[ct.Definition.Id] = delta;
            }
        }

        // ---------------------------------------------------------------- Persönliches

        private void Personal()
        {
            var p = _character.Personal;
            p.Gender = Str(hero["sex"]) switch { "male" => "männlich", "female" => "weiblich", _ => null };
            if (Long(hero["height"]) is > 0 and var height)
                p.Height = PersonalRolls.FormatHeight((int)Math.Round(height / (double)HeightUnitsPerHalfFinger));
            if (Long(hero["weigth"] ?? hero["weight"]) is > 0 and var weight)
                p.Weight = PersonalRolls.FormatWeight((int)Math.Round(weight / (double)WeightUnitsPerStone));
            p.HairColor = Text(hero["haircolor"]);
            p.EyeColor = Text(hero["eyecolor"]);
            p.Birthplace = Text(hero["placeofbirth"]);
            p.Notes = Text(hero["background"]);
        }

        // ---------------------------------------------------------------- Ausrüstung und Geld

        private void Equipment()
        {
            var custom = 0;
            foreach (var (list, type) in new[]
                     {
                         ("meleeweapons", "meleeweapon"), ("rangedweapons", "rangedweapon"), ("shields", "shield"),
                         ("armor", "armor"), ("otherobjects", "equipment"),
                     })
                foreach (var item in Instances(list))
                {
                    var key = Str(item["ruleelement"]?["id"]);
                    var amount = Math.Max(1, Int(item["amount"]) ?? 1);
                    var id = key is null ? null : map.Items.GetValueOrDefault($"{Str(item["ruleelement"]?["type"]) ?? type}/{key}");
                    var def = rules.FindAnyItem(id);
                    var entry = new EquipmentEntry
                    {
                        Id = def?.Id ?? "", Quantity = amount, Paid = false, Equipped = item["equipped"]?.GetValue<bool>() == true,
                    };
                    if (def is null)
                    {
                        custom++;
                        entry.Name = Str(item["name"]) ?? map.Unmapped.GetValueOrDefault(key ?? "") ?? key ?? "Gegenstand";
                    }
                    else Need(def.Publications);
                    if (EquipmentEntry.IsSingle(entry.Id))
                    {
                        entry.Quantity = 1;
                        for (var n = 0; n < amount; n++) _character.Equipment.Add(n == 0 ? entry : Clone(entry));
                    }
                    else _character.Equipment.Add(entry);
                }
            if (custom > 0)
                _notes.Add($"{custom} Gegenstände ohne Gegenstück in den Regeldaten sind als eigene Gegenstände übernommen.");

            static EquipmentEntry Clone(EquipmentEntry e) => new()
            {
                Id = e.Id, Name = e.Name, Quantity = 1, Paid = e.Paid, Carried = e.Carried, Equipped = false,
            };
        }

        /// <summary>Gegenstände aus Dark Aid sind nicht bezahlt; eine Buchung gleicht das Geld an den Geldbeutel an.</summary>
        private void Money()
        {
            if (Long(hero["purse"]) is not { } kreuzer) return;
            var state = new CharacterCalculator(rules).Calculate(_character);
            var difference = kreuzer / 100m - state.Equipment.Money;
            if (difference != 0) _character.Money.Add(new MoneyEntry { Amount = difference, Text = "Geldbeutel laut Dark Aid" });
        }

        // ---------------------------------------------------------------- Quellen und Phase

        /// <summary>Bücher aus den Dark-Aid-Regeln und aller übernommenen Inhalte aktivieren.</summary>
        private void Books()
        {
            if (hero["rules"]?["rulebooks"] is JsonArray books)
                foreach (var book in books.Select(Str).OfType<string>())
                    if (map.Rulebooks.GetValueOrDefault(book) is { } id && rules.Books.Contains(id) &&
                        rules.Books.Find(id)?.Adult != true)
                        _books.Add(id);
            new SourceSelection(_books).ApplyTo(_character, rules.Books);
        }

        private void Finish()
        {
            var bonuses = Instances("xp", "bonuses").Select(b => (Ap: Int(b["ap"]) ?? 0, Comment: Text(b["comment"])))
                .Where(b => b.Ap != 0).ToList();
            if (hero["iscreated"]?.GetValue<bool>() != true)
            {
                if (bonuses.Count > 0)
                    _notes.Add($"AP-Boni aus Dark Aid ({bonuses.Sum(b => b.Ap)} AP) gelten erst nach der Erschaffung und sind " +
                               "nicht übernommen.");
                return;
            }
            // Erschaffen: Der Endstand gilt als Erschaffung, die AP-Boni als Gutschriften.
            var state = new CharacterCalculator(rules).Calculate(_character);
            var summary = Phases.Summarize(state, rules);
            _character.Log.Add(new CreationFinishedEntry { AcceptedErrors = summary.Errors });
            foreach (var (ap, comment) in bonuses)
                _character.Log.Add(new ApAwardEntry { Amount = ap, Note = comment ?? "Dark-Aid-Import" });
            _character.Phase = CharacterPhase.Advancement;
        }

        // ---------------------------------------------------------------- Hilfen

        private IEnumerable<JsonObject> Instances(string name, string? sub = null) =>
            (sub is null ? hero[name] : hero[name]?[sub]) is JsonArray list ? list.OfType<JsonObject>() : [];

        private string? Map(IReadOnlyDictionary<string, string> dictionary, string? key, string what)
        {
            if (key is null) return null;
            if (dictionary.TryGetValue(key, out var id)) return id;
            Skip(key, what);
            return null;
        }

        private void Skip(string key, string what) =>
            _skipped.Add((map.Unmapped.GetValueOrDefault(key) ?? map.Unmapped.GetValueOrDefault(key.Split(':')[0])) is { } name
                ? name
                : $"{what} {key}");

        private void Need(IReadOnlyList<Publication> publications)
        {
            if (publications.Count > 0 && !publications.Any(p => _books.Contains(p.Book)) && rules.Books.Contains(publications[0].Book))
                _books.Add(publications[0].Book);
        }

        private static string Compact(string? name) =>
            new((name ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

        private static string? Join(string? a, string b) => a is null ? b : $"{a}: {b}";

        private static string? Str(JsonNode? node) =>
            node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

        private static string? Text(JsonNode? node) => Str(node)?.Trim() is { Length: > 0 } t ? t : null;

        private static int? Int(JsonNode? node) => Long(node) is { } l ? (int)l : null;

        private static long? Long(JsonNode? node) => node is JsonValue v
            ? v.TryGetValue<long>(out var i) ? i
            : v.TryGetValue<double>(out var d) ? (long)Math.Round(d)
            : v.TryGetValue<string>(out var s) && long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) ? p
            : null
            : null;
    }
}
