using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.Core.Optolith;

/// <summary>Optolith-Held als JSON-Text und Hinweise, was Optolith nicht kennt.</summary>
public sealed record OptolithExportResult(string Json, IReadOnlyList<string> Notes);

/// <summary>
/// Schreibt den aktuellen Stand eines Helden im Format von Optolith 1.5. Optolith speichert nur den Endstand, das
/// Steigerungsprotokoll geht dabei verloren; die AP-Gutschriften stecken in der AP-Summe.
/// </summary>
public static class OptolithExport
{
    public const string ClientVersion = "1.5.1";

    public static OptolithExportResult Write(Character character, RuleData rules, OptolithMapping map) =>
        new Exporter(character, rules, map).Run();

    private sealed class Exporter(Character character, RuleData rules, OptolithMapping map)
    {
        private readonly List<string> _notes = [];
        private readonly List<string> _skipped = [];
        private readonly CharacterState _state = new CharacterCalculator(rules).Calculate(character);
        private CreationLayer Layer => _state.Advancement?.Working ?? character.Creation;

        public OptolithExportResult Run()
        {
            var origin = _state.Origin;
            var hero = new JsonObject
            {
                ["clientVersion"] = ClientVersion,
                ["dateCreated"] = Date(character.CreatedAt),
                ["dateModified"] = Date(character.ModifiedAt),
                ["id"] = "H_" + character.CreatedAt.ToUnixTimeMilliseconds(),
                ["phase"] = character.Phase == CharacterPhase.Advancement ? 3
                    : origin.Species is null || origin.Culture is null ? 1 : 2,
                ["locale"] = "de-DE",
                ["name"] = character.Name is { Length: > 0 } name ? name : "Unbenannt",
                ["ap"] = new JsonObject { ["total"] = _state.Ap.Total },
                ["el"] = Reverse(map.ExperienceLevels, _state.ExperienceLevel.Id, "Erfahrungsgrad"),
            };
            Put(hero, "r", Reverse(map.Species, origin.Species?.Id, "Spezies"));
            if (character.Personal.AppearanceTable is { } table)
                Put(hero, "rv", map.SpeciesVariants.Where(p => p.Value == table).Select(p => p.Key).FirstOrDefault());
            Put(hero, "c", Reverse(map.Cultures, origin.Culture?.Id, "Kultur"));
            Profession(hero);
            hero["sex"] = PersonalRolls.GenderOf(character.Personal.Gender) == NameGender.Female ? "f" : "m";
            hero["pers"] = Personal();
            hero["attr"] = Attributes();
            hero["activatable"] = Activatables();
            Skills(hero);
            Spells(hero);
            hero["belongings"] = Belongings();
            hero["rules"] = new JsonObject
            {
                ["higherParadeValues"] = 0,
                ["attributeValueLimit"] = false,
                ["enableAllRuleBooks"] = true,
                ["enabledRuleBooks"] = new JsonArray(),
                ["enableLanguageSpecializations"] = false,
            };
            hero["pets"] = new JsonObject();

            if (character.Companions.Count > 0)
                _notes.Add("Tiergefährten kennt das Optolith-Format nicht; sie sind nicht enthalten.");
            if (character.Log.Count > 0)
                _notes.Add("Optolith speichert nur den Endstand; das Steigerungsprotokoll ist nicht enthalten.");
            if (_skipped.Count > 0)
                _notes.Insert(0, "Nicht in Optolith vorhanden: " + string.Join(", ", _skipped.Distinct()) + ".");
            return new OptolithExportResult(hero.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), _notes);
        }

        private void Profession(JsonObject hero)
        {
            var origin = _state.Origin;
            if (origin.Profession is not { } profession)
            {
                hero["p"] = "P_0";
                return;
            }
            var variant = origin.Variant?.Id;
            // Manche Optolith-Professionen sind bei uns Varianten (Seenritter, Waldritter …)
            var key = map.Professions.Where(p => p.Value.Id == profession.Id && (p.Value.Variant is null || p.Value.Variant == variant))
                .OrderBy(p => p.Value.Variant is null ? 1 : 0).ThenBy(p => OptolithMapping.OrderKey(p.Key))
                .Select(p => p.Key).FirstOrDefault();
            if (key is null)
            {
                hero["p"] = "P_0";
                hero["professionName"] = profession.Name;
                _notes.Add($"Die Profession {profession.Name} gibt es in Optolith nicht; sie ist als eigene Profession eingetragen.");
                return;
            }
            hero["p"] = key;
            if (variant is not null && map.Professions[key].Variant is null)
            {
                var pv = map.ProfessionVariants.Where(p => p.Value.Profession == profession.Id && p.Value.Variant == variant)
                    .Select(p => p.Key).FirstOrDefault();
                if (pv is null) Skip($"Variante {origin.Variant!.Name}");
                else hero["pv"] = pv;
            }
        }

        private JsonObject Personal()
        {
            var p = character.Personal;
            var pers = new JsonObject();
            Put(pers, "family", p.Family);
            Put(pers, "placeofbirth", p.Birthplace);
            Put(pers, "dateofbirth", p.Birthdate);
            Put(pers, "age", p.Age);
            Put(pers, "haircolor", Color(map.HairColors, p.HairColor));
            Put(pers, "eyecolor", Color(map.EyeColors, p.EyeColor));
            Put(pers, "size", PersonalRolls.ParseHeight(p.Height)?.ToString(CultureInfo.InvariantCulture) ?? p.Height);
            Put(pers, "weight", p.Weight?.Replace("Stein", "").Trim());
            Put(pers, "title", p.Title);
            Put(pers, "characteristics", p.Characteristics);

            // Sozialstatus steht nach einem Import in den Notizen („Sozialstatus (Optolith): Niederadel“)
            var status = 2;
            var notes = new List<string>();
            foreach (var part in (p.Notes ?? "").Split("\n\n"))
            {
                const string prefix = "Sozialstatus (Optolith): ";
                if (part.StartsWith(prefix, StringComparison.Ordinal) &&
                    map.SocialStatus.FirstOrDefault(s => s.Value == part[prefix.Length..].Trim()).Key is { } id)
                    status = int.Parse(id, CultureInfo.InvariantCulture);
                else if (part.Trim().Length > 0) notes.Add(part);
            }
            pers["socialstatus"] = status;
            Put(pers, "otherinfo", notes.Count > 0 ? string.Join("\n\n", notes) : null);
            var area = Layer.SpecialAbilities.FirstOrDefault(e => e.Source == Templates.CultureSource && e.Id == "sf.ortskenntnis")
                ?.Specification;
            Put(pers, "cultureAreaKnowledge", area);
            return pers;

            static int? Color(IReadOnlyDictionary<string, string> names, string? text) =>
                text is null ? null : names.Where(n => string.Equals(n.Value, text.Trim(), StringComparison.OrdinalIgnoreCase))
                    .Select(n => (int?)int.Parse(n.Key, CultureInfo.InvariantCulture)).FirstOrDefault();
        }

        private JsonObject Attributes()
        {
            var values = new JsonArray();
            foreach (var (key, id) in map.Attributes.OrderBy(p => OptolithMapping.OrderKey(p.Key)))
                values.Add(new JsonObject { ["id"] = key, ["value"] = _state.Attribute(id) });
            var attr = new JsonObject { ["values"] = values };
            // Gewählte Eigenschaft der Spezies (+1); ohne Auswahl die erste (Optolith verlangt eine)
            var species = _state.Origin.Species;
            var choice = species?.AttributeChanges.Where(a => a.IsChoice).Select((a, i) => (a, i))
                .FirstOrDefault(x => x.a.Value > 0);
            if (choice is { a: { } change, i: var index })
            {
                var chosen = character.Creation.SpeciesAttributeChoices.ElementAtOrDefault(index) ?? change.Options[0];
                attr["attributeAdjustmentSelected"] = map.Attributes.First(p => p.Value == chosen).Key;
            }
            else attr["attributeAdjustmentSelected"] = "ATTR_1";

            var energies = Layer.BoughtEnergies;
            attr["lp"] = energies.GetValueOrDefault(Energies.LifePoints);
            attr["ae"] = energies.GetValueOrDefault(Energies.Astral);
            attr["kp"] = energies.GetValueOrDefault(Energies.Karma);
            // Optolith kennt keine permanenten AsP durch Bindungen: sie zählen als Verlust (wie beim Import)
            var bound = _state.SpecialAbilities.Sum(t => TraitRules.PermanentAstral(t.Definition, t.Entry));
            attr["permanentAE"] = new JsonObject
            {
                ["lost"] = energies.GetValueOrDefault(Energies.AstralLost) + bound,
                ["redeemed"] = energies.GetValueOrDefault(Energies.AstralRepurchased),
            };
            attr["permanentKP"] = new JsonObject
            {
                ["lost"] = energies.GetValueOrDefault(Energies.KarmaLost),
                ["redeemed"] = energies.GetValueOrDefault(Energies.KarmaRepurchased),
            };
            attr["permanentLP"] = new JsonObject { ["lost"] = 0 };
            return attr;
        }

        // ---------------------------------------------------------------- Vor-/Nachteile und Sonderfertigkeiten

        private JsonObject Activatables()
        {
            var keys = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (key, a) in map.Activatables.OrderBy(p => OptolithMapping.OrderKey(p.Key))) keys.TryAdd(a.Id, key);

            var result = new JsonObject();
            // Automatische Vor-/Nachteile der Spezies speichert Optolith beim Helden mit.
            foreach (var t in _state.Traits.Concat(_state.SpecialAbilities))
            {
                if (!keys.TryGetValue(t.Definition.Id, out var key))
                {
                    Skip(Label(t));
                    continue;
                }
                var instance = Instance(map.Activatables[key], t);
                if (instance is null)
                {
                    Skip(Label(t));
                    continue;
                }
                if (result[key] is not JsonArray list) result[key] = list = new JsonArray();
                list.Add(instance);
            }
            return result;
        }

        private JsonObject? Instance(OptolithActivatable mapped, TraitState t)
        {
            var def = t.Definition;
            var entry = t.Entry;
            var o = new JsonObject();
            var specification = entry.Specification;

            if (entry.Option is { } option)
            {
                var sid = Sid(mapped, option);
                if (sid is not null) o["sid"] = sid;
                else if (def.Cost.Type == TraitCostType.ByOption && specification is not null && SpellKey(specification) is { } spell)
                {
                    o["sid"] = spell; // Lieblingszauber „B-Zauber“ + Präzisierung „Ignifaxius“
                    specification = null;
                }
                else return null;
            }
            if (specification is not null)
            {
                var skill = Str(o["sid"]);
                var application = skill is null ? null : map.SkillApplications.GetValueOrDefault(skill)
                    ?.FirstOrDefault(p => p.Value == specification).Key;
                if (application is not null) o["sid2"] = int.Parse(application, CultureInfo.InvariantCulture);
                else if (o["sid"] is null && mapped.Options.FirstOrDefault(p => p.Value.Text == specification).Key is { } text)
                    o["sid"] = int.Parse(text, CultureInfo.InvariantCulture);
                else if (o["sid"] is null && SpellKey(specification) is { } spell)
                    o["sid"] = spell; // Tradition mit Zauber, Meistertrick …
                else if (o["sid"] is null && mapped.Input) o["sid"] = specification;
                else if (skill is not null) o["sid2"] = specification;
            }
            if (def.MaxLevel is not null)
                o["tier"] = def.OptionTarget == TraitOptionTarget.Language && entry is { Source: Templates.CultureSource, Free: true }
                    ? 4 // Muttersprache
                    : t.Level;
            return o;
        }

        /// <summary>Auswahl als Optolith-<c>sid</c>: Verweis („TAL_9“) oder Nummer der Ausprägung.</summary>
        private JsonNode? Sid(OptolithActivatable mapped, string option)
        {
            if ((Key(map.Skills, option) ?? Key(map.CombatTechniques, option) ?? Key(map.Spells, option)) is { } reference)
                return reference;
            return mapped.Options.FirstOrDefault(p => p.Value.Option == option).Key is { } number
                ? int.Parse(number, CultureInfo.InvariantCulture)
                : null;
        }

        private string? SpellKey(string name) =>
            rules.Spells.FirstOrDefault(s => s.Name == name) is { } spell ? Key(map.Spells, spell.Id) : null;

        private string Label(TraitState t) =>
            t.Entry.Option is { } option
                ? $"{t.Definition.Name} ({rules.FindSkill(option)?.Name ?? rules.FindSpell(option)?.Name ?? rules.FindLanguage(option)?.Name ?? rules.FindScript(option)?.Name ?? option})"
                : t.Definition.Name;

        // ---------------------------------------------------------------- Werte

        private void Skills(JsonObject hero)
        {
            var talents = new JsonObject();
            foreach (var s in _state.Skills.Where(s => s.Value > 0))
                if (Key(map.Skills, s.Definition.Id) is { } key) talents[key] = s.Value;
            hero["talents"] = talents;

            var ct = new JsonObject();
            foreach (var c in _state.CombatTechniques.Where(c => c.Value != rules.Creation.CombatTechniqueStartValue))
                if (Key(map.CombatTechniques, c.Definition.Id) is { } key) ct[key] = c.Value;
                else Skip(c.Definition.Name);
            hero["ct"] = ct;
        }

        private void Spells(JsonObject hero)
        {
            JsonObject spells = new(), liturgies = new();
            JsonArray cantrips = [], blessings = [];
            foreach (var s in _state.Spells)
            {
                if (Key(map.Spells, s.Definition.Id) is not { } key)
                {
                    Skip(s.Definition.Name);
                    continue;
                }
                switch (s.Definition.Group)
                {
                    case SpellGroup.Spell or SpellGroup.Ritual: spells[key] = s.Value ?? 0; break;
                    case SpellGroup.Liturgy or SpellGroup.Ceremony: liturgies[key] = s.Value ?? 0; break;
                    case SpellGroup.Cantrip: cantrips.Add(key); break;
                    case SpellGroup.Blessing: blessings.Add(key); break;
                }
            }
            hero["spells"] = spells;
            hero["cantrips"] = cantrips;
            hero["liturgies"] = liturgies;
            hero["blessings"] = blessings;
        }

        // ---------------------------------------------------------------- Ausrüstung

        private JsonObject Belongings()
        {
            var templates = new Dictionary<string, (string Key, int Group)>(StringComparer.Ordinal);
            foreach (var (key, item) in map.Items.OrderBy(p => OptolithMapping.OrderKey(p.Key)))
                templates.TryAdd(item.Id, (key, item.Group));

            var items = new JsonObject();
            var n = 0;
            var withoutStats = 0;
            foreach (var e in character.Equipment)
            {
                var def = rules.FindAnyItem(e.Id);
                var id = "ITEM_" + ++n;
                var o = new JsonObject { ["id"] = id, ["name"] = e.Name ?? def?.Name ?? "Gegenstand" };
                var custom = e.Name is not null || e.Weight is not null || e.Price is not null;
                if (def is not null && templates.TryGetValue(def.Id, out var template))
                {
                    o["gr"] = template.Group;
                    o["amount"] = e.Quantity;
                    o["isTemplateLocked"] = !custom;
                    o["template"] = template.Key;
                    // Ohne Sperre liest Optolith die Werte aus dem Gegenstand selbst
                    if (custom && def is WeaponDefinition or ArmorDefinition)
                        _notes.Add($"„{o["name"]}“ ist in Optolith nicht an die Vorlage gebunden; Kampfwerte bitte prüfen.");
                }
                else
                {
                    o["gr"] = def switch
                    {
                        WeaponDefinition { Ranged: true } => 2,
                        WeaponDefinition => 1,
                        ArmorDefinition => 4,
                        _ => 7,
                    };
                    o["amount"] = e.Quantity;
                    o["isTemplateLocked"] = false;
                    if (def is WeaponDefinition or ArmorDefinition) withoutStats++;
                }
                if (custom || def is null || !templates.ContainsKey(def.Id))
                {
                    if ((e.Weight ?? def?.WeightStone) is { } weight) o["weight"] = weight;
                    if ((e.Price ?? def?.PriceSilver) is { } price) o["price"] = price;
                }
                items[id] = o;
            }
            if (withoutStats > 0)
                _notes.Add($"{withoutStats} Waffen/Rüstungen gibt es in Optolith nicht als Vorlage; sie sind ohne Kampfwerte enthalten.");

            // Geld in Dukaten, Silbertalern, Hellern und Kreuzern
            var kreuzer = (long)Math.Round(Math.Max(0, _state.Equipment.Money) * 100);
            var purse = new JsonObject
            {
                ["d"] = (kreuzer / 1000).ToString(CultureInfo.InvariantCulture),
                ["s"] = (kreuzer / 100 % 10).ToString(CultureInfo.InvariantCulture),
                ["h"] = (kreuzer / 10 % 10).ToString(CultureInfo.InvariantCulture),
                ["k"] = (kreuzer % 10).ToString(CultureInfo.InvariantCulture),
            };
            return new JsonObject { ["items"] = items, ["armorZones"] = new JsonObject(), ["purse"] = purse };
        }

        // ---------------------------------------------------------------- Hilfen

        private string? Reverse(IReadOnlyDictionary<string, string> map, string? id, string what)
        {
            if (id is null) return null;
            if (Key(map, id) is { } key) return key;
            Skip($"{what} {rules.FindSpecies(id)?.Name ?? rules.FindCulture(id)?.Name ?? id}");
            return null;
        }

        private static string? Key(IReadOnlyDictionary<string, string> map, string id) =>
            map.Where(p => p.Value == id).Select(p => p.Key).OrderBy(OptolithMapping.OrderKey).FirstOrDefault();

        private void Skip(string what) => _skipped.Add(what);

        private static void Put(JsonObject o, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) o[key] = value.Trim();
        }

        private static void Put(JsonObject o, string key, int? value)
        {
            if (value is { } v) o[key] = v;
        }

        private static string Date(DateTimeOffset date) =>
            date.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

        private static string? Str(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
    }
}
