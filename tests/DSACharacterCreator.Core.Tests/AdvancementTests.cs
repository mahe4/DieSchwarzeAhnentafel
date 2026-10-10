using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Persistence;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.Core.Tests;

/// <summary>Erschaffung abschließen, Steigerungsprotokoll, Korrektur der Erschaffung (Meilenstein 5).</summary>
public class AdvancementTests
{
    private static readonly RuleData Rules = TestRules.Data;
    private static readonly CharacterCalculator Calculator = new(Rules);

    /// <summary>Abgeschlossener Held mit <paramref name="leftover"/> übrigen AP (bis 10 verfällt nichts).</summary>
    private static Character FinishedHero(int leftover = 5)
    {
        var c = new Character();
        SourceSelection.Default(Rules.Books).ApplyTo(c, Rules.Books);
        c.Creation.Attributes["MU"] = 12;
        c.Creation.Attributes["KL"] = 12;
        c.Creation.SkillAdjustments["skill.klettern"] = 7;
        AdjustRemaining(c, leftover);
        Phases.FinishCreation(c, Calculator.Calculate(c), Rules);
        return c;
    }

    /// <summary>Verbraucht AP bis auf <paramref name="leftover"/> über einen Vorteil mit selbst eingetragenem AP-Wert.</summary>
    private static void AdjustRemaining(Character c, int leftover)
    {
        var manual = Rules.Advantages.First(a => a.Cost.Type == TraitCostType.Manual);
        var remaining = Calculator.Calculate(c).Ap.Remaining;
        c.Creation.Traits.Add(new TraitEntry { Id = manual.Id, ManualAp = remaining - leftover });
        Assert.Equal(leftover, Calculator.Calculate(c).Ap.Remaining);
    }

    private static void Change(Character c, Action<CreationLayer> change)
    {
        var state = Calculator.Calculate(c);
        var working = state.Advancement!.Working.Clone();
        change(working);
        Phases.Record(c, state, working, Rules);
    }

    private static int Skill(CharacterState s, string id) => s.Skills.First(x => x.Definition.Id == id).Value;

    [Fact]
    public void Finish_KeepsAtMostTenAp_RestIsForfeited()
    {
        var c = FinishedHero(leftover: 25);
        var state = Calculator.Calculate(c);

        Assert.Equal(CharacterPhase.Advancement, c.Phase);
        var finished = Assert.IsType<CreationFinishedEntry>(Assert.Single(c.Log));
        Assert.Equal(15, finished.ForfeitedAp);
        Assert.Equal(10, state.Ap.Remaining);
        Assert.Contains("15 übrige AP verfallen", state.Advancement!.Entries[0].Text);
    }

    [Fact]
    public void Raise_IsLogged_CostsByTable_WithoutCreationCaps()
    {
        var c = FinishedHero();
        Phases.AwardAp(c, 100, "Abenteuer 1");
        Change(c, w => w.SkillAdjustments["skill.klettern"] = w.SkillAdjustments.GetValueOrDefault("skill.klettern") + 2);
        Change(c, w => w.SkillAdjustments["skill.klettern"] += 1); // wird mit dem vorigen Eintrag zusammengefasst

        var state = Calculator.Calculate(c);
        var raise = Assert.IsType<RaiseEntry>(c.Log[^1]);
        Assert.Equal((7, 10), (raise.From, raise.To));
        Assert.Equal(10, Skill(state, "skill.klettern"));
        // Klettern (B): 7→8, 8→9, 9→10 kosten je 2 AP
        Assert.Equal(6, state.Advancement!.Entries[^1].Ap);
        Assert.Equal(5 + 100 - 6, state.Ap.Remaining);
        // Erschaffungsgrenze (erfahren: FW 10) gilt nicht mehr, nur höchste Eigenschaft + 2
        Change(c, w => w.SkillAdjustments["skill.klettern"] += 1);
        state = Calculator.Calculate(c);
        Assert.Equal(11, Skill(state, "skill.klettern"));
        Assert.DoesNotContain(state.Issues, i => i.Code == "skill.creationMax");
    }

    [Fact]
    public void Lowering_OnlyUndoesOwnRaises()
    {
        var c = FinishedHero();
        Phases.AwardAp(c, 100, null);
        Change(c, w => w.Attributes["MU"] = 13);
        Change(c, w => w.Attributes["MU"] = 11); // unter den Stand der Erschaffung (12) geht es nicht

        var state = Calculator.Calculate(c);
        Assert.Equal(12, state.Attribute("MU"));
        Assert.DoesNotContain(c.Log, e => e is RaiseEntry);
        Assert.Equal(12, state.Attributes.First(a => a.Definition.Id == "MU").Min);
    }

    [Fact]
    public void SpecialAbility_Acquired_AndRemovedAgain()
    {
        var c = FinishedHero();
        Phases.AwardAp(c, 100, null);
        Change(c, w => w.SpecialAbilities.Add(new TraitEntry { Id = "sf.kampfreflexe", Level = 1 }));
        var state = Calculator.Calculate(c);
        Assert.IsType<AcquireEntry>(c.Log[^1]);
        Assert.Equal(10, state.Advancement!.Entries[^1].Ap);
        Assert.Contains(state.SpecialAbilities, s => s.Definition.Id == "sf.kampfreflexe" && s.Entry.LogEntryId == c.Log[^1].Id);

        // Stufe des erworbenen Eintrags ändert den Protokolleintrag selbst
        Change(c, w => w.SpecialAbilities.Single(s => s.Id == "sf.kampfreflexe").Level = 2);
        Assert.Equal(2, ((AcquireEntry)c.Log[^1]).Entry.Level);

        Change(c, w => w.SpecialAbilities.RemoveAll(s => s.Id == "sf.kampfreflexe"));
        Assert.DoesNotContain(c.Log, e => e is AcquireEntry);
    }

    [Fact]
    public void Disadvantage_BoughtOff_CostsItsValue()
    {
        var c = FinishedHero();
        c.Phase = CharacterPhase.Creation;
        c.Log.Clear();
        c.Creation.Traits.Add(new TraitEntry { Id = "disadv.pech", Level = 1 });
        Phases.FinishCreation(c, Calculator.Calculate(c), Rules);
        Phases.AwardAp(c, 100, null);
        var before = Calculator.Calculate(c);
        Assert.True(Phases.NeedsGmPermission(before.Advancement!.Working,
            WithChange(before, w => w.Traits.RemoveAll(t => t.Id == "disadv.pech"))));

        Change(c, w => w.Traits.RemoveAll(t => t.Id == "disadv.pech"));
        var state = Calculator.Calculate(c);
        Assert.IsType<RemoveEntry>(c.Log[^1]);
        Assert.Equal(20, state.Advancement!.Entries[^1].Ap);
        Assert.DoesNotContain(state.Traits, t => t.Definition.Id == "disadv.pech");
    }

    private static CreationLayer WithChange(CharacterState state, Action<CreationLayer> change)
    {
        var w = state.Advancement!.Working.Clone();
        change(w);
        return w;
    }

    [Fact]
    public void Energies_BoughtAfterCreation_TableD_MaxKo()
    {
        var c = FinishedHero();
        Phases.AwardAp(c, 200, null);
        Change(c, w => w.BoughtEnergies[Energies.LifePoints] = 3);
        var state = Calculator.Calculate(c);
        Assert.Equal(12, state.Ap.Energies); // Spalte D: 4 AP je Punkt bis 12
        Assert.Equal(3, state.Energies.First(e => e.Id == Energies.LifePoints).Value);

        Change(c, w => w.BoughtEnergies[Energies.LifePoints] = 9); // KO 8
        Assert.Contains(Calculator.Calculate(c).Issues, i => i.Code == "energy.max");
        Change(c, w => w.BoughtEnergies[Energies.Astral] = 1);
        Assert.Contains(Calculator.Calculate(c).Issues, i => i.Code == "energy.unavailable");
    }

    [Fact]
    public void Correction_ShowsShiftedEntries_AndCanBeResolvedOrDiscarded()
    {
        var c = FinishedHero();
        Phases.AwardAp(c, 100, null);
        Change(c, w => w.SkillAdjustments["skill.klettern"] += 2); // 7 → 9

        Phases.StartCorrection(c);
        Assert.Equal(CharacterPhase.Creation, c.Phase);
        c.Creation.SkillAdjustments["skill.klettern"] = 8; // in der Erschaffung schon 8
        var state = Calculator.Calculate(c);
        Assert.Equal(8, Skill(state, "skill.klettern")); // angezeigt wird der Erschaffungsstand
        var conflict = state.Advancement!.Entries.Single(e => e.Entry is RaiseEntry);
        Assert.Equal(LogConflict.Shifted, conflict.Conflict);
        Assert.Equal(8, conflict.CurrentValue);
        Assert.Contains(state.Issues, i => i.Code == "log.conflict");
        Assert.Equal(10, Skill(state.Advancement.Final, "skill.klettern")); // Vorschau: um zwei Stufen verschoben

        // Begrenzen: Ziel bleibt 9
        Phases.CapEntry(c, conflict.Entry.Id, 8);
        state = Calculator.Calculate(c);
        Assert.False(state.Advancement!.HasUnresolvedConflicts);
        Assert.Equal(9, Skill(state.Advancement.Final, "skill.klettern"));

        Phases.FinishCreation(c, state, Rules);
        Assert.Equal(CharacterPhase.Advancement, c.Phase);
        Assert.Null(c.CreationSnapshot);
        Assert.Equal("Erschaffung korrigiert", Assert.IsType<NoteEntry>(c.Log[^1]).Text);
        Assert.Single(c.Log.OfType<CreationFinishedEntry>());
    }

    [Fact]
    public void Correction_Discard_RestoresEverything()
    {
        var c = FinishedHero();
        Phases.AwardAp(c, 100, null);
        Change(c, w => w.Attributes["MU"] = 13);
        var log = CharacterSerializer.Serialize(c);

        Phases.StartCorrection(c);
        c.Creation.Attributes["MU"] = 14;
        Assert.Equal(LogConflict.Shifted, Calculator.Calculate(c).Advancement!.Entries.Single(e => e.Entry is RaiseEntry).Conflict);
        Phases.DiscardCorrection(c);

        Assert.Equal(CharacterPhase.Advancement, c.Phase);
        Assert.Equal(12, c.Creation.Attributes["MU"]);
        Assert.Equal(13, Calculator.Calculate(c).Attribute("MU"));
        Assert.Equal(log, CharacterSerializer.Serialize(c));
    }

    [Fact]
    public void Correction_AcquiredInCreation_MakesLogEntryInvalid()
    {
        var c = FinishedHero();
        Phases.AwardAp(c, 100, null);
        Change(c, w => w.SpecialAbilities.Add(new TraitEntry { Id = "sf.kampfreflexe", Level = 1 }));
        Phases.StartCorrection(c);
        c.Creation.SpecialAbilities.Add(new TraitEntry { Id = "sf.kampfreflexe", Level = 1 });

        var entry = Calculator.Calculate(c).Advancement!.Entries.Single(e => e.Entry is AcquireEntry);
        Assert.Equal(LogConflict.Invalid, entry.Conflict);
        Phases.DeleteEntry(c, entry.Entry.Id);
        Assert.False(Calculator.Calculate(c).Advancement!.HasUnresolvedConflicts);
    }

    [Fact]
    public void Log_SurvivesSaveAndLoad()
    {
        var c = FinishedHero();
        Phases.AwardAp(c, 50, "Test");
        Change(c, w => w.SpecialAbilities.Add(new TraitEntry { Id = "sf.kampfreflexe", Level = 1 }));
        Change(c, w => w.Attributes["MU"] = 13);
        Phases.StartCorrection(c);

        var loaded = CharacterSerializer.Deserialize(CharacterSerializer.Serialize(c));
        Assert.Equal(c.Log.Select(e => e.GetType()), loaded.Log.Select(e => e.GetType()));
        Assert.NotNull(loaded.CreationSnapshot);
        Assert.Equal(4, loaded.CreationSnapshot!.Log.Count);
        Assert.Equal(Calculator.Calculate(c).Advancement!.Final.Ap, Calculator.Calculate(loaded).Advancement!.Final.Ap);
    }

    [Fact]
    public void ReplayCache_GivesSameResultsAsFreshCalculation()
    {
        var calculator = new CharacterCalculator(Rules); // ein Rechner mit Zwischenspeicher, wie im Editor
        var c = FinishedHero();
        Phases.AwardAp(c, 300, "A");
        void Step(Action<CreationLayer> change)
        {
            var state = calculator.Calculate(c);
            var working = state.Advancement!.Working.Clone();
            change(working);
            Phases.Record(c, state, working, Rules);
        }
        Step(w => w.Attributes["MU"] = 13);
        Step(w => w.SkillAdjustments["skill.klettern"] += 2);
        Step(w => w.SpecialAbilities.Add(new TraitEntry { Id = "sf.kampfreflexe", Level = 1 }));
        Step(w => w.SkillAdjustments["skill.klettern"] -= 1); // ändert einen älteren Eintrag
        Step(w => w.Attributes["MU"] = 14);
        c.Log.RemoveAt(1); // Gutschrift gelöscht: alles danach ändert sich

        void Same()
        {
            var cached = calculator.Calculate(c);
            var fresh = new CharacterCalculator(Rules).Calculate(c);
            Assert.Equal(fresh.Ap, cached.Ap);
            Assert.Equal(fresh.Issues.Select(i => i.Message), cached.Issues.Select(i => i.Message));
            Assert.Equal(fresh.Advancement!.Entries.Select(e => (e.Text, e.Ap, e.Conflict)),
                cached.Advancement!.Entries.Select(e => (e.Text, e.Ap, e.Conflict)));
        }
        Same();
        Phases.StartCorrection(c);
        c.Creation.SkillAdjustments["skill.klettern"] = 8;
        Same();
        Phases.CapEntry(c, c.Log.OfType<RaiseEntry>().First(r => r.Target == "skill.klettern").Id, 8);
        Same();
        Phases.DiscardCorrection(c);
        Same();
    }
}
