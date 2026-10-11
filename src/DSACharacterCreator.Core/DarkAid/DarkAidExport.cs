using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.Core.DarkAid;

/// <summary>Dark-Aid-Held als JSON-Text und Hinweise, was Dark Aid nicht kennt.</summary>
public sealed record DarkAidExportResult(string Json, IReadOnlyList<string> Notes);

/// <summary>
/// Schreibt den aktuellen Stand eines Helden im Format von The Dark Aid (<c>*.tdc</c>, Version 6). Dark Aid speichert nur
/// den Endstand; die AP-Gutschriften des Protokolls werden zu AP-Boni (<c>xp.bonuses</c>).
/// </summary>
public static class DarkAidExport
{
    /// <summary>Fokusregeln eines neuen Dark-Aid-Helden; Zauber- und Liturgieerweiterungen sind in der App immer aktiv.</summary>
    private static readonly string[] EnabledFocusRules =
        ["trefferzonenregeln1", "kampfstile", "zaubererweiterungen", "liturgieerweiterungen", "zauberstile", "talismane",
         "predigtenundvisionen", "talentstile", "liturgiestile"];

    private static readonly string[] DisabledFocusRules =
        ["spezielleverletzungenundbehandlung", "hoehereverteidigung", "kontakteundverbindungen", "lebensstilregeln",
         "startkapitalnacherfahrung", "talentgruppenprobe", "kontaktebeiderheldenerschaffung", "eigenschaftsobergrenze",
         "uthurischesprachen", "sprachspezialisierungen", "myranischesprachen", "praegungen", "merkmalsproben",
         "heldenundmeisterpersonen"];

    private static readonly string[] BaseValues =
        ["lebensenergie", "astralenergie", "karmaenergie", "seelenkraft", "zaehigkeit", "ausweichen", "initiative",
         "geschwindigkeit", "schicksalspunkte", "wundschwelle"];

    public static DarkAidExportResult Write(Character character, RuleData rules, DarkAidMapping map) =>
        new Exporter(character, rules, map).Run();

    private sealed class Exporter(Character character, RuleData rules, DarkAidMapping map)
    {
        private readonly List<string> _notes = [];
        private readonly List<string> _skipped = [];
        private readonly CharacterState _state = new CharacterCalculator(rules).Calculate(character);
        private CreationLayer Layer => _state.Advancement?.Working ?? character.Creation;

        /// <summary>Eintrag der App (ID, Auswahl, Präzisierung) → Dark-Aid-Eintrag mit dieser Auswahl („schnellladenboegen“).</summary>
        private readonly Dictionary<(string, string?, string?), string> _withChoice = [];

        /// <summary>Eintrag der App → Dark-Aid-Eintrag ohne feste Auswahl.</summary>
        private readonly Dictionary<string, string> _plain = new(StringComparer.Ordinal);

        public DarkAidExportResult Run()
        {
            foreach (var (key, a) in map.Activatables)
            {
                if (a.Option is null && a.Text is null) _plain.TryAdd(a.Id, key);
                else _withChoice.TryAdd((a.Id, a.Option, a.Text), key);
            }

            var origin = _state.Origin;
            var hero = new JsonObject
            {
                ["version"] = 6,
                ["uuid"] = "{" + character.Id.ToString("D") + "}",
                ["name"] = character.Name is { Length: > 0 } name ? name : "Unbenannt",
                ["sex"] = PersonalRolls.GenderOf(character.Personal.Gender) == NameGender.Female ? "female" : "male",
                ["checkrange"] = true,
            };
            Race(hero);
            hero["culture"] = Reverse(map.Cultures, origin.Culture?.Id, "Kultur") ?? "";
            Profession(hero);
            Personal(hero);
            hero["xp"] = Experience();
            if (character.Phase == CharacterPhase.Advancement) hero["iscreated"] = true;
            hero["attributes"] = new JsonArray(map.Attributes
                .Select(p => (JsonNode)new JsonObject { ["id"] = p.Key, ["level"] = _state.Attribute(p.Value) }).ToArray());
            hero["basevalues"] = BaseValueList();
            Activatables(hero);
            Skills(hero);
            Spells(hero);
            Equipment(hero);
            hero["purse"] = ((long)Math.Round(_state.Equipment.Money * 100)).ToString(CultureInfo.InvariantCulture);
            hero["rules"] = Rules();

            if (character.Companions.Count > 0)
                _notes.Add("Tiergefährten kennt das Dark-Aid-Format nicht; sie sind nicht enthalten.");
            if (character.Log.Any(e => e is not (ApAwardEntry or CreationFinishedEntry)))
                _notes.Add("Dark Aid speichert nur den Endstand; das Steigerungsprotokoll ist nicht enthalten.");
            if (_skipped.Count > 0)
                _notes.Insert(0, "Nicht in Dark Aid vorhanden: " + string.Join(", ", _skipped.Distinct()) + ".");
            return new DarkAidExportResult(hero.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), _notes);
        }

        // ---------------------------------------------------------------- Herkunft

        private void Race(JsonObject hero)
        {
            var species = _state.Origin.Species;
            if (species is null)
            {
                hero["race"] = "";
                return;
            }
            var table = character.Personal.AppearanceTable ?? rules.FindAppearance(species.Id)?.Choices[0].Name;
            var key = map.Races.Where(r => r.Value.Species == species.Id)
                .OrderBy(r => r.Value.Table == table ? 0 : r.Value.Table is null && r.Key != Compact(species.Name) ? 1 : 2)
                .Select(r => r.Key).FirstOrDefault();
            if (key is null) Skip($"Spezies {species.Name}");
            hero["race"] = key ?? "";
        }

        private void Profession(JsonObject hero)
        {
            var origin = _state.Origin;
            if (origin.Profession is not { } profession)
            {
                hero["profession"] = "";
                hero["professionname"] = "";
                return;
            }
            var variant = origin.Variant?.Id;
            var key = map.Professions.Where(p => p.Value.Id == profession.Id && p.Value.Variant == variant).Select(p => p.Key)
                .FirstOrDefault();
            if (key is null && variant is not null)
            {
                key = map.Professions.Where(p => p.Value.Id == profession.Id && p.Value.Variant is null).Select(p => p.Key)
                    .FirstOrDefault();
                if (key is not null) Skip($"Variante {origin.Variant!.Name}");
            }
            if (key is null)
                _notes.Add($"Die Profession {profession.Name} gibt es in Dark Aid nicht; sie ist nur als Name eingetragen.");
            hero["profession"] = key ?? "";
            hero["professionname"] = origin.Variant?.Name ?? profession.Name;
        }

        private void Personal(JsonObject hero)
        {
            var p = character.Personal;
            hero["height"] = PersonalRolls.ParseHeight(p.Height) is { } height
                ? (height * DarkAidImport.HeightUnitsPerHalfFinger).ToString(CultureInfo.InvariantCulture)
                : "";
            hero["weigth"] = Stone(p.Weight) is { } stone
                ? ((long)Math.Round(stone * DarkAidImport.WeightUnitsPerStone)).ToString(CultureInfo.InvariantCulture)
                : "";
            hero["haircolor"] = p.HairColor?.Trim() ?? "";
            hero["eyecolor"] = p.EyeColor?.Trim() ?? "";
            if (p.Birthplace is { Length: > 0 } place) hero["placeofbirth"] = place.Trim();
            if (p.Notes is { Length: > 0 } notes) hero["background"] = notes;

            static decimal? Stone(string? text)
            {
                var number = new string((text ?? "").Trim().TakeWhile(c => char.IsDigit(c) || c is ',' or '.').ToArray());
                return decimal.TryParse(number.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var v)
                    ? v
                    : null;
            }
        }

        /// <summary>Erfahrungsgrad und die AP-Gutschriften des Protokolls als AP-Boni.</summary>
        private JsonObject Experience()
        {
            var xp = new JsonObject
            {
                ["startinglevel"] = Reverse(map.ExperienceLevels, _state.ExperienceLevel.Id, "Erfahrungsgrad") ?? "kompetent",
            };
            var bonuses = character.Log.OfType<ApAwardEntry>().Where(a => a.Amount != 0)
                .Select(a => (JsonNode)new JsonObject { ["ap"] = a.Amount, ["comment"] = a.Note ?? "" }).ToArray();
            if (bonuses.Length > 0) xp["bonuses"] = new JsonArray(bonuses);
            return xp;
        }

        // ---------------------------------------------------------------- Grundwerte

        private JsonArray BaseValueList()
        {
            var energies = Layer.BoughtEnergies;
            var list = new JsonArray();
            foreach (var id in BaseValues)
            {
                var o = new JsonObject { ["id"] = id };
                var (improved, back) = id switch
                {
                    "lebensenergie" => (energies.GetValueOrDefault(Energies.LifePoints), 0),
                    "astralenergie" => (energies.GetValueOrDefault(Energies.Astral), energies.GetValueOrDefault(Energies.AstralRepurchased)),
                    "karmaenergie" => (energies.GetValueOrDefault(Energies.Karma), energies.GetValueOrDefault(Energies.KarmaRepurchased)),
                    _ => (0, 0),
                };
                // „improved“ = zugekauft, „bought“ = zurückgekaufter Verlust
                if (back > 0) o["bought"] = back;
                if (improved > 0) o["improved"] = improved;
                // Permanente AsP durch Bindungen: Dark Aid führt die bindende Sonderfertigkeit als Verlust
                if (id == "astralenergie")
                {
                    var losses = _state.SpecialAbilities.Where(t => TraitRules.PermanentAstral(t.Definition, t.Entry) > 0)
                        .Select(t => Key(t)).OfType<string>()
                        .Select(k => (JsonNode)new JsonObject { ["source"] = k }).ToArray();
                    if (losses.Length > 0) o["losses"] = new JsonArray(losses);
                    if (energies.GetValueOrDefault(Energies.AstralLost) > 0)
                        _notes.Add("Sonstige permanente AsP-Verluste kennt das Dark-Aid-Format nicht.");
                }
                list.Add((JsonNode)o);
            }
            return list;
        }

        // ---------------------------------------------------------------- Vor-/Nachteile und Sonderfertigkeiten

        private void Activatables(JsonObject hero)
        {
            JsonArray traits = [], abilities = [], enchantments = [], mother = [];
            foreach (var t in _state.Traits.Concat(_state.SpecialAbilities))
            {
                var instance = Instance(t);
                if (instance is null)
                {
                    Skip(Label(t));
                    continue;
                }
                var key = Str(instance["id"])!;
                if (!t.Definition.IsSpecialAbility) traits.Add((JsonNode)instance);
                else if (map.Enchantments.Contains(key)) enchantments.Add((JsonNode)new JsonObject { ["id"] = key });
                else abilities.Add((JsonNode)instance);
                if (t.Definition.OptionTarget == TraitOptionTarget.Language && t.Entry is { Source: Templates.CultureSource, Free: true })
                    mother.Add(instance.DeepClone());
            }
            hero["disadvantages"] = traits;
            hero["specialabilities"] = abilities;
            hero["mothertongues"] = mother;
            if (enchantments.Count > 0) hero["enchantments"] = enchantments;
        }

        private string? Key(TraitState t)
        {
            var def = t.Definition;
            var entry = t.Entry;
            if (def.Tradition is not null)
                return map.Traditions.FirstOrDefault(p => p.Value == def.Id).Key is { } tradition
                    ? map.BlessedTraditions.Contains(tradition) ? "geweihtentradition" : "magischetradition"
                    : null;
            return _withChoice.GetValueOrDefault((def.Id, entry.Option, null))
                   ?? _withChoice.GetValueOrDefault((def.Id, null, entry.Specification))
                   ?? _plain.GetValueOrDefault(def.Id);
        }

        private JsonObject? Instance(TraitState t)
        {
            var def = t.Definition;
            var entry = t.Entry;
            if (Key(t) is not { } key) return null;
            var o = new JsonObject { ["id"] = key };
            if (def.MaxLevel is not null) o["level"] = t.Level;
            if (key is "magischetradition" or "geweihtentradition")
            {
                var tradition = map.Traditions.First(p => p.Value == def.Id).Key;
                o["variant"] = RuleElement(tradition, key == "magischetradition" ? "magicaltradition" : "blessedtradition");
                return o;
            }
            // Die Auswahl steckt schon im Dark-Aid-Eintrag („schnellladenboegen“)
            if (map.Activatables[key] is { } mapped && (mapped.Option is not null || mapped.Text is not null)) return o;
            if (map.Variants.GetValueOrDefault(key) is not { } type) return o;
            var variant = Variant(type, t);
            if (variant is null) return null;
            o["variant"] = variant;
            return o;
        }

        /// <summary>Auswahl in der Form, die Dark Aid für diesen Eintrag erwartet.</summary>
        private JsonObject? Variant(string type, TraitState t)
        {
            var entry = t.Entry;
            var text = entry.Specification;
            switch (type.Split(':')[0])
            {
                case "string" or "terrain":
                    // ohne Präzisierung (kostenlose Ortskenntnis der Kultur) mit leerem Text
                    return new JsonObject { ["name"] = entry.Option ?? text ?? "", ["type"] = type };
                case "stringwithcost":
                    return new JsonObject { ["apcost"] = t.Ap ?? 0, ["name"] = entry.Option ?? text ?? "", ["type"] = type };
                case "tradesecret":
                    // Unbekannte Berufsgeheimnisse behalten beim Import ihre Dark-Aid-ID als Präzisierung
                    return (NameKey("tradesecret", text) ?? (text is { Length: > 0 } && text.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c)) ? text : null)) is { } secret
                        ? new JsonObject
                        {
                            ["apcost"] = t.Ap ?? 0,
                            ["tradesecret"] = new JsonObject { ["id"] = secret, ["type"] = "tradesecret" },
                            ["type"] = type,
                        }
                        : null;
                case "skillspecialization":
                    return Key(map.Skills, entry.Option) is { } skill
                        ? new JsonObject { ["application"] = text ?? "", ["skill"] = skill, ["type"] = type }
                        : null;
                case "specializedknowledge":
                    if (Key(map.Skills, entry.Option) is not { } known) return null;
                    var parts = (text ?? "").Split(", ");
                    return new JsonObject
                    {
                        ["application1"] = parts[0], ["application2"] = parts.Length > 1 ? parts[1] : parts[0], ["skill"] = known,
                        ["type"] = type,
                    };
                case "ruleelement":
                    return Reference(type.Split(':').Skip(1).ToList(), t);
                default:
                    return null;
            }
        }

        /// <summary>Gewählte Sprache, Schrift, Fertigkeit, Kampftechnik, Zauber, Merkmal oder sonstige Auswahl.</summary>
        private JsonObject? Reference(List<string> types, TraitState t)
        {
            var entry = t.Entry;
            var option = entry.Option;
            var text = entry.Specification;
            if (Key(map.Languages, option) is { } language)
            {
                var o = RuleElement(language, "language");
                if (text is { Length: > 0 }) o["text"] = text; // Dialekt
                return o;
            }
            if (Key(map.Scripts, option) is { } script) return RuleElement(script, "script");
            if (Key(map.CombatTechniques, option) is { } ct) return RuleElement(ct, "combattechnique");
            if (Key(map.Skills, option) is { } skill) return RuleElement(skill, "skill");
            if (rules.FindSpell(option) is { } spell && Key(map.Spells, spell.Id) is { } spellKey)
                return RuleElement(spellKey, IsChant(spell) ? "chant" : "spell");
            // Lieblingszauber: Ausprägung „B-Zauber“, Zauber als Präzisierung
            if (text is not null && rules.Spells.FirstOrDefault(s => s.Name == text) is { } named &&
                Key(map.Spells, named.Id) is { } namedKey)
                return RuleElement(namedKey, IsChant(named) ? "chant" : "spell");
            foreach (var type in types.Count > 0 ? types : map.Names.Keys.ToList())
                if (NameKey(type, option ?? text) is { } id)
                    return RuleElement(id, type);
            return null;
        }

        private static JsonObject RuleElement(string id, string type) => new()
        {
            ["ruleelement"] = new JsonObject { ["id"] = id, ["type"] = type },
            ["type"] = "ruleelement",
        };

        private string? NameKey(string type, string? name) =>
            name is null ? null : map.Names.GetValueOrDefault(type)?.FirstOrDefault(p => string.Equals(p.Value, name.Trim(),
                StringComparison.OrdinalIgnoreCase)).Key;

        private string Label(TraitState t) =>
            t.Entry.Option is { } option
                ? $"{t.Definition.Name} ({rules.FindSkill(option)?.Name ?? rules.FindSpell(option)?.Name ?? rules.FindLanguage(option)?.Name ?? rules.FindScript(option)?.Name ?? option})"
                : t.Entry.Specification is { } spec ? $"{t.Definition.Name} ({spec})" : t.Definition.Name;

        // ---------------------------------------------------------------- Werte

        private void Skills(JsonObject hero)
        {
            var skills = new JsonArray();
            foreach (var s in _state.Skills.Where(s => s.Value > 0))
                if (Key(map.Skills, s.Definition.Id) is { } key)
                    skills.Add((JsonNode)new JsonObject { ["id"] = key, ["level"] = s.Value });
            hero["skills"] = skills;

            var combat = new JsonArray();
            foreach (var c in _state.CombatTechniques.Where(c => c.Value != rules.Creation.CombatTechniqueStartValue))
                if (Key(map.CombatTechniques, c.Definition.Id) is { } key)
                    combat.Add((JsonNode)new JsonObject { ["id"] = key, ["level"] = c.Value });
                else Skip(c.Definition.Name);
            hero["combattechniques"] = combat;
        }

        private void Spells(JsonObject hero)
        {
            JsonArray spells = [], chants = [];
            foreach (var s in _state.Spells)
            {
                if (Key(map.Spells, s.Definition.Id) is not { } key)
                {
                    Skip(s.Definition.Name);
                    continue;
                }
                var o = new JsonObject();
                var enhancements = new JsonArray();
                foreach (var e in s.Enhancements)
                {
                    var enhancement = map.Enhancements.GetValueOrDefault(key)?.FirstOrDefault(p => p.Value == e.Definition.Id).Key;
                    if (enhancement is not null) enhancements.Add((JsonNode)JsonValue.Create(enhancement));
                    else _ownAbilities.Add(OwnEnhancement(s.Definition, e.Definition));
                }
                if (enhancements.Count > 0) o["enhancements"] = enhancements;
                o["id"] = key;
                if (s.Value is { } value) o["level"] = value;
                (IsChant(s.Definition) ? chants : spells).Add((JsonNode)o);
            }
            hero["spells"] = spells;
            hero["chants"] = chants;
            if (_ownAbilities.Count > 0)
            {
                foreach (var own in _ownAbilities) hero["specialabilities"]!.AsArray().Add((JsonNode)own);
                _notes.Add($"{_ownAbilities.Count} Zaubererweiterungen kennt Dark Aid nicht; sie sind als eigene Sonderfertigkeiten " +
                           "„Zauber (Erweiterung)“ enthalten.");
            }
        }

        /// <summary>Selbst angelegte Sonderfertigkeiten für Erweiterungen, die Dark Aid nicht kennt (wie in älteren Fassungen).</summary>
        private readonly List<JsonObject> _ownAbilities = [];

        private static JsonObject OwnEnhancement(SpellDefinition spell, SpellEnhancement enhancement)
        {
            var name = $"{spell.Name} ({enhancement.Name})";
            // gleichbleibende ID je Name, damit ein erneuter Export dieselbe Sonderfertigkeit ergibt
            var guid = new Guid(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(name)));
            return new JsonObject
            {
                ["ruleelement"] = new JsonObject
                {
                    ["apcost"] = enhancement.Ap,
                    ["group"] = "magische",
                    ["id"] = new JsonObject { ["id"] = "{" + guid.ToString("D") + "}", ["type"] = "specialability" },
                    ["name"] = name,
                    ["rulesdescription"] = "",
                },
            };
        }

        private static bool IsChant(SpellDefinition spell) =>
            spell.Group is SpellGroup.Liturgy or SpellGroup.Ceremony or SpellGroup.Blessing;

        // ---------------------------------------------------------------- Ausrüstung

        private void Equipment(JsonObject hero)
        {
            var lists = new Dictionary<string, JsonArray>
            {
                ["meleeweapon"] = [], ["rangedweapon"] = [], ["shield"] = [], ["armor"] = [], ["equipment"] = [],
            };
            var missing = 0;
            foreach (var e in character.Equipment)
            {
                var item = map.Items.FirstOrDefault(p => p.Value == e.Id && e.Id != "").Key;
                if (item is null)
                {
                    missing++;
                    continue;
                }
                var cut = item.IndexOf('/');
                var type = item[..cut];
                var o = new JsonObject { ["amount"] = e.Quantity };
                if (type is "meleeweapon" or "shield" or "armor") o["equipped"] = e.Equipped;
                if (type is "meleeweapon" or "shield") o["handling"] = new JsonArray();
                o["ruleelement"] = new JsonObject { ["id"] = item[(cut + 1)..], ["type"] = type };
                lists[type].Add((JsonNode)o);
            }
            hero["meleeweapons"] = lists["meleeweapon"];
            hero["rangedweapons"] = lists["rangedweapon"];
            hero["shields"] = lists["shield"];
            hero["armor"] = lists["armor"];
            hero["otherobjects"] = lists["equipment"];
            if (missing > 0)
                _notes.Add($"{missing} Gegenstände ohne Gegenstück in Dark Aid (eigene Gegenstände) sind nicht enthalten.");
        }

        // ---------------------------------------------------------------- Regeln

        private JsonObject Rules()
        {
            var focus = new JsonArray();
            foreach (var id in EnabledFocusRules) focus.Add((JsonNode)new JsonObject { ["id"] = id });
            foreach (var id in DisabledFocusRules) focus.Add((JsonNode)new JsonObject { ["choice"] = -1, ["id"] = id });
            var books = new JsonArray();
            foreach (var (key, book) in map.Rulebooks)
                if (key == "regelwerk" || character.EnabledBooks.Contains(book))
                    books.Add((JsonNode)JsonValue.Create(key));
            return new JsonObject
            {
                ["focusrules"] = focus,
                ["locale"] = "de_DE",
                ["rulebooks"] = books,
                ["rulesystem"] = "dsa5aventurien",
                ["version"] = "6",
            };
        }

        // ---------------------------------------------------------------- Hilfen

        private string? Reverse(IReadOnlyDictionary<string, string> dictionary, string? id, string what)
        {
            if (id is null) return null;
            if (Key(dictionary, id) is { } key) return key;
            Skip($"{what} {rules.FindCulture(id)?.Name ?? id}");
            return null;
        }

        private static string? Key(IReadOnlyDictionary<string, string> dictionary, string? id) =>
            id is null ? null : dictionary.FirstOrDefault(p => p.Value == id).Key;

        private void Skip(string what) => _skipped.Add(what);

        private static string Compact(string name) =>
            new(name.ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Where(char.IsLetterOrDigit).ToArray());

        private static string? Str(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
    }
}
