using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.Core.Tests;

public class CharacterCalculatorTests
{
    private static readonly CharacterCalculator Calculator = new(TestRules.Data);

    private static Character NewCharacter(Action<CreationLayer>? setup = null)
    {
        var c = new Character();
        SourceSelection.Default(TestRules.Data.Books).ApplyTo(c, TestRules.Data.Books);
        setup?.Invoke(c.Creation);
        return c;
    }

    [Fact]
    public void NewCharacter_StartsWithDefaults()
    {
        var state = Calculator.Calculate(NewCharacter());

        Assert.Equal("erfahren", state.ExperienceLevel.Id);
        Assert.Equal(1100, state.Ap.Total);
        Assert.Equal(0, state.Ap.Spent);
        Assert.All(state.Attributes, a => Assert.Equal(8, a.Value));
        Assert.All(state.Skills, s => Assert.Equal(0, s.Value));
        Assert.All(state.CombatTechniques, c => Assert.Equal(6, c.Value));
        Assert.Equal(59, state.Skills.Count);
        Assert.Equal(3, state.Derived.FatePoints);
        Assert.Null(state.Derived.LifePoints);
        Assert.Null(state.Origin.Species);
    }

    [Fact]
    public void Costs_AreSummedPerCategory()
    {
        var state = Calculator.Calculate(NewCharacter(c =>
        {
            c.Attributes["MU"] = 14;            // 6 × 15
            c.SkillAdjustments["skill.gassenwissen"] = 9; // C: 9 × 3
            c.CombatTechniqueAdjustments["ct.dolche"] = 2; // = 8 // B: 2 × 2
        }));

        Assert.Equal(0, state.Ap.Species);
        Assert.Equal(90, state.Ap.Attributes);
        Assert.Equal(27, state.Ap.Skills);
        Assert.Equal(4, state.Ap.CombatTechniques);
        Assert.Equal(1100 - 121, state.Ap.Remaining);
    }

    [Fact]
    public void CombatValues_MatchWikiExamples()
    {
        // Louisa: Raufen 6, MU 14, GE 14 → AT 8, PA 5
        var louisa = Calculator.Calculate(NewCharacter(c =>
        {
            c.Attributes["MU"] = 14;
            c.Attributes["GE"] = 14;
        }));
        var raufen = louisa.CombatTechniques.Single(t => t.Definition.Id == "ct.raufen");
        Assert.Equal(8, raufen.Attack);
        Assert.Equal(5, raufen.Parry);

        // Chris: Schwerter 12, MU 15, GE/KK 12 → AT 14, PA 7 (Kompetent, damit KtW 12 erlaubt bleibt)
        var chris = Calculator.Calculate(NewCharacter(c =>
        {
            c.Attributes["MU"] = 15;
            c.Attributes["GE"] = 12;
            c.Attributes["KK"] = 12;
            c.CombatTechniqueAdjustments["ct.schwerter"] = 6; // = 12
        }));
        var schwerter = chris.CombatTechniques.Single(t => t.Definition.Id == "ct.schwerter");
        Assert.Equal(14, schwerter.Attack);
        Assert.Equal(7, schwerter.Parry);

        // Sarah: Bögen 12, FF 14 → FK 14
        var sarah = Calculator.Calculate(NewCharacter(c =>
        {
            c.Attributes["FF"] = 14;
            c.CombatTechniqueAdjustments["ct.boegen"] = 6; // = 12
        }));
        var boegen = sarah.CombatTechniques.Single(t => t.Definition.Id == "ct.boegen");
        Assert.Equal(14, boegen.RangedAttack);
        Assert.Null(boegen.Attack);
        Assert.Null(boegen.Parry);
    }

    [Fact]
    public void DerivedValues_RoundHalfUp()
    {
        // Sarah: GE 15 → AW 8; MU 12 + GE 15 → INI 14
        var state = Calculator.Calculate(NewCharacter(c =>
        {
            c.Attributes["MU"] = 12;
            c.Attributes["GE"] = 15;
        }));

        Assert.Equal(8, state.Derived.Dodge);
        Assert.Equal(14, state.Derived.Initiative);
    }

    [Fact]
    public void CreationLimits_DependOnExperienceLevel()
    {
        var state = Calculator.Calculate(NewCharacter(c =>
        {
            c.ExperienceLevel = "unerfahren";
            c.Attributes["KL"] = 13;
            c.CombatTechniqueAdjustments["ct.dolche"] = 3; // = 9
        }));

        Assert.Equal(900, state.Ap.Total);
        Assert.Contains(state.Issues, i => i.Code == "attribute.creationMax" && i.TargetId == "KL");
        Assert.Contains(state.Issues, i => i.Code == "ct.creationMax" && i.TargetId == "ct.dolche");
    }

    [Fact]
    public void SkillAboveCheckAttributes_IsError_EvenWhenLevelAllowsIt()
    {
        // Kompetent erlaubt FW 13, Klettern 11 liegt aber über MU/GE/KK 8 + 2.
        var state = Calculator.Calculate(NewCharacter(c =>
        {
            c.ExperienceLevel = "kompetent";
            c.SkillAdjustments["skill.klettern"] = 11;
        }));

        Assert.DoesNotContain(state.Issues, i => i.Code == "skill.creationMax");
        Assert.Contains(state.Issues, i => i.Code == "skill.attributeMax" && i.TargetId == "skill.klettern");
    }

    [Fact]
    public void SkillMax_IsHighestCheckAttributePlusTwo_CappedByLevel()
    {
        var state = Calculator.Calculate(NewCharacter(c => c.Attributes["KK"] = 9));

        var klettern = state.Skills.Single(s => s.Definition.Id == "skill.klettern"); // MU/GE/KK
        Assert.Equal(10, klettern.Max); // min(9 + 2 = 11, Erfahren 10)
    }

    [Fact]
    public void OutstandingSkillAndCombatTechnique_RaiseMaxima()
    {
        // Herausragende Fertigkeit (Klettern) zweimal: Erfahren 10 → 12, KK 12 + 2 → 16
        var state = Calculator.Calculate(NewCharacter(c =>
        {
            c.Attributes["KK"] = 12;
            c.Traits.Add(new TraitEntry { Id = "adv.herausragende-fertigkeit", Option = "skill.klettern" });
            c.Traits.Add(new TraitEntry { Id = "adv.herausragende-fertigkeit", Option = "skill.klettern" });
            c.Traits.Add(new TraitEntry { Id = "adv.herausragende-kampftechnik", Option = "ct.schwerter" });
            c.SkillAdjustments["skill.klettern"] = 12;
            c.CombatTechniqueAdjustments["ct.schwerter"] = 7; // 13 > Erfahren 12
        }));
        Assert.Equal(12, state.Skills.Single(s => s.Definition.Id == "skill.klettern").Max);
        Assert.DoesNotContain(state.Issues, i => i.Code == "skill.creationMax");
        Assert.Equal(13, state.CombatTechniques.Single(c => c.Definition.Id == "ct.schwerter").Max);
        Assert.DoesNotContain(state.Issues, i => i.Code == "ct.creationMax");
    }

    [Fact]
    public void AttributeTotal_IsLimited()
    {
        var state = Calculator.Calculate(NewCharacter(c =>
        {
            foreach (var a in new[] { "MU", "KL", "IN", "CH", "FF", "GE", "KO" }) c.Attributes[a] = 13;
            c.Attributes["KK"] = 12;
        }));

        Assert.Equal(103, state.Attributes.Sum(a => a.Value));
        Assert.Contains(state.Issues, i => i.Code == "attribute.creationTotal");
    }

    [Fact]
    public void Overspending_IsError_LeftoverIsInfo()
    {
        var overspent = Calculator.Calculate(NewCharacter(c =>
        {
            c.ExperienceLevel = "unerfahren";
            foreach (var a in new[] { "MU", "KL", "IN", "CH", "FF", "GE", "KO", "KK" }) c.Attributes[a] = 12;
            foreach (var skill in TestRules.Data.Skills) c.SkillAdjustments[skill.Id] = 10;
        }));
        Assert.True(overspent.Ap.Remaining < 0);
        Assert.Contains(overspent.Issues, i => i is { Code: "ap.overspent", Severity: IssueSeverity.Error });

        var fresh = Calculator.Calculate(NewCharacter());
        Assert.Contains(fresh.Issues, i => i is { Code: "ap.leftover", Severity: IssueSeverity.Info });
    }

    [Fact]
    public void AfterCreation_LevelLimitsNoLongerApply()
    {
        var character = NewCharacter(c =>
        {
            c.Attributes["MU"] = 16;
            c.Attributes["GE"] = 16;
            c.SkillAdjustments["skill.klettern"] = 13;
        });
        character.Phase = CharacterPhase.Advancement;

        var state = Calculator.Calculate(character);

        Assert.DoesNotContain(state.Issues, i => i.Scope == RuleScope.CreationOnly);
        Assert.Null(state.Attributes.Single(a => a.Definition.Id == "MU").Max);
        Assert.Equal(18, state.Skills.Single(s => s.Definition.Id == "skill.klettern").Max);
    }

    [Fact]
    public void ValuesFromDisabledSources_AreWarned()
    {
        var character = NewCharacter(c => c.CombatTechniqueAdjustments["ct.peitschen"] = 2);
        new SourceSelection(["grw"]).ApplyTo(character, TestRules.Data.Books);

        var state = Calculator.Calculate(character);

        var peitschen = state.CombatTechniques.Single(t => t.Definition.Id == "ct.peitschen");
        Assert.False(peitschen.Available);
        Assert.Contains(state.Issues, i => i.Code == "source.disabled" && i.TargetId == "ct.peitschen");
    }

    [Fact]
    public void Whips_AttackWithDexterity_AndCannotParry()
    {
        var state = Calculator.Calculate(NewCharacter(c =>
        {
            c.Attributes["FF"] = 14;
            c.Attributes["MU"] = 8;
        }));

        var peitschen = state.CombatTechniques.Single(t => t.Definition.Id == "ct.peitschen");
        Assert.Equal(8, peitschen.Attack); // 6 + (14 − 8) / 3
        Assert.Null(peitschen.Parry);
    }

    // ---- Herkunft ----

    [Fact]
    public void Species_AddsApAndBaseValues_LouisaExample()
    {
        // Louisa: Mensch, MU 14, KL 12, IN 14, GE 14, KO 10, KK 9 → LeP 25, SK 2, ZK 0, AW 7, INI 14, GS 8
        var state = Calculator.Calculate(NewCharacter(c =>
        {
            c.Species = "species.mensch";
            c.SpeciesAttributeChoices = ["MU"];
            c.Attributes["MU"] = 14;
            c.Attributes["KL"] = 12;
            c.Attributes["IN"] = 14;
            c.Attributes["GE"] = 14;
            c.Attributes["KO"] = 10;
            c.Attributes["KK"] = 9;
        }));

        Assert.Equal(14, state.Attribute("MU"));
        Assert.Equal((25, 2, 0, 8), (state.Derived.LifePoints, state.Derived.Spirit, state.Derived.Toughness, state.Derived.Speed));
        Assert.Equal(7, state.Derived.Dodge);
        Assert.Equal(14, state.Derived.Initiative);
        Assert.Equal(0, state.Ap.Species);
    }

    [Fact]
    public void SpeciesModifiers_ShiftOnlyTheMaximum()
    {
        var state = Calculator.Calculate(NewCharacter(c =>
        {
            c.Species = "species.zwerg";           // KO und KK +1; CH oder GE −2
            c.SpeciesAttributeChoices = ["GE"];
            c.Attributes["KK"] = 15;               // Zwerg darf KK bis 15 (Erfahren 14 + 1)
            c.Attributes["GE"] = 13;               // aber GE nur bis 12
        }));

        var kk = state.Attributes.Single(a => a.Definition.Id == "KK");
        Assert.Equal((15, 1, 15), (kk.Value, kk.Modifier, kk.Max));
        var ge = state.Attributes.Single(a => a.Definition.Id == "GE");
        Assert.Equal((13, 8, 12), (ge.Value, ge.Min, ge.Max));
        Assert.DoesNotContain(state.Issues, i => i.Code == "attribute.creationMax" && i.TargetId == "KK");
        Assert.Contains(state.Issues, i => i.Code == "attribute.creationMax" && i.TargetId == "GE");
        Assert.Equal(61, state.Ap.Species);
        // Kein geschenkter Punkt: Werte und Summe bleiben, wie gekauft
        Assert.Equal(8, state.Attribute("KO"));
        Assert.Equal(8 * 6 + 15 + 13, state.Attributes.Sum(a => a.Value));
    }

    [Fact]
    public void MissingSpeciesChoice_IsWarned()
    {
        var state = Calculator.Calculate(NewCharacter(c => c.Species = "species.elf"));

        Assert.Contains(state.Issues, i => i.Code == "species.attributeChoice");
        Assert.Equal(15, state.Attributes.Single(a => a.Definition.Id == "IN").Max); // Erfahren 14 + 1; feste Änderungen gelten trotzdem
    }

    [Fact]
    public void CulturePackage_AddsSkillsAndAp_AndCanBeDeclined()
    {
        var withPackage = Calculator.Calculate(NewCharacter(c => c.Culture = "culture.andergaster"));
        Assert.Equal(20, withPackage.Ap.Spent);
        Assert.Equal(20, withPackage.CulturePackageAp);
        var holz = withPackage.Skills.Single(s => s.Definition.Id == "skill.holzbearbeitung");
        Assert.Equal((2, 2, 4), (holz.Value, holz.PackageValue, holz.ApSpent)); // B: 2 × 2 AP, wie selbst gesteigert

        var without = Calculator.Calculate(NewCharacter(c =>
        {
            c.Culture = "culture.andergaster";
            c.CulturePackage = false;
        }));
        Assert.Equal(0, without.Ap.Spent);
        Assert.Equal(0, without.Skills.Single(s => s.Definition.Id == "skill.holzbearbeitung").Value);
    }

    [Fact]
    public void ProfessionPackage_SetsValues_CultureBonusAddsOnTop()
    {
        var state = Calculator.Calculate(NewCharacter(c =>
        {
            c.Culture = "culture.mittelreicher";       // Holzbearbeitung +1 …
            c.Profession = "profession.streuner";      // Gassenwissen 7, Dolche 10, Raufen 12, Armbrüste/Wurfwaffen 10
            c.ProfessionCombatTechniqueChoices = ["ct.wurfwaffen"];
        }));

        Assert.Equal(12 + 212 - 3 - 6, state.Ap.Spent); // ohne SF der Vorlage und Sprachen-Budget
        Assert.Equal(7, state.Skills.Single(s => s.Definition.Id == "skill.gassenwissen").Value);
        Assert.Equal(1, state.Skills.Single(s => s.Definition.Id == "skill.holzbearbeitung").Value);
        Assert.Equal(10, state.CombatTechniques.Single(t => t.Definition.Id == "ct.wurfwaffen").Value);
        Assert.Equal(6, state.CombatTechniques.Single(t => t.Definition.Id == "ct.armbrueste").Value);
        Assert.Equal(12, state.CombatTechniques.Single(t => t.Definition.Id == "ct.raufen").Value);
        Assert.Equal(12 + 203, state.Ap.Skills + state.Ap.CombatTechniques + state.Ap.Unmodeled);
        Assert.DoesNotContain(state.Issues, i => i.Code == "profession.combatChoice");
    }

    [Fact]
    public void ProfessionVariant_ReplacesValuesAndAp()
    {
        var state = Calculator.Calculate(NewCharacter(c =>
        {
            c.Profession = "profession.streuner";
            c.ProfessionVariant = "einbrecher-in";     // Klettern 7 statt 3 … (Wiki: 221 AP)
            c.ProfessionCombatTechniqueChoices = ["ct.wurfwaffen"];
        }));

        // Die Vorlage kostet 225 AP (so auch im Wiki-Rechenbeispiel Schritt 6a); die Variantenseite nennt 221 AP.
        // Ausgegeben sind hier 216: ohne die SF der Vorlage (nur über Templates) und ohne das Sprachen-Budget (6 AP).
        Assert.Equal(225, state.ProfessionCost!.Total);
        Assert.Equal(216, state.Ap.Spent);
        Assert.Contains(state.Issues, i => i.Code == "wiki.professionAp");
        Assert.Equal(7, state.Skills.Single(s => s.Definition.Id == "skill.klettern").Value);
    }

    [Fact]
    public void TemplateValues_CanBeRaisedAndLowered_LoweringRefunds()
    {
        var state = Calculator.Calculate(NewCharacter(c =>
        {
            c.Profession = "profession.streuner";
            c.SkillAdjustments["skill.gassenwissen"] = 1;   // 7 → 8: C = 3 AP
            c.SkillAdjustments["skill.verbergen"] = -2;     // 5 → 3: C, 2 × 3 = −6 AP
        }));

        Assert.Equal(8, state.Skills.Single(s => s.Definition.Id == "skill.gassenwissen").Value);
        Assert.Equal(3, state.Skills.Single(s => s.Definition.Id == "skill.verbergen").Value);
        // Gegenüber der Vorlage: +3 AP für Gassenwissen, −6 AP für Verbergen.
        var template = Calculator.Calculate(NewCharacter(c => c.Profession = "profession.streuner"));
        Assert.Equal(3 - 6, state.Ap.Spent - template.Ap.Spent);
    }

    [Fact]
    public void ProfessionAttributeRequirements_AreChecked()
    {
        var state = Calculator.Calculate(NewCharacter(c => c.Profession = "profession.balihoer-krieger"));

        Assert.Contains(state.Issues, i => i is { Code: "profession.attribute", TargetId: "MU", Severity: IssueSeverity.Warning });
    }

    [Fact]
    public void CulturePackage_CostsWhatItContains_NotTheWikiValue()
    {
        // Zahori: Wiki nennt 27 AP, die Talente kosten 28 AP.
        var state = Calculator.Calculate(NewCharacter(c => c.Culture = "culture.zahori"));

        Assert.Equal(28, state.Ap.Spent);
        Assert.Contains(state.Issues, i => i.Code == "wiki.cultureAp");
    }

    [Fact]
    public void ProfessionCost_IsBrokenDown()
    {
        var state = Calculator.Calculate(NewCharacter(c => c.Profession = "profession.streuner"));

        // Streuner: Talente + Kampftechniken 203, Fertigkeitsspezialisierung Gassenwissen (C) 3, Sprachen 6 = 212 = Wiki
        var cost = state.ProfessionCost!;
        Assert.Equal(212, cost.WikiAp);
        Assert.Equal((203, 3, 6, 0), (cost.Skills + cost.CombatTechniques, cost.SpecialAbilities, cost.Languages, cost.Unmodeled));
        Assert.Equal(212, cost.Total);
        Assert.DoesNotContain(state.Issues, i => i.Code == "wiki.professionAp");
    }

    [Fact]
    public void TemplateValues_CostTheSameAsSelfBoughtValues()
    {
        var fromTemplate = Calculator.Calculate(NewCharacter(c => c.Culture = "culture.andergaster")); // Holzbearbeitung +2 …
        var bought = Calculator.Calculate(NewCharacter(c =>
        {
            c.SkillAdjustments["skill.holzbearbeitung"] = 2;
            c.SkillAdjustments["skill.orientierung"] = 1;
            c.SkillAdjustments["skill.pflanzenkunde"] = 1;
            c.SkillAdjustments["skill.sagen-legenden"] = 1;
            c.SkillAdjustments["skill.tierkunde"] = 1;
            c.SkillAdjustments["skill.wildnisleben"] = 2;
        }));

        Assert.Equal(bought.Ap.Spent, fromTemplate.Ap.Spent);
        Assert.Equal(bought.Skills.Select(s => s.Value), fromTemplate.Skills.Select(s => s.Value));
    }

    [Fact]
    public void TemplateSkill_CanBeLoweredToZero()
    {
        var state = Calculator.Calculate(NewCharacter(c =>
        {
            c.Profession = "profession.streuner";
            c.SkillAdjustments["skill.gassenwissen"] = -7;
        }));

        var gassenwissen = state.Skills.Single(s => s.Definition.Id == "skill.gassenwissen");
        Assert.Equal((0, 0), (gassenwissen.Value, gassenwissen.ApSpent));
    }

    [Fact]
    public void ChangingProfession_ResetsEverythingFromTheOldTemplate()
    {
        var c = NewCharacter(x =>
        {
            x.Attributes["MU"] = 12;
            x.Culture = "culture.mittelreicher";
            x.Profession = "profession.streuner";
            x.ProfessionVariant = "einbrecher-in";
            x.ProfessionCombatTechniqueChoices = ["ct.wurfwaffen"];
            x.SkillAdjustments["skill.gassenwissen"] = 2;
            x.CombatTechniqueAdjustments["ct.dolche"] = 1;
        });

        c.Creation.SetProfession("profession.kriegerin");

        Assert.Equal("profession.kriegerin", c.Creation.Profession);
        Assert.Null(c.Creation.ProfessionVariant);
        Assert.Empty(c.Creation.ProfessionCombatTechniqueChoices);
        Assert.Empty(c.Creation.SkillAdjustments);
        Assert.Empty(c.Creation.CombatTechniqueAdjustments);
        Assert.Equal(12, c.Creation.Attributes["MU"]);
        Assert.Equal("culture.mittelreicher", c.Creation.Culture);
    }

    [Fact]
    public void ChoosingSameProfessionAgain_ChangesNothing()
    {
        var c = NewCharacter(x =>
        {
            x.Profession = "profession.streuner";
            x.SkillAdjustments["skill.gassenwissen"] = 2;
        });

        c.Creation.SetProfession("profession.streuner");

        Assert.Equal(2, c.Creation.SkillAdjustments["skill.gassenwissen"]);
    }

    [Theory]
    [InlineData("culture")]
    [InlineData("package")]
    [InlineData("variant")]
    public void ChangingAnyTemplate_ResetsAdjustments(string change)
    {
        var c = NewCharacter(x =>
        {
            x.Attributes["MU"] = 12;
            x.Culture = "culture.mittelreicher";
            x.Profession = "profession.streuner";
            x.SkillAdjustments["skill.gassenwissen"] = 2;
            x.CombatTechniqueAdjustments["ct.dolche"] = -1;
        });

        switch (change)
        {
            case "culture": c.Creation.SetCulture("culture.horasier"); break;
            case "package": c.Creation.SetCulturePackage(false); break;
            case "variant": c.Creation.SetProfessionVariant("dieb-in"); break;
        }

        Assert.False(c.Creation.HasValueAdjustments);
        Assert.Equal(12, c.Creation.Attributes["MU"]);
        Assert.Equal("profession.streuner", c.Creation.Profession);
    }

    [Fact]
    public void Preselection_CombinesCultureAndProfession()
    {
        // Horasier: Rechnen +2, Etikette +2 …; Streuner: Rechnen 4
        var state = Calculator.Calculate(NewCharacter(x =>
        {
            x.Culture = "culture.horasier";
            x.Profession = "profession.streuner";
        }));

        var rechnen = state.Skills.Single(s => s.Definition.Id == "skill.rechnen");
        Assert.Equal((6, 6), (rechnen.Value, rechnen.PackageValue));
    }

    [Theory]
    [InlineData("erfahren", 10, 380)]
    [InlineData("kompetent", 12, 480)]
    [InlineData("meisterlich", 12, 680)]
    [InlineData("legendaer", 12, 680)]     // nächstliegender Wertesatz
    [InlineData("unerfahren", 10, 380)]
    public void ProfessionValues_FollowExperienceLevel(string level, int daggers, int wikiAp)
    {
        // Elementarist (erfahren/kompetent/meisterlich): Dolche 10/12/12, AP 380/480/680
        var state = Calculator.Calculate(NewCharacter(c =>
        {
            c.ExperienceLevel = level;
            c.Profession = "profession.elementarist";
        }));

        Assert.Equal(daggers, state.CombatTechniques.Single(t => t.Definition.Id == "ct.dolche").Value);
        Assert.Equal(wikiAp, state.ProfessionCost!.WikiAp);
        Assert.DoesNotContain(state.Issues, i => i.Code == "profession.unparsed");
    }

    [Fact]
    public void ProfessionRequirements_FollowExperienceLevel()
    {
        // Elementarist: kompetent zusätzlich MU 15
        var state = Calculator.Calculate(NewCharacter(c =>
        {
            c.ExperienceLevel = "kompetent";
            c.Profession = "profession.elementarist";
            c.Attributes["MU"] = 14;
        }));

        Assert.Contains(state.Issues, i => i.Code == "profession.attribute" && i.Message.Contains("MU 15"));
    }

    [Fact]
    public void ProfessionTemplate_AddsItsSpecialAbilities_AndReplacesThemOnChange()
    {
        var c = NewCharacter(_ => { });
        Templates.ChangeProfession(c.Creation, "profession.streuner", TestRules.Data);
        c.Creation.SpecialAbilities.Add(new TraitEntry { Id = "sf.ortskenntnis", Specification = "Gareth" }); // selbst hinzugefügt

        var spec = Assert.Single(c.Creation.SpecialAbilities, e => e.Source == Templates.ProfessionSource);
        Assert.Equal(("sf.fertigkeitsspezialisierung-talente", "skill.gassenwissen"), (spec.Id, spec.Option));
        Assert.Equal(3, Calculator.Calculate(c).Ap.SpecialAbilities - 2); // + Ortskenntnis 2

        Templates.ChangeProfession(c.Creation, "profession.kriegerin", TestRules.Data);
        Assert.DoesNotContain(c.Creation.SpecialAbilities, e => e.Option == "skill.gassenwissen");
        Assert.Contains(c.Creation.SpecialAbilities, e => e.Specification == "Gareth");
    }

    [Fact]
    public void CultureTemplate_GivesFreeMotherTongueAndLocalKnowledge()
    {
        var c = NewCharacter(_ => { });
        Templates.ChangeCulture(c.Creation, "culture.mittelreicher", TestRules.Data);

        var language = Assert.Single(c.Creation.SpecialAbilities, e => e.Id == "sf.sprache");
        Assert.Equal(("language.garethi", 3, true), (language.Option, language.Level, language.Free));
        Assert.Contains(c.Creation.SpecialAbilities, e => e.Id == "sf.ortskenntnis" && e.Free);
        Assert.Equal(0, Calculator.Calculate(c).Ap.SpecialAbilities);

        Templates.ChangeCulture(c.Creation, "culture.thorwaler", TestRules.Data);
        Assert.Equal("language.thorwalsch", Assert.Single(c.Creation.SpecialAbilities, e => e.Id == "sf.sprache").Option);
    }

    [Fact]
    public void CompleteProfession_HasNoWikiRest_MismatchIsReported()
    {
        // Landwehrsoldat: ohne Zauber/Liturgien vollständig strukturiert, Wiki-Wert passt nicht zum Inhalt
        var state = Calculator.Calculate(NewCharacter(c => c.Profession = "profession.landwehrsoldat"));

        Assert.Equal(0, state.ProfessionCost!.Unmodeled);
        Assert.Contains(state.Issues, i => i.Code == "wiki.professionAp");
    }
}
