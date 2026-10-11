using System.Text.Json.Nodes;
using DSACharacterCreator.Content;
using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Optolith;
using DSACharacterCreator.Core.Persistence;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.Core.Tests;

public class OptolithTests
{
    private static readonly RuleData Rules = TestRules.Data;

    private static readonly OptolithMapping Map = LoadMap();

    private static OptolithMapping LoadMap()
    {
        using var stream = EmbeddedContent.Open(OptolithMapping.ResourcePath);
        return OptolithMapping.Load(stream);
    }

    /// <summary>Abgeschlossene Kriegerin aus Optolith 1.5 (Mensch, Mittelreicherin, Erfahren, 1300 AP).</summary>
    private const string Hero = """
        {
          "clientVersion": "1.5.1", "dateCreated": "2024-03-01T10:00:00.000Z", "dateModified": "2024-03-02T10:00:00.000Z",
          "id": "H_1709287200000", "phase": 3, "locale": "de-DE", "name": "Alrike",
          "ap": { "total": 1300 }, "el": "EL_3", "r": "R_1", "rv": "RV_1", "c": "C_8", "p": "P_9", "sex": "f",
          "pers": { "family": "Gerbelstein", "age": "24", "haircolor": 3, "eyecolor": 4, "size": "178", "weight": "80",
                    "socialstatus": 3, "cultureAreaKnowledge": "Gareth", "otherinfo": "Spricht im Schlaf." },
          "attr": {
            "values": [ { "id": "ATTR_1", "value": 14 }, { "id": "ATTR_2", "value": 11 }, { "id": "ATTR_3", "value": 12 },
                        { "id": "ATTR_4", "value": 11 }, { "id": "ATTR_5", "value": 11 }, { "id": "ATTR_6", "value": 13 },
                        { "id": "ATTR_7", "value": 14 }, { "id": "ATTR_8", "value": 14 } ],
            "attributeAdjustmentSelected": "ATTR_8", "ae": 0, "kp": 0, "lp": 2,
            "permanentAE": { "lost": 0, "redeemed": 0 }, "permanentKP": { "lost": 0, "redeemed": 0 }, "permanentLP": { "lost": 0 }
          },
          "activatable": {
            "ADV_4": [ { "sid": "TAL_10" } ],
            "DISADV_1": [ { "sid": 8, "tier": 1 } ],
            "DISADV_33": [ { "sid": 1 } ],
            "SA_9": [ { "sid": "TAL_5", "sid2": 2 } ],
            "SA_29": [ { "sid": 8, "tier": 4 }, { "sid": 6, "tier": 2 } ],
            "SA_22": [ { "sid": "Rommilys" } ],
            "ADV_0": [ { "sid": "Glückspilz", "cost": 5 } ]
          },
          "talents": { "TAL_5": 10, "TAL_10": 8, "TAL_4": 6 },
          "ct": { "CT_12": 12, "CT_3": 9 },
          "spells": {}, "cantrips": [], "liturgies": {}, "blessings": [],
          "belongings": {
            "items": {
              "ITEM_1": { "id": "ITEM_1", "name": "Langschwert", "gr": 1, "amount": 1, "isTemplateLocked": true, "template": "ITEMTPL_35" },
              "ITEM_2": { "id": "ITEM_2", "name": "Glücksbringer", "gr": 15, "amount": 1, "isTemplateLocked": false, "weight": 0.1, "price": 2 }
            },
            "armorZones": {}, "purse": { "d": "3", "s": "5", "h": "0", "k": "0" }
          },
          "rules": {}, "pets": {}
        }
        """;

    private static OptolithImportResult Import(string json = Hero) =>
        OptolithImport.Read(json, Rules, Map, SourceSelection.Default(Rules.Books));

    private static CharacterState Calculate(Character c) => new CharacterCalculator(Rules).Calculate(c);

    [Fact]
    public void Mapping_PointsOnlyToExistingContent()
    {
        var missing = Map.ExperienceLevels.Values.Where(id => Rules.ExperienceLevels.All(l => l.Id != id))
            .Concat(Map.Species.Values.Where(id => Rules.FindSpecies(id) is null))
            .Concat(Map.Cultures.Values.Where(id => Rules.FindCulture(id) is null))
            .Concat(Map.Professions.Values.Where(p => Rules.FindProfession(p.Id) is null).Select(p => p.Id))
            .Concat(Map.ProfessionVariants.Values.Where(v => Rules.FindProfession(v.Profession)?.Variants.All(x => x.Id != v.Variant) != false)
                .Select(v => v.Profession + "/" + v.Variant))
            .Concat(Map.Skills.Values.Where(id => Rules.FindSkill(id) is null))
            .Concat(Map.CombatTechniques.Values.Where(id => Rules.FindCombatTechnique(id) is null))
            .Concat(Map.Spells.Values.Where(id => Rules.FindSpell(id) is null))
            .Concat(Map.Activatables.Values.Where(a => (Rules.FindTrait(a.Id) ?? Rules.FindSpecialAbility(a.Id)) is null).Select(a => a.Id))
            .Concat(Map.Activatables.Values.SelectMany(a => a.Options.Values).Select(o => o.Option)
                .Where(o => o is not null && o.Contains('.') && Rules.FindLanguage(o) is null && Rules.FindScript(o) is null)!)
            .Concat(Map.Items.Values.Where(i => Rules.FindAnyItem(i.Id) is null).Select(i => i.Id))
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void Detects_OptolithHeroes()
    {
        Assert.True(OptolithImport.IsOptolithHero(Hero));
        Assert.False(OptolithImport.IsOptolithHero(CharacterSerializer.Serialize(new Character { Name = "Alrik" })));
        Assert.False(OptolithImport.IsOptolithHero("kein JSON"));
    }

    [Fact]
    public void Import_SetsOriginAndValues()
    {
        var c = Import().Character;
        var state = Calculate(c);

        Assert.Equal("Alrike", c.Name);
        Assert.Equal(("erfahren", "species.mensch", "culture.mittelreicher", "profession.kriegerin"),
            (c.Creation.ExperienceLevel, c.Creation.Species, c.Creation.Culture, c.Creation.Profession));
        Assert.Equal(["KK"], c.Creation.SpeciesAttributeChoices);
        Assert.Equal(14, state.Attribute("KK"));
        Assert.Equal(10, state.Skills.First(s => s.Definition.Id == "skill.kraftakt").Value);
        Assert.Equal(0, state.Skills.First(s => s.Definition.Id == "skill.zechen").Value);
        Assert.Equal(12, state.CombatTechniques.First(t => t.Definition.Id == "ct.schwerter").Value);
        Assert.Equal(6, state.CombatTechniques.First(t => t.Definition.Id == "ct.boegen").Value);
        Assert.Equal(2, c.Creation.BoughtEnergies[Energies.LifePoints]);
    }

    [Fact]
    public void Import_FinishedHero_GetsApAwardInLog()
    {
        var c = Import().Character;

        Assert.Equal(CharacterPhase.Advancement, c.Phase);
        Assert.IsType<CreationFinishedEntry>(c.Log[0]);
        Assert.Equal(200, Assert.IsType<ApAwardEntry>(c.Log[1]).Amount);
        Assert.Equal(1300, Calculate(c).Ap.Total);
    }

    [Fact]
    public void Import_MapsOptionsLevelsAndTexts()
    {
        var creation = Import().Character.Creation;

        var native = creation.SpecialAbilities.Single(e => e.Id == "sf.sprache" && e.Option == "language.garethi");
        Assert.Equal((3, Templates.CultureSource, true), (native.Level, native.Source, native.Free));
        Assert.Equal(2, creation.SpecialAbilities.Single(e => e.Option == "language.bosparano").Level);
        // Ortskenntnis der Kultur (kostenlos) und eine in Optolith gekaufte zweite
        Assert.Equal("Gareth", creation.SpecialAbilities.Single(e => e.Id == "sf.ortskenntnis" && e.Free).Specification);
        Assert.Equal("Rommilys", creation.SpecialAbilities.Single(e => e.Id == "sf.ortskenntnis" && !e.Free).Specification);
        var specialization = creation.SpecialAbilities.Single(e => e.Id == "sf.fertigkeitsspezialisierung-talente");
        Assert.Equal(("skill.kraftakt", Map.SkillApplications["TAL_5"]["2"]), (specialization.Option, specialization.Specification));
        Assert.Equal("skill.sinnesschaerfe", creation.Traits.Single(e => e.Id == "adv.begabung").Option);
        var arrogance = creation.Traits.Single(e => e.Id == "disadv.persoenlichkeitsschwaeche");
        Assert.Equal(("Arroganz", -10), (arrogance.Specification, arrogance.ManualAp));
        Assert.Equal("Spinnen", creation.Traits.Single(e => e.Id == "disadv.angst-vor").Specification);
    }

    [Fact]
    public void Import_ReportsWhatIsMissing()
    {
        var notes = Import().Notes;

        Assert.Contains(notes, n => n.Contains("Eigener Vorteil", StringComparison.Ordinal));
    }

    [Fact]
    public void Import_TakesPersonalDataEquipmentAndPurse()
    {
        var c = Import().Character;

        Assert.Equal(("Gerbelstein", "weiblich", "1,78 Schritt", "80 Stein", "braun", "Mittelländer"),
            (c.Personal.Family, c.Personal.Gender, c.Personal.Height, c.Personal.Weight, c.Personal.HairColor, c.Personal.AppearanceTable));
        Assert.Contains("Niederadel", c.Personal.Notes);
        Assert.Contains("Spricht im Schlaf.", c.Personal.Notes);
        Assert.Contains(c.Equipment, e => e.Id == "weapon.langschwert" && e.Name is null && !e.Paid);
        Assert.Contains(c.Equipment, e => e.Id == "" && e.Name == "Glücksbringer" && e.Price == 2);
        Assert.Equal(35m, Calculate(c).Equipment.Money);
    }

    [Fact]
    public void Export_WritesOptolithFormat()
    {
        var hero = JsonNode.Parse(OptolithExport.Write(Import().Character, Rules, Map).Json)!;

        Assert.Equal((3, 1300, "EL_3", "R_1", "C_8", "P_9", "f"),
            ((int)hero["phase"]!, (int)hero["ap"]!["total"]!, (string)hero["el"]!, (string)hero["r"]!, (string)hero["c"]!,
                (string)hero["p"]!, (string)hero["sex"]!));
        Assert.Equal(10, (int)hero["talents"]!["TAL_5"]!);
        Assert.Equal(12, (int)hero["ct"]!["CT_12"]!);
        Assert.Null(hero["ct"]!["CT_2"]);
        var languages = hero["activatable"]!["SA_29"]!.AsArray();
        Assert.Contains(languages, l => (int)l!["sid"]! == 8 && (int)l["tier"]! == 4);
        Assert.Equal(1, (int)hero["activatable"]!["DISADV_33"]![0]!["sid"]!);
        Assert.Equal(("TAL_5", 2), ((string)hero["activatable"]!["SA_9"]![0]!["sid"]!, (int)hero["activatable"]!["SA_9"]![0]!["sid2"]!));
        Assert.Equal(3, (int)hero["pers"]!["socialstatus"]!);
        Assert.Equal("Gareth", (string)hero["pers"]!["cultureAreaKnowledge"]!);
        Assert.Equal(("3", "5"), ((string)hero["belongings"]!["purse"]!["d"]!, (string)hero["belongings"]!["purse"]!["s"]!));
    }

    [Fact]
    public void ExportAndImport_KeepTheHero()
    {
        var first = Import().Character;
        var again = Import(OptolithExport.Write(first, Rules, Map).Json).Character;
        CharacterState a = Calculate(first), b = Calculate(again);

        Assert.Equal((a.Ap.Total, a.Ap.Spent), (b.Ap.Total, b.Ap.Spent));
        Assert.Equal(a.Attributes.Select(x => x.Value), b.Attributes.Select(x => x.Value));
        Assert.Equal(a.Skills.Select(x => x.Value), b.Skills.Select(x => x.Value));
        Assert.Equal(a.CombatTechniques.Select(x => x.Value), b.CombatTechniques.Select(x => x.Value));
        Assert.Equal(a.SpecialAbilities.Select(t => (t.Definition.Id, t.Entry.Option, t.Entry.Specification, t.Level)).Order(),
            b.SpecialAbilities.Select(t => (t.Definition.Id, t.Entry.Option, t.Entry.Specification, t.Level)).Order());
        Assert.Equal(a.Equipment.Money, b.Equipment.Money);
    }

    [Fact]
    public void SpellEnhancements_GoToOptolithAndBack()
    {
        var c = new Character { Name = "Magierin" };
        SourceSelection.Default(Rules.Books).ApplyTo(c, Rules.Books);
        c.Creation.Species = "species.mensch";
        Templates.ChangeCulture(c.Creation, "culture.mittelreicher", Rules);
        c.Creation.Traits.Add(new TraitEntry { Id = "adv.zauberer" });
        c.Creation.SpecialAbilities.Add(new TraitEntry { Id = "sf.tradition-gildenmagier" });
        c.Creation.Spells.Add(new SpellEntry { Id = "spell.ignifaxius", Value = 12, Enhancements = ["intensiver-strahl", "windender-strahl"] });

        var hero = JsonNode.Parse(OptolithExport.Write(c, Rules, Map).Json)!;
        var sids = hero["activatable"]!["SA_414"]!.AsArray().Select(x => (int)x!["sid"]!).ToList();
        Assert.Equal(2, sids.Count);
        Assert.All(sids, sid => Assert.Equal("spell.ignifaxius", Map.Enhancements["SA_414"][sid.ToString()].Spell));

        var again = Import(hero.ToJsonString()).Character;
        Assert.Equal(["intensiver-strahl", "windender-strahl"], again.Creation.Spells.Single(s => s.Id == "spell.ignifaxius").Enhancements.Order());
    }

    [Fact]
    public void Export_NewHeroInCreation()
    {
        var c = new Character { Name = "Neu" };
        Templates.ChangeCulture(c.Creation, "culture.mittelreicher", Rules);
        c.Creation.Species = "species.mensch";

        var hero = JsonNode.Parse(OptolithExport.Write(c, Rules, Map).Json)!;

        Assert.Equal((2, "P_0"), ((int)hero["phase"]!, (string)hero["p"]!));
        Assert.True(OptolithImport.IsOptolithHero(hero.ToJsonString()));
    }

    [Fact]
    public void Import_RejectsOtherFiles()
    {
        Assert.Throws<InvalidDataException>(() => Import("""{ "name": "x" }"""));
        Assert.Throws<InvalidDataException>(() => Import("nicht lesbar"));
    }
}
