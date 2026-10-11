using System.Text.Json.Nodes;
using DSACharacterCreator.Content;
using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.DarkAid;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.Core.Tests;

public class DarkAidTests
{
    private static readonly RuleData Rules = TestRules.Data;

    private static readonly DarkAidMapping Map = LoadMap();

    private static DarkAidMapping LoadMap()
    {
        using var stream = EmbeddedContent.Open(DarkAidMapping.ResourcePath);
        return DarkAidMapping.Load(stream);
    }

    /// <summary>Erschaffene Diebin mit Gildenmagier-Tradition aus Dark Aid (Mittelländerin, Mittelreicherin, Erfahren).</summary>
    private const string Hero = """
        {
          "version": 6, "uuid": "{58dedfca-f670-40e8-8fbb-3e2346cda766}", "name": "Alrike", "sex": "female",
          "race": "mittellaender", "culture": "mittelreicher", "profession": "dieb", "professionname": "Diebin",
          "height": "17800", "weigth": "32000000", "haircolor": "blond", "eyecolor": "grün", "placeofbirth": "Gareth",
          "background": "Spricht im Schlaf.", "checkrange": true, "iscreated": true,
          "xp": { "startinglevel": "erfahren", "bonuses": [ { "ap": 50, "comment": "Start" }, { "ap": 30, "comment": "Abenteuer" } ] },
          "attributes": [ { "id": "mut", "level": 13 }, { "id": "klugheit", "level": 13 }, { "id": "intuition", "level": 13 },
                          { "id": "charisma", "level": 11 }, { "id": "fingerfertigkeit", "level": 14 },
                          { "id": "gewandtheit", "level": 14 }, { "id": "konstitution", "level": 11 },
                          { "id": "koerperkraft", "level": 11 } ],
          "basevalues": [ { "id": "lebensenergie", "improved": 2 },
                          { "id": "astralenergie", "bought": 2, "losses": [ { "source": "bindungdesstabes" } ] },
                          { "id": "karmaenergie" } ],
          "disadvantages": [
            { "id": "zauberer" },
            { "id": "schlechteeigenschaft", "variant": { "apcost": -5, "name": "Neugier", "type": "stringwithcost" } },
            { "id": "begabung", "variant": { "ruleelement": { "id": "verbergen", "type": "skill" }, "type": "ruleelement" } },
            { "id": "prinzipientreue", "level": 1, "variant": { "name": "Phex", "type": "string" } }
          ],
          "mothertongues": [ { "id": "sprache", "level": 3, "variant": { "ruleelement": { "id": "garethi", "type": "language" }, "type": "ruleelement" } } ],
          "specialabilities": [
            { "id": "sprache", "level": 3, "variant": { "ruleelement": { "id": "garethi", "type": "language" }, "type": "ruleelement" } },
            { "id": "sprache", "level": 2, "variant": { "ruleelement": { "id": "thorwalsch", "type": "language" }, "text": "Olporter-Dialekt", "type": "ruleelement" } },
            { "id": "schrift", "variant": { "ruleelement": { "id": "kuslikerzeichen", "type": "script" }, "type": "ruleelement" } },
            { "id": "ortskenntnis", "variant": { "name": "Gareth", "type": "string" } },
            { "id": "fertigkeitsspezialisierung", "variant": { "application": "Beschatten", "skill": "gassenwissen", "type": "skillspecialization" } },
            { "id": "schnellladenboegen" },
            { "id": "magischetradition", "variant": { "ruleelement": { "id": "gildenmagier", "type": "magicaltradition" }, "type": "ruleelement" } },
            { "id": "lieblingszauber", "variant": { "ruleelement": { "id": "ignifaxius", "type": "spell" }, "type": "ruleelement" } },
            { "ruleelement": { "apcost": 3, "group": "magische", "id": { "id": "{705daa9e-8785-4304-bf0c-15c3d08502e1}", "type": "specialability" },
                               "name": "Ignifaxius (Größere Reichweite)" } },
            { "ruleelement": { "apcost": 5, "group": "kampf", "id": { "id": "{8943804f-8022-45de-aa07-af0552999be2}", "type": "specialability" },
                               "name": "Hausregel-Finte" } }
          ],
          "enchantments": [ { "id": "bindungdesstabes" } ],
          "skills": [ { "id": "verbergen", "level": 8 }, { "id": "gassenwissen", "level": 7 }, { "id": "sinnesschaerfe", "level": 6 } ],
          "combattechniques": [ { "id": "dolche", "level": 10 }, { "id": "boegen", "level": 9 } ],
          "spells": [ { "enhancements": [ "intensiverstrahl" ], "id": "ignifaxius", "level": 10 }, { "id": "feuerfinger" },
                      { "id": "gibtesnicht", "level": 5 } ],
          "meleeweapons": [ { "amount": 1, "equipped": true, "handling": [], "ruleelement": { "id": "dolch", "type": "meleeweapon" } },
                            { "amount": 1, "handling": [], "name": "Hexenkrallen", "type": "meleeweapon" } ],
          "rangedweapons": [], "shields": [],
          "armor": [ { "amount": 1, "equipped": true, "ruleelement": { "id": "lederharnisch", "type": "armor" } } ],
          "otherobjects": [ { "amount": 2, "ruleelement": { "id": "kohlestift", "type": "equipment" } } ],
          "purse": "12345",
          "rules": { "focusrules": [], "locale": "de_DE", "rulebooks": [ "regelwerk", "aventurischemagie1" ],
                     "rulesystem": "dsa5aventurien", "version": "6" }
        }
        """;

    private static DarkAidImportResult Import(string json = Hero) =>
        DarkAidImport.Read(json, Rules, Map, SourceSelection.Default(Rules.Books));

    private static CharacterState Calculate(Character c) => new CharacterCalculator(Rules).Calculate(c);

    [Fact]
    public void Mapping_PointsOnlyToExistingContent()
    {
        var missing = Map.ExperienceLevels.Values.Where(id => Rules.ExperienceLevels.All(l => l.Id != id))
            .Concat(Map.Races.Values.Where(r => Rules.FindSpecies(r.Species) is null).Select(r => r.Species))
            .Concat(Map.Cultures.Values.Where(id => Rules.FindCulture(id) is null))
            .Concat(Map.Professions.Values.Where(p => Rules.FindProfession(p.Id) is not { } def ||
                                                      (p.Variant is not null && def.Variants.All(v => v.Id != p.Variant)))
                .Select(p => p.Id + "/" + p.Variant))
            .Concat(Map.Skills.Values.Where(id => Rules.FindSkill(id) is null))
            .Concat(Map.CombatTechniques.Values.Where(id => Rules.FindCombatTechnique(id) is null))
            .Concat(Map.Spells.Values.Where(id => Rules.FindSpell(id) is null))
            .Concat(Map.Languages.Values.Where(id => Rules.FindLanguage(id) is null))
            .Concat(Map.Scripts.Values.Where(id => Rules.FindScript(id) is null))
            .Concat(Map.Traditions.Values.Where(id => Rules.FindSpecialAbility(id)?.Tradition is null))
            .Concat(Map.Activatables.Values.Where(a => (Rules.FindTrait(a.Id) ?? Rules.FindSpecialAbility(a.Id)) is null).Select(a => a.Id))
            .Concat(Map.Enhancements.SelectMany(s => s.Value.Values.Select(e => (Spell: Map.Spells[s.Key], Id: e)))
                .Where(e => Rules.FindSpell(e.Spell)?.FindEnhancement(e.Id) is null).Select(e => e.Spell + "/" + e.Id))
            .Concat(Map.Items.Values.Where(id => Rules.FindAnyItem(id) is null))
            .Concat(Map.Rulebooks.Values.Where(id => !Rules.Books.Contains(id)))
            .ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void Detects_DarkAidHeroes()
    {
        Assert.True(DarkAidImport.IsDarkAidHero(Hero));
        Assert.False(DarkAidImport.IsDarkAidHero("""{ "clientVersion": "1.5.1", "activatable": {} }"""));
        Assert.False(DarkAidImport.IsDarkAidHero("kein JSON"));
        Assert.Throws<InvalidDataException>(() => Import("""{ "name": "x" }"""));
    }

    [Fact]
    public void Import_SetsOriginValuesAndPersonalData()
    {
        var c = Import().Character;
        Assert.Equal("Alrike", c.Name);
        Assert.Equal("species.mensch", c.Creation.Species);
        Assert.Equal("culture.mittelreicher", c.Creation.Culture);
        Assert.Equal("profession.streuner", c.Creation.Profession);
        Assert.Equal("dieb-in", c.Creation.ProfessionVariant);
        Assert.Equal("erfahren", c.Creation.ExperienceLevel);

        var state = Calculate(c);
        Assert.Equal(14, state.Attribute("FF"));
        Assert.Equal(8, state.Skills.Single(s => s.Definition.Id == "skill.verbergen").Value);
        Assert.Equal(0, state.Skills.Single(s => s.Definition.Id == "skill.klettern").Value);
        Assert.Equal(10, state.CombatTechniques.Single(t => t.Definition.Id == "ct.dolche").Value);
        Assert.Equal(6, state.CombatTechniques.Single(t => t.Definition.Id == "ct.raufen").Value);

        var p = c.Personal;
        Assert.Equal("weiblich", p.Gender);
        Assert.Equal("1,78 Schritt", p.Height);
        Assert.Equal("80 Stein", p.Weight);
        Assert.Equal("blond", p.HairColor);
        Assert.Equal("Gareth", p.Birthplace);
        Assert.Equal("Spricht im Schlaf.", p.Notes);
        Assert.Equal("Mittelländer", p.AppearanceTable);
    }

    [Fact]
    public void Import_MapsVariantsLevelsAndChoices()
    {
        var c = Import().Character;
        var traits = c.Creation.Traits;
        Assert.Contains(traits, t => t is { Id: "disadv.schlechte-eigenschaft", Specification: "Neugier", ManualAp: -5 });
        Assert.Contains(traits, t => t is { Id: "adv.begabung", Option: "skill.verbergen" });
        Assert.Contains(traits, t => t is { Id: "disadv.prinzipientreue", Level: 1, Specification: "Phex" });

        var abilities = c.Creation.SpecialAbilities;
        // Muttersprache = kostenlose Sprache der Kultur, erste Ortskenntnis = die der Kultur
        Assert.Contains(abilities, a => a is { Id: "sf.sprache", Option: "language.garethi", Free: true, Source: Templates.CultureSource });
        Assert.Contains(abilities, a => a is { Id: "sf.ortskenntnis", Specification: "Gareth", Free: true });
        Assert.Contains(abilities, a => a is { Id: "sf.sprache", Option: "language.thorwalsch", Level: 2, Specification: "Olporter-Dialekt", Free: false });
        Assert.Contains(abilities, a => a is { Id: "sf.schrift", Option: "script.kusliker-zeichen" });
        Assert.Contains(abilities, a => a is { Id: "sf.fertigkeitsspezialisierung-talente", Option: "skill.gassenwissen", Specification: "Beschatten" });
        Assert.Contains(abilities, a => a is { Id: "sf.schnellladen-kampftechnik", Option: "Bögen" });
        Assert.Contains(abilities, a => a.Id == "sf.tradition-gildenmagier");
        Assert.Contains(abilities, a => a is { Id: "sf.lieblingszauber", Option: "C-Zauber", Specification: "Ignifaxius" });
        Assert.Contains(abilities, a => a.Id == "sf.bindung-des-stabes");
    }

    [Fact]
    public void Import_SpellsEnhancementsAndEnergies()
    {
        var c = Import().Character;
        var ignifaxius = c.Creation.Spells.Single(s => s.Id == "spell.ignifaxius");
        Assert.Equal(10, ignifaxius.Value);
        // Erweiterung aus der Liste und alte Form als eigene Sonderfertigkeit
        Assert.Equal(["intensiver-strahl", "groessere-reichweite"], ignifaxius.Enhancements);
        Assert.Null(c.Creation.Spells.Single(s => s.Id == "cantrip.feuerfinger").Value);

        // improved = Zukauf, bought = Rückkauf der Bindungskosten
        Assert.Equal(2, c.Creation.BoughtEnergies[Energies.LifePoints]);
        Assert.Equal(2, c.Creation.BoughtEnergies[Energies.AstralRepurchased]);
        Assert.False(c.Creation.BoughtEnergies.ContainsKey(Energies.Astral));
    }

    [Fact]
    public void Import_CreatedHero_GetsBonusesInLog()
    {
        var c = Import().Character;
        Assert.Equal(CharacterPhase.Advancement, c.Phase);
        Assert.IsType<CreationFinishedEntry>(c.Log[0]);
        Assert.Equal([(50, "Start"), (30, "Abenteuer")], c.Log.OfType<ApAwardEntry>().Select(a => (a.Amount, a.Note)));
        Assert.Equal(1100 + 80, Calculate(c).Ap.Total);
    }

    [Fact]
    public void Import_EquipmentPurseAndReport()
    {
        var result = Import();
        var c = result.Character;
        Assert.Contains(c.Equipment, e => e is { Id: "weapon.dolch", Equipped: true, Paid: false });
        Assert.Contains(c.Equipment, e => e is { Id: "armor.lederharnisch", Equipped: true });
        Assert.Contains(c.Equipment, e => e is { Id: "item.kohlestift", Quantity: 2 });
        Assert.Contains(c.Equipment, e => e is { Id: "", Name: "Hexenkrallen" });
        Assert.Equal(123.45m, Calculate(c).Equipment.Money);
        Assert.Contains("aventurische-magie", c.EnabledBooks);

        var report = result.Notes[0];
        Assert.StartsWith("Nicht übernommen", report);
        Assert.Contains("Hausregel-Finte", report);
        Assert.Contains("gibtesnicht", report);
    }

    [Fact]
    public void Export_WritesDarkAidFormat()
    {
        var c = Import().Character;
        var json = JsonNode.Parse(DarkAidExport.Write(c, Rules, Map).Json)!;
        Assert.Equal(6, (int)json["version"]!);
        Assert.Equal("{" + c.Id.ToString("D") + "}", (string?)json["uuid"]);
        Assert.Equal("dieb", (string?)json["profession"]);
        Assert.Equal("mittellaender", (string?)json["race"]);
        Assert.Equal("17800", (string?)json["height"]);
        Assert.Equal("32000000", (string?)json["weigth"]);
        Assert.Equal("12345", (string?)json["purse"]);
        Assert.True((bool)json["iscreated"]!);
        Assert.Equal(2, json["xp"]!["bonuses"]!.AsArray().Count);
        Assert.Contains(json["specialabilities"]!.AsArray(), a => (string?)a!["id"] == "schnellladenboegen" && a["variant"] is null);
        Assert.Contains(json["specialabilities"]!.AsArray(), a => (string?)a!["id"] == "magischetradition" &&
                                                                  (string?)a["variant"]!["ruleelement"]!["id"] == "gildenmagier");
        Assert.Contains(json["enchantments"]!.AsArray(), a => (string?)a!["id"] == "bindungdesstabes");
        Assert.Single(json["mothertongues"]!.AsArray());
        var astral = json["basevalues"]!.AsArray().Single(b => (string?)b!["id"] == "astralenergie")!;
        Assert.Equal(2, (int)astral["bought"]!);
        Assert.Equal("bindungdesstabes", (string?)astral["losses"]![0]!["source"]);
        // Erweiterung, die Dark Aid nicht kennt: eigene Sonderfertigkeit wie in älteren Fassungen
        Assert.Contains(json["specialabilities"]!.AsArray(), a => (string?)a!["ruleelement"]?["name"] == "Ignifaxius (Größere Reichweite)");
    }

    [Fact]
    public void ExportAndImport_KeepTheHero()
    {
        var first = Import().Character;
        var again = Import(DarkAidExport.Write(first, Rules, Map).Json).Character;
        var a = Calculate(first);
        var b = Calculate(again);
        Assert.Equal(a.Ap.Total, b.Ap.Total);
        Assert.Equal(a.Ap.Spent, b.Ap.Spent);
        string Entries(CharacterState s) => string.Join("; ", s.Traits.Concat(s.SpecialAbilities)
            .Select(t => $"{t.Definition.Id}|{t.Entry.Option}|{t.Entry.Specification}|{t.Level}|{t.Ap}").Order());
        Assert.Equal(Entries(a), Entries(b));
        Assert.Equal(first.Creation.Spells.Single(s => s.Id == "spell.ignifaxius").Enhancements,
            again.Creation.Spells.Single(s => s.Id == "spell.ignifaxius").Enhancements);
    }

    [Fact]
    public void Export_NewHeroInCreation()
    {
        var c = new Character { Name = "Neu" };
        c.Creation.ExperienceLevel = "kompetent";
        var json = JsonNode.Parse(DarkAidExport.Write(c, Rules, Map).Json)!;
        Assert.Null(json["iscreated"]);
        Assert.Equal("kompetent", (string?)json["xp"]!["startinglevel"]);
        var back = Import(json.ToJsonString()).Character;
        Assert.Equal(CharacterPhase.Creation, back.Phase);
        Assert.Equal("Neu", back.Name);
    }
}
