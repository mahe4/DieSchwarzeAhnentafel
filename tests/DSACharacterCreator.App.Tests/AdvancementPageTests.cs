using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Persistence;
using DSACharacterCreator.ViewModels;

namespace DSACharacterCreator.UiTests;

/// <summary>Abschluss, Steigern, Korrektur und Rückgängig in der Oberfläche (Meilenstein 5).</summary>
public sealed class AdvancementPageTests : IDisposable
{
    private readonly TestShell _app = new();

    public AdvancementPageTests()
    {
        _app.List.NewCommand.Execute(null);
    }

    public void Dispose() => _app.Dispose();

    private EditorViewModel Editor => _app.Editor;

    private PhaseViewModel Phase => Editor.Phase;

    private ValueRowViewModel Attribute(string id) => Editor.Attributes.Rows.Single(a => a.Id == id);

    private SkillRowViewModel Skill(string id) => Editor.Skills.Groups.SelectMany(g => g.Rows).Single(r => r.Id == id);

    private void Pick(string name) =>
        Editor.Picker!.Groups.SelectMany(g => g.Items).Single(i => i.Name == name).AddCommand!.Execute(null);

    private void Finish()
    {
        Phase.FinishCommand.Execute(null);
        Assert.NotNull(Editor.Confirmation);
        Editor.Confirmation!.ConfirmCommand.Execute(null);
    }

    [Fact]
    public void Finish_ShowsForfeitedAp_AndSwitchesToAdvancement()
    {
        Attribute("MU").IncreaseCommand.Execute(null); // 15 AP, 1085 übrig

        Phase.FinishCommand.Execute(null);
        Assert.Contains("1075 AP verfallen", Editor.Confirmation!.Message);
        Editor.Confirmation.ConfirmCommand.Execute(null);

        Assert.False(Editor.IsCreation);
        Assert.True(Phase.IsAdvancement);
        Assert.False(Editor.Basics.IsEditable);
        Assert.Equal("10 AP verfügbar", Phase.RemainingText);
        Assert.Equal(CharacterPhase.Advancement, new CharacterRepository(_app.Directory).LoadAll().Characters.Single().Phase);
    }

    [Fact]
    public void Advancement_RaiseCreatesLogEntry_LowerUndoesIt()
    {
        Finish();
        Phase.AwardAmount = "50";
        Phase.AwardNote = "Abenteuer";
        Phase.AwardCommand.Execute(null);
        Assert.Equal("60 AP verfügbar", Phase.RemainingText);

        Skill("skill.klettern").IncreaseCommand.Execute(null);
        Skill("skill.klettern").IncreaseCommand.Execute(null);
        Assert.Equal(2, Skill("skill.klettern").Value);
        Assert.Equal("Klettern: 0 → 2", Phase.Log[0].Text);
        Assert.Equal("4 AP", Phase.Log[0].ApText); // Faktor B: 2 + 2
        Assert.Equal("+50 AP", Phase.Log[1].ApText);

        Skill("skill.klettern").DecreaseCommand.Execute(null);
        Skill("skill.klettern").DecreaseCommand.Execute(null);
        Assert.False(Skill("skill.klettern").DecreaseCommand.CanExecute(null)); // nicht unter den Stand der Erschaffung
        Assert.DoesNotContain(Editor.Character.Log, e => e is RaiseEntry);
    }

    [Fact]
    public void Advancement_AdvantageNeedsGmPermission()
    {
        Finish();
        Phase.AwardAmount = "100";
        Phase.AwardCommand.Execute(null);

        Editor.Traits.AddAdvantageCommand.Execute(null);
        Pick("Glück I–III");
        Assert.Equal("Erlaubnis der Spielleitung", Editor.Confirmation!.Title);
        Editor.Confirmation.CancelCommand.Execute(null);
        Assert.Empty(Editor.Traits.Advantages);

        Editor.Traits.AddAdvantageCommand.Execute(null);
        Pick("Glück I–III");
        Editor.Confirmation!.ConfirmCommand.Execute(null);
        var row = Assert.Single(Editor.Traits.Advantages);
        Assert.Equal("30 AP · Steigerung", row.ApText);
        Assert.IsType<AcquireEntry>(Editor.Character.Log[^1]);
    }

    [Fact]
    public void CreationEntries_AreLockedAfterFinishing()
    {
        Editor.SpecialAbilities.AddSpecialAbilityCommand.Execute(null);
        Pick("Finte I–III");
        Finish();

        var row = Assert.Single(Editor.SpecialAbilities.SpecialAbilities);
        Assert.False(row.CanRemove);
        Assert.True(row.HasLevels);
        Assert.False(row.DecreaseLevelCommand.CanExecute(null));
    }

    [Fact]
    public void Correction_CanBeDiscarded()
    {
        Attribute("MU").IncreaseCommand.Execute(null);
        Finish();
        Phase.AwardAmount = "100";
        Phase.AwardCommand.Execute(null);
        Attribute("MU").IncreaseCommand.Execute(null); // 9 → 10 im Protokoll

        Phase.StartCorrectionCommand.Execute(null);
        Editor.Confirmation!.ConfirmCommand.Execute(null);
        Assert.True(Phase.IsCorrecting);
        Assert.True(Editor.Basics.IsEditable);
        Assert.Equal(9, Attribute("MU").Value); // Erschaffungsstand

        Attribute("MU").IncreaseCommand.Execute(null); // in der Erschaffung schon 10
        Assert.NotNull(Phase.ConflictText);
        Assert.False(Phase.FinishCommand.CanExecute(null));
        var conflict = Phase.Log.Single(l => l.NeedsResolution);
        Assert.True(conflict.CanShift);

        Phase.DiscardCorrectionCommand.Execute(null);
        Editor.Confirmation!.ConfirmCommand.Execute(null);
        Assert.True(Phase.IsAdvancement);
        Assert.Equal(10, Attribute("MU").Value);
    }

    [Fact]
    public void Correction_ResolveAndAccept()
    {
        Finish();
        Phase.AwardAmount = "100";
        Phase.AwardCommand.Execute(null);
        Skill("skill.klettern").IncreaseCommand.Execute(null);

        Phase.StartCorrectionCommand.Execute(null);
        Editor.Confirmation!.ConfirmCommand.Execute(null);
        Skill("skill.klettern").IncreaseCommand.Execute(null);
        Phase.Log.Single(l => l.NeedsResolution).ShiftCommand.Execute(null);
        Assert.Null(Phase.ConflictText);

        Finish();
        Assert.True(Phase.IsAdvancement);
        Assert.Equal(2, Skill("skill.klettern").Value);
        Assert.Equal("Erschaffung korrigiert", Phase.Log[0].Text);
    }

    [Fact]
    public void UndoRedo_RestoresPreviousStates()
    {
        Attribute("MU").IncreaseCommand.Execute(null);
        Attribute("MU").IncreaseCommand.Execute(null);
        Finish();

        Editor.UndoCommand.Execute(null);
        Assert.True(Editor.IsCreation);
        Editor.UndoCommand.Execute(null);
        Assert.Equal(9, Attribute("MU").Value);
        Editor.RedoCommand.Execute(null);
        Editor.RedoCommand.Execute(null);
        Assert.False(Editor.IsCreation);
        Assert.Equal(10, Attribute("MU").Value);
        Assert.False(Editor.RedoCommand.CanExecute(null));
    }

    [Fact]
    public void Energies_OnlyAfterCreation()
    {
        Assert.False(Editor.Attributes.ShowEnergies);
        Finish();
        Assert.True(Editor.Attributes.ShowEnergies);
        var life = Editor.Attributes.Energies.Single(e => e.Id == Energies.LifePoints);
        Assert.False(Editor.Attributes.Energies.Single(e => e.Id == Energies.Astral).IsVisible);

        life.IncreaseCommand.Execute(null);
        Assert.Equal(1, life.Value);
        Assert.Equal(4, Editor.State.Ap.Energies);
    }
}
