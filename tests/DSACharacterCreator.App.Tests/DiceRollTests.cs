using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DSACharacterCreator.Core.Sheet;
using DSACharacterCreator.ViewModels;
using DSACharacterCreator.Views;

namespace DSACharacterCreator.UiTests;

/// <summary>Würfelknöpfe im Charakterbogen: Talente, Zauber, Waffen; in den Einstellungen ausblendbar.</summary>
public sealed class DiceRollTests : IDisposable
{
    private readonly TestShell _app = new();

    public void Dispose() => _app.Dispose();

    private static readonly string? ShotDir = Environment.GetEnvironmentVariable("DSA_SHOTS");

    private EditorViewModel Editor => _app.Editor;

    private void BuildHero()
    {
        _app.List.NewCommand.Execute(null);
        Editor.Basics.SpeciesOptions.Single(s => s.Name == "Mensch").IsSelected = true;
        Core.Rules.Templates.ChangeCulture(Editor.Character.Creation, "culture.mittelreicher", TestShell.Rules);
        Core.Rules.Templates.ChangeProfession(Editor.Character.Creation, "profession.gildenloser-magier-nach-agrimeton", TestShell.Rules);
        Editor.Refresh();
        Editor.ChangeEquipment(list =>
        {
            list.Add(new Core.Characters.EquipmentEntry { Id = "weapon.langschwert", Equipped = true });
            list.Add(new Core.Characters.EquipmentEntry { Id = "weapon.kurzschwert", Equipped = true });
        });
    }

    private Window Show(double width)
    {
        var window = new Window { Width = width, Height = 800, Content = new MainView { DataContext = _app.Shell } };
        window.Show();
        Editor.SelectedSection = Editor.Sections.Single(s => s.Page == Editor.Sheet);
        Settle();
        return window;
    }

    private static void Settle()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private static void Shot(Window window, string name)
    {
        if (ShotDir is null) return;
        Directory.CreateDirectory(ShotDir);
        window.CaptureRenderedFrame()!.Save(Path.Combine(ShotDir, $"dice-{name}.png"), new PngBitmapEncoderOptions());
    }

    private static List<Button> DiceButtons(Window window) =>
        window.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("dice") && b.IsEffectivelyVisible).ToList();

    [AvaloniaFact]
    public void DiceButtons_OpenRollDialog()
    {
        BuildHero();
        var window = Show(392);
        Shot(window, "sheet");
        var buttons = DiceButtons(window);
        Assert.NotEmpty(buttons);

        // erster Knopf: Kampftechnik; Waffe mit Auswahl AT/PA/beidhändig
        var weaponButton = buttons.First(b => b.DataContext is SheetWeapon);
        weaponButton.Command!.Execute(weaponButton.CommandParameter);
        Settle();
        var roll = Editor.DiceRoll!;
        Assert.True(roll.HasOptions);
        Assert.Contains(roll.Options, o => o.Check.Label == "AT beidhändig");
        Assert.NotEmpty(roll.Faces);
        Assert.True(window.GetVisualDescendants().OfType<DiceRollView>().Single().IsEffectivelyVisible);
        Shot(window, "weapon");
        // Auswahl wechseln würfelt nicht neu
        var first = roll.Faces[0].Value;
        roll.SelectedOption = roll.Options.Single(o => o.Check.Label == "PA beidhändig");
        Assert.StartsWith("PA beidhändig", roll.Subtitle);
        Assert.Equal(first, roll.Faces[0].Value);
        Assert.True(Editor.ClosePicker());
        Assert.Null(Editor.DiceRoll);

        // Talent: drei Würfel, Modifikator wertet dieselben Würfel neu aus
        var skillButton = buttons.First(b => b.DataContext is SheetSkill { Name: "Klettern" });
        skillButton.Command!.Execute(skillButton.CommandParameter);
        Settle();
        roll = Editor.DiceRoll!;
        Assert.Equal("Klettern", roll.Title);
        Assert.Equal(3, roll.Faces.Count);
        var dice = roll.Faces.Select(f => f.Value).ToList();
        roll.DecreaseCommand.Execute(null);
        Assert.Equal("−1 erschwert", roll.ModifierText);
        Assert.Equal(dice, roll.Faces.Select(f => f.Value));
        Shot(window, "skill");
        roll.CloseCommand.Execute(null);

        // Zauber mit Probe; Zaubertricks ohne Knopf
        Assert.Contains(buttons, b => b.DataContext is SheetSpell { Name: "Ignifaxius" });
    }

    [AvaloniaFact]
    public void DiceButtons_HiddenBySetting()
    {
        _app.List.OpenSettingsCommand.Execute(null);
        Assert.True(_app.List.Settings!.ShowDiceButtons);
        _app.List.Settings.ShowDiceButtons = false;
        Assert.False(_app.Shell.Settings.Load().ShowDiceButtons);
        _app.List.Settings.CloseCommand.Execute(null);
        BuildHero();
        var window = Show(392);
        Assert.Empty(DiceButtons(window));
    }
}
