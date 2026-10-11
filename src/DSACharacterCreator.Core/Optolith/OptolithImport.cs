using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.Core.Optolith;

/// <summary>Importierter Held und Hinweise, was nicht oder nur ungefähr übernommen werden konnte.</summary>
public sealed record OptolithImportResult(Character Character, IReadOnlyList<string> Notes);

/// <summary>
/// Liest einen Optolith-Helden (Optolith 1.x, <c>*.json</c>). Optolith speichert nur den Endstand; Kultur und Profession
/// werden als Vorlage gesetzt und alle Werte so angepasst, dass sie dem Optolith-Stand entsprechen. Abgeschlossene
/// Helden (Phase 3) kommen als abgeschlossene Erschaffung mit einer AP-Gutschrift über die restlichen AP ins Protokoll.
/// </summary>
public static class OptolithImport
{
    /// <summary>Sieht der Text wie ein Optolith-Held aus (statt einer Datei dieser App)?</summary>
    public static bool IsOptolithHero(string json)
    {
        try
        {
            return JsonNode.Parse(json) is JsonObject o && o.ContainsKey("clientVersion") && o.ContainsKey("activatable");
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <exception cref="InvalidDataException">Die Datei ist kein lesbarer Optolith-Held.</exception>
    public static OptolithImportResult Read(string json, RuleData rules, OptolithMapping map, SourceSelection sources)
    {
        JsonObject hero;
        try
        {
            hero = JsonNode.Parse(json) as JsonObject ?? throw new InvalidDataException("Die Datei ist kein Optolith-Held.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Die Datei ist kein Optolith-Held.", ex);
        }
        if (!hero.ContainsKey("activatable") || !hero.ContainsKey("attr"))
            throw new InvalidDataException("Die Datei ist kein Optolith-Held.");
        try
        {
            return new Importer(hero, rules, map, sources).Run();
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException or KeyNotFoundException)
        {
            throw new InvalidDataException("Der Optolith-Held konnte nicht gelesen werden: " + ex.Message, ex);
        }
    }

    private sealed class Importer(JsonObject hero, RuleData rules, OptolithMapping map, SourceSelection sources)
    {
        private readonly List<string> _notes = [];
        private readonly List<string> _skipped = [];
        private readonly HashSet<string> _books = new(sources.EnabledBookIds, StringComparer.Ordinal);
        private readonly Character _character = new();

        private CreationLayer Creation => _character.Creation;

        public OptolithImportResult Run()
        {
            var c = _character;
            c.Name = Str(hero["name"])?.Trim() ?? "";
            if (DateTimeOffset.TryParse(Str(hero["dateCreated"]), CultureInfo.InvariantCulture, DateTimeStyles.None, out var created))
                c.CreatedAt = created;

            Creation.ExperienceLevel = Map(map.ExperienceLevels, Str(hero["el"]), "Erfahrungsgrad");
            var level = rules.ExperienceLevel(Creation.ExperienceLevel);
            Origin();
            Attributes();
            Activatables();
            Spells();
            Energies();
            Personal();
            Equipment();
            Books();
            Adjustments();
            Money();
            Finish(level);

            if (hero["pets"] is JsonObject { Count: > 0 } pets)
                _notes.Add($"Tiere aus Optolith ({pets.Count}) wurden nicht übernommen; bitte unter „Tiergefährten“ neu anlegen.");
            if (hero["pact"] is JsonObject)
                _notes.Add("Der Pakt aus Optolith wurde nicht übernommen.");
            if (_skipped.Count > 0)
                _notes.Insert(0, "Nicht übernommen (in der App nicht vorhanden): " + string.Join(", ", _skipped.Distinct()) + ".");
            return new OptolithImportResult(c, _notes);
        }

        // ---------------------------------------------------------------- Herkunft

        private void Origin()
        {
            Creation.Species = Map(map.Species, Str(hero["r"]), "Spezies");
            if (rules.FindSpecies(Creation.Species) is { } species)
            {
                Need(species.Publications);
                var chosen = map.Attributes.GetValueOrDefault(Str(hero["attr"]?["attributeAdjustmentSelected"]) ?? "");
                foreach (var change in species.AttributeChanges.Where(a => a.IsChoice))
                    Creation.SpeciesAttributeChoices.Add(change.Value > 0 && chosen is not null && change.Options.Contains(chosen)
                        ? chosen
                        : null);
            }

            var culture = Map(map.Cultures, Str(hero["c"]), "Kultur");
            Templates.ChangeCulture(Creation, culture, rules);
            if (rules.FindCulture(culture) is { } cultureDef) Need(cultureDef.Publications);

            var optolithProfession = Str(hero["p"]);
            OptolithProfession? profession = null;
            if (optolithProfession is not null && optolithProfession != "P_0")
            {
                profession = map.Professions.GetValueOrDefault(optolithProfession);
                if (profession is null) Skip(optolithProfession, "Profession");
            }
            else if (Str(hero["professionName"]) is { Length: > 0 } custom)
            {
                _notes.Add($"Eigene Profession „{custom}“ aus Optolith: Die Werte sind übernommen, eine Vorlage ist nicht gesetzt.");
            }
            Templates.ChangeProfession(Creation, profession?.Id, rules);
            if (rules.FindProfession(profession?.Id) is { } professionDef) Need(professionDef.Publications);

            var variant = profession?.Variant;
            if (Str(hero["pv"]) is { } pv)
            {
                if (map.ProfessionVariants.TryGetValue(pv, out var v) && v.Profession == profession?.Id) variant = v.Variant;
                else Skip(pv, "Professionsvariante");
            }
            Creation.SetProfessionVariant(variant);
        }

        // ---------------------------------------------------------------- Eigenschaften

        private void Attributes()
        {
            if (hero["attr"]?["values"] is not JsonArray values) return;
            foreach (var value in values)
            {
                // Optolith 1.0: ["ATTR_1", 14, 0]; danach { "id": "ATTR_1", "value": 14 }
                var (id, v) = value is JsonArray a ? (Str(a[0]), Int(a[1])) : (Str(value?["id"]), Int(value?["value"]));
                if (id is not null && map.Attributes.TryGetValue(id, out var attribute) && v is { } n)
                    Creation.Attributes[attribute] = n;
            }
        }

        // ---------------------------------------------------------------- Vor-/Nachteile und Sonderfertigkeiten

        private void Activatables()
        {
            if (hero["activatable"] is not JsonObject activatable) return;
            var traitRules = new TraitRules(rules);
            var species = rules.FindSpecies(Creation.Species);
            // Automatische Vor-/Nachteile der Spezies setzt die App selbst.
            var automatic = (species?.AutomaticAdvantages.Select(n => traitRules.Resolve(n, false)) ?? [])
                .Concat(species?.AutomaticDisadvantages.Select(n => traitRules.Resolve(n, true)) ?? [])
                .OfType<(TraitDefinition Definition, TraitEntry Entry)>()
                .Select(r => r.Definition.Id).ToHashSet();

            var traits = new List<TraitEntry>();
            var abilities = new List<(TraitEntry Entry, bool Native)>();
            foreach (var (key, node) in activatable.OrderBy(p => OptolithMapping.OrderKey(p.Key)))
            {
                if (node is not JsonArray { Count: > 0 } instances) continue;
                if (!map.Activatables.TryGetValue(key, out var mapped) ||
                    (rules.FindTrait(mapped.Id) ?? rules.FindSpecialAbility(mapped.Id)) is not { } def)
                {
                    Skip(key, "Eintrag", instances.Count > 1 ? instances.Count : null);
                    continue;
                }
                Need(def.Publications);
                foreach (var instance in instances.OfType<JsonObject>())
                {
                    if (!def.IsSpecialAbility && automatic.Contains(def.Id)) continue;
                    var entry = Entry(key, mapped, def, instance, out var native);
                    if (entry is null) continue;
                    if (def.IsSpecialAbility) abilities.Add((entry, native));
                    else traits.Add(entry);
                }
            }

            Merge(Creation.Traits, traits.Select(t => (t, false)).ToList());
            Merge(Creation.SpecialAbilities, abilities);
            if (Str(hero["pers"]?["cultureAreaKnowledge"]) is { Length: > 0 } area &&
                Creation.SpecialAbilities.FirstOrDefault(e => e.Source == Templates.CultureSource && e.Id == "sf.ortskenntnis") is { } local)
                local.Specification = area;
        }

        /// <summary>
        /// Gleicht die Einträge der Vorlagen (Kultur, Profession) mit den importierten ab: Passende Einträge behalten ihre
        /// Herkunft, fehlende der Profession entfallen, die übrigen kommen als eigene Einträge hinzu. Die Einträge der Kultur
        /// (Muttersprache, Ortskenntnis) bleiben; die Muttersprache übernimmt die Sprache aus Optolith.
        /// </summary>
        private static void Merge(List<TraitEntry> list, List<(TraitEntry Entry, bool Native)> imported)
        {
            var matched = new HashSet<TraitEntry>();
            foreach (var (entry, native) in imported)
            {
                var existing = native
                    ? list.FirstOrDefault(e => !matched.Contains(e) && e.Source == Templates.CultureSource && e.Id == entry.Id &&
                                               (e.Option is null || e.Option == entry.Option))
                    : list.FirstOrDefault(e => !matched.Contains(e) && e.Id == entry.Id && e.Option == entry.Option);
                if (existing is null)
                {
                    if (native)
                    {
                        entry.Source = Templates.CultureSource;
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
            list.RemoveAll(e => e.Source == Templates.ProfessionSource && !matched.Contains(e));
        }

        /// <param name="native">Muttersprache (Optolith: Sprache auf Stufe 4).</param>
        private TraitEntry? Entry(string key, OptolithActivatable mapped, TraitDefinition def, JsonObject instance, out bool native)
        {
            native = false;
            var entry = new TraitEntry { Id = def.Id };
            var level = Int(instance["tier"]);
            if (def.OptionTarget == TraitOptionTarget.Language && level == 4)
            {
                native = true;
                level = 3;
            }
            if (def.MaxLevel is not null) entry.Level = level ?? 1;

            int? optionAp = null;
            switch (instance["sid"])
            {
                case JsonValue v when v.TryGetValue<int>(out var number):
                    if (mapped.Options.TryGetValue(number.ToString(CultureInfo.InvariantCulture), out var option))
                    {
                        entry.Option = option.Option;
                        entry.Specification = option.Text;
                        optionAp = option.Ap;
                    }
                    else
                    {
                        Skip($"{key}:{number}", "Auswahl");
                        return null;
                    }
                    break;
                case JsonValue v when v.TryGetValue<string>(out var text):
                    if (IsReference(text, out var id))
                    {
                        if (id is null)
                        {
                            Skip(text, "Auswahl");
                            return null;
                        }
                        Reference(def, entry, id);
                    }
                    else entry.Specification = text; // freie Eingabe, z. B. „Angst vor …“
                    break;
            }
            // Zweite Auswahl: Anwendungsgebiet (Fertigkeitsspezialisierung) oder freier Text
            switch (instance["sid2"])
            {
                case JsonValue v when v.TryGetValue<int>(out var number):
                    var skill = Str(instance["sid"]) ?? "";
                    var application = map.SkillApplications.GetValueOrDefault(skill)?.GetValueOrDefault(
                        number.ToString(CultureInfo.InvariantCulture));
                    entry.Specification = Join(entry.Specification, application ?? number.ToString(CultureInfo.InvariantCulture));
                    break;
                case JsonValue v when v.TryGetValue<string>(out var text):
                    entry.Specification = Join(entry.Specification, text);
                    break;
            }
            if (def.Cost.Type == TraitCostType.Manual || Int(instance["cost"]) is not null)
            {
                var ap = Int(instance["cost"]) ?? optionAp ?? mapped.Ap;
                entry.ManualAp = ap is { } a && def.IsDisadvantage ? -Math.Abs(a) : ap;
            }
            return entry;
        }

        /// <summary>
        /// Gewählte Fertigkeit, Kampftechnik oder gewählter Zauber: als Auswahl, wenn der Eintrag eine verlangt; bei
        /// Ausprägungen nach Steigerungsfaktor („B-Zauber“ beim Lieblingszauber) die passende Ausprägung; sonst (Tradition
        /// mit Zauber, Meistertrick …) der Name als Präzisierung.
        /// </summary>
        private void Reference(TraitDefinition def, TraitEntry entry, string id)
        {
            if (def.OptionTarget is TraitOptionTarget.Skill or TraitOptionTarget.CombatTechnique)
            {
                entry.Option = id;
                return;
            }
            var spell = rules.FindSpell(id);
            entry.Specification = rules.FindSkill(id)?.Name ?? spell?.Name ?? rules.FindCombatTechnique(id)?.Name;
            if (def.Cost.Type == TraitCostType.ByOption && spell?.ImprovementCost is { } factor)
                entry.Option = def.Cost.Options!.FirstOrDefault(o => o.Name.StartsWith(factor + "-", StringComparison.Ordinal))?.Name;
        }

        /// <summary>Ist der Text ein Verweis wie „TAL_9“ oder „SPELL_40“? <paramref name="id"/> ist <c>null</c>, wenn
        /// es dazu nichts in der App gibt.</summary>
        private bool IsReference(string text, out string? id)
        {
            id = text.Split('_')[0] switch
            {
                "TAL" => map.Skills.GetValueOrDefault(text),
                "CT" => map.CombatTechniques.GetValueOrDefault(text),
                "SPELL" or "LITURGY" or "CANTRIP" or "BLESSING" => map.Spells.GetValueOrDefault(text),
                _ => "",
            };
            if (id != "") return true;
            id = null;
            return false;
        }

        // ---------------------------------------------------------------- Zauber und Liturgien

        private void Spells()
        {
            var imported = new List<(string Id, int? Value)>();
            foreach (var group in new[] { "spells", "liturgies" })
                if (hero[group] is JsonObject values)
                    foreach (var (key, value) in values)
                        AddSpell(key, Int(value) ?? 0);
            foreach (var group in new[] { "cantrips", "blessings" })
                if (hero[group] is JsonArray ids)
                    foreach (var id in ids)
                        if (Str(id) is { } key) AddSpell(key, null);

            var matched = new HashSet<SpellEntry>();
            foreach (var (id, value) in imported)
            {
                var existing = Creation.Spells.FirstOrDefault(e => e.Id == id && !matched.Contains(e));
                if (existing is null) Creation.Spells.Add(existing = new SpellEntry { Id = id });
                existing.Value = rules.FindSpell(id)?.ImprovementCost is null ? null : value;
                matched.Add(existing);
            }
            // Magische Handlungen (Flüche, Elfenlieder …) kennt eine Optolith-Heldendatei nicht; die der Profession bleiben.
            Creation.Spells.RemoveAll(e => e.Source == Templates.ProfessionSource && !matched.Contains(e) &&
                                           rules.FindSpell(e.Id)?.Group != SpellGroup.MagicalAction);

            void AddSpell(string key, int? value)
            {
                if (map.Spells.TryGetValue(key, out var id) && rules.FindSpell(id) is { } def)
                {
                    Need(def.Publications);
                    imported.Add((id, value));
                }
                else Skip(key, "Zauber");
            }
        }

        // ---------------------------------------------------------------- Energien

        private void Energies()
        {
            var attr = hero["attr"];
            Set(Characters.Energies.LifePoints, Int(attr?["lp"]));
            Set(Characters.Energies.Astral, Int(attr?["ae"]));
            Set(Characters.Energies.Karma, Int(attr?["kp"]));

            // Optolith kennt keine permanenten AsP durch Bindungen (Traditionsartefakte); in der App ergeben sie sich aus den
            // Sonderfertigkeiten. Die AsP bleiben gleich: Verluste in Optolith zählen zuerst als Bindung, ein Rest als
            // sonstiger Verlust; nicht erfasste Bindungen gelten als zurückgekauft.
            var bound = Creation.SpecialAbilities.Sum(e => rules.FindSpecialAbility(e.Id) is { } d ? TraitRules.PermanentAstral(d, e) : 0);
            var lostAe = Int(attr?["permanentAE"]?["lost"]) ?? 0;
            var redeemedAe = Int(attr?["permanentAE"]?["redeemed"]) ?? 0;
            Set(Characters.Energies.AstralLost, Math.Max(0, lostAe - bound));
            Set(Characters.Energies.AstralRepurchased, redeemedAe + Math.Max(0, bound - lostAe));
            Set(Characters.Energies.KarmaLost, Int(attr?["permanentKP"]?["lost"]));
            Set(Characters.Energies.KarmaRepurchased, Int(attr?["permanentKP"]?["redeemed"]));
            if (Int(attr?["permanentLP"]?["lost"]) is > 0 and var lp)
                _notes.Add($"Permanent verlorene LeP ({lp}) kennt die App nicht; bitte in den Notizen festhalten.");

            void Set(string id, int? value)
            {
                if (value is > 0 and var v) Creation.BoughtEnergies[id] = v;
                else Creation.BoughtEnergies.Remove(id);
            }
        }

        // ---------------------------------------------------------------- Talente und Kampftechniken

        /// <summary>
        /// Talente und Kampftechniken speichert die App als Abweichung vom Wert der Vorlagen; Optolith speichert den Wert.
        /// </summary>
        private void Adjustments()
        {
            var state = new CharacterCalculator(rules).Calculate(_character);
            var skills = new Dictionary<string, int>();
            if (hero["talents"] is JsonObject talents)
                foreach (var (key, value) in talents)
                {
                    if (map.Skills.TryGetValue(key, out var id)) skills[id] = Int(value) ?? 0;
                    else Skip(key, "Talent");
                }
            foreach (var skill in state.Skills)
            {
                var delta = skills.GetValueOrDefault(skill.Definition.Id) - skill.PackageValue;
                if (delta != 0) Creation.SkillAdjustments[skill.Definition.Id] = delta;
            }

            var start = rules.Creation.CombatTechniqueStartValue;
            var combat = new Dictionary<string, int>();
            if (hero["ct"] is JsonObject cts)
                foreach (var (key, value) in cts)
                {
                    if (map.CombatTechniques.TryGetValue(key, out var id)) combat[id] = Int(value) ?? start;
                    else Skip(key, "Kampftechnik");
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
            var pers = hero["pers"];
            var p = _character.Personal;
            p.Family = Text(pers?["family"]);
            p.Birthplace = Text(pers?["placeofbirth"]);
            p.Birthdate = Text(pers?["dateofbirth"]);
            p.Age = Text(pers?["age"]);
            p.Gender = Str(hero["sex"]) switch { "m" => "männlich", "f" => "weiblich", _ => null };
            p.Height = Text(pers?["size"]) is { } size
                ? PersonalRolls.ParseHeight(size) is { } h ? PersonalRolls.FormatHeight(h) : size
                : null;
            p.Weight = Text(pers?["weight"]) is { } weight
                ? decimal.TryParse(weight.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var w)
                    ? PersonalRolls.FormatWeight((int)Math.Round(w))
                    : weight
                : null;
            p.HairColor = Color(map.HairColors, pers?["haircolor"]);
            p.EyeColor = Color(map.EyeColors, pers?["eyecolor"]);
            p.Title = Text(pers?["title"]);
            p.Characteristics = Text(pers?["characteristics"]);

            var notes = new List<string>();
            if (Text(pers?["otherinfo"]) is { } other) notes.Add(other);
            if (Int(pers?["socialstatus"]) is { } status && status != 2 &&
                map.SocialStatus.GetValueOrDefault(status.ToString(CultureInfo.InvariantCulture)) is { } name)
                notes.Add($"Sozialstatus (Optolith): {name}");
            p.Notes = notes.Count > 0 ? string.Join("\n\n", notes) : null;

            if (Str(hero["rv"]) is { } rv && map.SpeciesVariants.TryGetValue(rv, out var table) &&
                rules.FindAppearance(Creation.Species)?.Choices.Any(c => c.Name == table) == true)
                p.AppearanceTable = table;

            static string? Color(IReadOnlyDictionary<string, string> names, JsonNode? node) =>
                Int(node) is { } id ? names.GetValueOrDefault(id.ToString(CultureInfo.InvariantCulture)) : null;
        }

        // ---------------------------------------------------------------- Ausrüstung und Geld

        private void Equipment()
        {
            if (hero["belongings"]?["items"] is not JsonObject items) return;
            var custom = 0;
            foreach (var (_, node) in items)
            {
                if (node is not JsonObject item) continue;
                var name = Text(item["name"]) ?? "Gegenstand";
                var amount = Math.Max(1, Int(item["amount"]) ?? 1);
                var locked = item["isTemplateLocked"]?.GetValue<bool>() == true;
                var template = Str(item["template"]) is { } t ? map.Items.GetValueOrDefault(t) : null;
                var def = template is null ? null : rules.FindAnyItem(template.Id);
                var entry = new EquipmentEntry { Id = def?.Id ?? "", Quantity = amount, Paid = false };
                if (def is null)
                {
                    custom++;
                    entry.Name = name;
                }
                else
                {
                    Need(def.Publications);
                    if (name != def.Name) entry.Name = name;
                }
                if (def is null || !locked)
                {
                    entry.Weight = Decimal(item["weight"]);
                    entry.Price = Decimal(item["price"]);
                }
                if (EquipmentEntry.IsSingle(entry.Id))
                {
                    entry.Quantity = 1;
                    for (var n = 0; n < amount; n++) _character.Equipment.Add(n == 0 ? entry : Clone(entry));
                }
                else _character.Equipment.Add(entry);
            }
            // Angelegt wird die Rüstung mit dem höchsten Rüstungsschutz; Waffen gelten als nicht geführt.
            if (_character.Equipment.Where(e => e.Id.StartsWith("armor.", StringComparison.Ordinal))
                    .MaxBy(e => rules.FindArmor(e.Id)?.Protection ?? 0) is { } armor)
                armor.Equipped = true;
            if (custom > 0)
                _notes.Add($"{custom} Gegenstände ohne Gegenstück in den Regeldaten sind als eigene Gegenstände übernommen.");

            static EquipmentEntry Clone(EquipmentEntry e) => new()
            {
                Id = e.Id, Name = e.Name, Quantity = 1, Weight = e.Weight, Price = e.Price, Paid = e.Paid, Carried = e.Carried,
            };
        }

        /// <summary>Gegenstände aus Optolith sind nicht bezahlt; eine Buchung gleicht das Geld an den Geldbeutel an.</summary>
        private void Money()
        {
            if (hero["belongings"]?["purse"] is not JsonObject purse) return;
            var silver = 10m * (Decimal(purse["d"]) ?? 0) + (Decimal(purse["s"]) ?? 0) + (Decimal(purse["h"]) ?? 0) / 10m +
                         (Decimal(purse["k"]) ?? 0) / 100m;
            var state = new CharacterCalculator(rules).Calculate(_character);
            var difference = silver - state.Equipment.Money;
            if (difference != 0) _character.Money.Add(new MoneyEntry { Amount = difference, Text = "Geldbeutel laut Optolith" });
        }

        // ---------------------------------------------------------------- Quellen und Phase

        /// <summary>Bücher aller übernommenen Inhalte aktivieren (z. B. Ü18-Werke, wenn der Held Inhalte daraus nutzt).</summary>
        private void Books()
        {
            var selection = new SourceSelection(_books);
            selection.ApplyTo(_character, rules.Books);
        }

        private void Finish(ExperienceLevel level)
        {
            var total = Int(hero["ap"]?["total"]) ?? level.Ap;
            var phase = Int(hero["phase"]) ?? 1;
            if (phase < 3)
            {
                if (total != level.Ap)
                    _notes.Add($"Optolith gibt {total} AP an, der Erfahrungsgrad {level.Name} {level.Ap} AP.");
                return;
            }
            // Abgeschlossen: Der Endstand gilt als Erschaffung, die AP über den Erfahrungsgrad hinaus als Gutschrift.
            var state = new CharacterCalculator(rules).Calculate(_character);
            var summary = Phases.Summarize(state, rules);
            _character.Log.Add(new CreationFinishedEntry { AcceptedErrors = summary.Errors });
            if (total != level.Ap)
                _character.Log.Add(new ApAwardEntry { Amount = total - level.Ap, Note = "Optolith-Import" });
            _character.Phase = CharacterPhase.Advancement;

            var spent = new CharacterCalculator(rules).Calculate(_character).Ap.Spent;
            if (Int(hero["ap"]?["spent"]) is { } optolithSpent && optolithSpent != spent)
                _notes.Add($"Optolith zählt {optolithSpent} ausgegebene AP, die App {spent}.");
        }

        // ---------------------------------------------------------------- Hilfen

        private string? Map(IReadOnlyDictionary<string, string> dictionary, string? key, string what)
        {
            if (key is null) return null;
            if (dictionary.TryGetValue(key, out var id)) return id;
            Skip(key, what);
            return null;
        }

        private void Skip(string key, string what, int? count = null)
        {
            var name = map.Unmapped.GetValueOrDefault(key.Split(':')[0]);
            _skipped.Add(name is null ? $"{what} {key}" : count is > 1 ? $"{name} ({count}×)" : name);
        }

        private void Need(IReadOnlyList<Publication> publications)
        {
            if (publications.Count > 0 && !publications.Any(p => _books.Contains(p.Book)) && rules.Books.Contains(publications[0].Book))
                _books.Add(publications[0].Book);
        }

        private static string? Join(string? a, string b) => a is null ? b : $"{a}: {b}";

        private static string? Str(JsonNode? node) =>
            node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

        private static string? Text(JsonNode? node) =>
            node is JsonValue v ? (v.TryGetValue<string>(out var s) ? s : v.ToJsonString()).Trim() is { Length: > 0 } t ? t : null : null;

        private static int? Int(JsonNode? node) => node is JsonValue v
            ? v.TryGetValue<int>(out var i) ? i
            : v.TryGetValue<double>(out var d) ? (int)Math.Round(d)
            : v.TryGetValue<string>(out var s) && int.TryParse(s, out var p) ? p : null
            : null;

        private static decimal? Decimal(JsonNode? node) => node is JsonValue v
            ? v.TryGetValue<decimal>(out var d) ? d
            : v.TryGetValue<string>(out var s) && decimal.TryParse(s.Replace(',', '.'), NumberStyles.Number,
                CultureInfo.InvariantCulture, out var p) ? p : null
            : null;
    }
}
