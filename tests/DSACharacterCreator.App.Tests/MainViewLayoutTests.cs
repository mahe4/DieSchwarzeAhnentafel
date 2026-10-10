using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DSACharacterCreator.ViewModels;
using DSACharacterCreator.Views;

[assembly: AvaloniaTestApplication(typeof(DSACharacterCreator.UiTests.TestAppBuilder))]

namespace DSACharacterCreator.UiTests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public sealed class MainViewLayoutTests : IDisposable
{
    private readonly TestShell _app = new();

    public void Dispose() => _app.Dispose();

    private static readonly string? ShotDir = Environment.GetEnvironmentVariable("DSA_SHOTS");

    private Window Show(double width, double height = 800, bool openEditor = true)
    {
        if (openEditor)
        {
            _app.List.NewCommand.Execute(null);
            _app.Editor.Name = "Alrik";
        }
        var window = new Window
        {
            Width = width,
            Height = height,
            Content = new MainView { DataContext = _app.Shell },
        };
        window.Show();
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        return window;
    }

    private static T Find<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static void Shot(Window window, string name)
    {
        if (ShotDir is null) return;
        Directory.CreateDirectory(ShotDir);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Kein Frame gerendert.");
        frame.Save(Path.Combine(ShotDir, $"{name}.png"), new PngBitmapEncoderOptions());
    }

    [AvaloniaFact]
    public void Wide_ShowsSideNavigationAndSummaryPanel()
    {
        var window = Show(1400);
        Shot(window, "wide");

        Assert.Equal(Dock.Left, DockPanel.GetDock(Find<Border>(window, "NavHost")));
        Assert.True(Find<Border>(window, "SummaryHost").IsVisible);
        Assert.False(Find<Border>(window, "CompactBar").IsVisible);
    }

    [AvaloniaFact]
    public void Medium_ReplacesSummaryPanelWithCompactBar()
    {
        var window = Show(1000);
        Shot(window, "medium");

        Assert.Equal(Dock.Left, DockPanel.GetDock(Find<Border>(window, "NavHost")));
        Assert.False(Find<Border>(window, "SummaryHost").IsVisible);
        Assert.True(Find<Border>(window, "CompactBar").IsVisible);
    }

    [AvaloniaFact]
    public void Narrow_MovesNavigationToBottom()
    {
        var window = Show(390, 844);
        Shot(window, "narrow");

        Assert.Equal(Dock.Bottom, DockPanel.GetDock(Find<Border>(window, "NavHost")));
        Assert.False(Find<Border>(window, "SummaryHost").IsVisible);
        Assert.True(Find<Border>(window, "CompactBar").IsVisible);
    }

    [AvaloniaFact]
    public void ValuesPage_RendersInAllWidths()
    {
        var window = Show(1400, 900);
        var editor = _app.Editor;
        foreach (var id in new[] { "MU", "GE", "KK" })
            for (var i = 0; i < 4; i++) editor.Attributes.Rows.Single(a => a.Id == id).IncreaseCommand.Execute(null);
        var klettern = editor.Skills.Groups.SelectMany(g => g.Rows).Single(r => r.Id == "skill.klettern");
        for (var i = 0; i < 7; i++) klettern.IncreaseCommand.Execute(null);
        klettern.ToggleDetailsCommand.Execute(null);
        editor.SelectedSection = editor.Sections.Single(s => s.Page == editor.Attributes);
        Dispatcher.UIThread.RunJobs();
        Shot(window, "attributes-wide");
        Assert.Equal(8, Find<ItemsControl>(window, "AttributeList").ItemCount);

        editor.SelectedSection = editor.Sections.Single(s => s.Page == editor.Skills);
        Dispatcher.UIThread.RunJobs();
        Shot(window, "skills-wide");

        editor.SelectedSection = editor.Sections.Single(s => s.Page == editor.CombatTechniques);
        Dispatcher.UIThread.RunJobs();
        Shot(window, "combat-wide");
        Assert.Equal(21, Find<ItemsControl>(window, "CombatTechniqueList").ItemCount);

        window.Width = 390;
        window.Height = 844;
        Dispatcher.UIThread.RunJobs();
        Shot(window, "combat-narrow");
        editor.SelectedSection = editor.Sections.Single(s => s.Page == editor.Skills);
        Dispatcher.UIThread.RunJobs();
        Shot(window, "skills-narrow");
        editor.SelectedSection = editor.Sections.Single(s => s.Page == editor.Attributes);
        Dispatcher.UIThread.RunJobs();
        Shot(window, "attributes-narrow");

        editor.SelectedSection = editor.Sections[0];
        Dispatcher.UIThread.RunJobs();
        Shot(window, "basics-narrow");
        Assert.Equal(10, Find<ItemsControl>(window, "SpeciesList").ItemCount);
    }

    [AvaloniaFact]
    public void BasicsAndPicker_RenderInAllWidths()
    {
        var window = Show(1400, 900);
        var basics = _app.Editor.Basics;
        basics.SpeciesOptions.Single(s => s.Name == "Elfen").IsSelected = true;
        basics.ChooseProfessionCommand.Execute(null);
        _app.Editor.Picker!.Groups.SelectMany(g => g.Items).Single(i => i.Name == "Streuner").PickCommand!.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Shot(window, "basics-wide");
        Assert.Equal(10, Find<ItemsControl>(window, "SpeciesList").ItemCount);

        basics.ChooseCultureCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Shot(window, "picker-wide");
        Assert.True(Find<Border>(window, "PickerOverlay").IsVisible);

        window.Width = 390;
        window.Height = 844;
        Dispatcher.UIThread.RunJobs();
        Shot(window, "picker-narrow");
        _app.Editor.ClosePicker();
        Dispatcher.UIThread.RunJobs();
        Shot(window, "basics-narrow2");
    }

    [AvaloniaFact]
    public void TraitsPage_Renders()
    {
        var window = Show(1400, 900);
        var editor = _app.Editor;
        editor.Basics.SpeciesOptions.Single(s => s.Name == "Elfen").IsSelected = true;
        editor.Character.Creation.Traits.Add(new Core.Characters.TraitEntry { Id = "adv.begabung", Option = "skill.singen" });
        editor.Character.Creation.Traits.Add(new Core.Characters.TraitEntry { Id = "adv.hohe-lebenskraft", Level = 2 });
        editor.Character.Creation.Traits.Add(new Core.Characters.TraitEntry { Id = "disadv.angst-vor", Specification = "Spinnen" });
        Core.Rules.Templates.ChangeCulture(editor.Character.Creation, "culture.auelfen", TestShell.Rules);
        Core.Rules.Templates.ChangeProfession(editor.Character.Creation, "profession.streuner", TestShell.Rules);
        editor.Character.Creation.SpecialAbilities.Add(new Core.Characters.TraitEntry { Id = "sf.finte", Level = 2 });
        editor.Refresh();
        editor.SelectedSection = editor.Sections.Single(s => s.Page == editor.Traits);
        Dispatcher.UIThread.RunJobs();
        Shot(window, "traits-wide");
        Assert.Equal(4, Find<ItemsControl>(window, "AdvantageList").ItemCount);

        editor.SelectedSection = editor.Sections.Single(s => s.Page == editor.SpecialAbilities);
        Dispatcher.UIThread.RunJobs();
        Shot(window, "sf-wide");
        Assert.True(Find<ItemsControl>(window, "SpecialAbilityList").ItemCount >= 3);

        window.Width = 360;
        window.Height = 844;
        Dispatcher.UIThread.RunJobs();
        Shot(window, "sf-narrow");
    }

    [AvaloniaFact]
    public void PhasePanel_RendersInAllPhases()
    {
        var window = Show(1400, 900);
        var editor = _app.Editor;
        var phase = editor.Phase;
        Dispatcher.UIThread.RunJobs();
        Shot(window, "phase-creation");
        Assert.True(Find<Button>(window, "FinishButton").IsVisible);

        phase.FinishCommand.Execute(null);
        editor.Confirmation!.ConfirmCommand.Execute(null);
        phase.AwardAmount = "120";
        phase.AwardNote = "Der Fluch von Ashgrabaal";
        phase.AwardCommand.Execute(null);
        var klettern = editor.Skills.Groups.SelectMany(g => g.Rows).Single(r => r.Id == "skill.klettern");
        klettern.IncreaseCommand.Execute(null);
        klettern.IncreaseCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Find<Expander>(window, "LogExpander").IsExpanded = true;
        Dispatcher.UIThread.RunJobs();
        Shot(window, "phase-advancement");
        Assert.True(Find<Button>(window, "AwardButton").IsVisible);
        Assert.False(Find<StackPanel>(window, "CreationChoices").IsEnabled);

        phase.StartCorrectionCommand.Execute(null);
        editor.Confirmation!.ConfirmCommand.Execute(null);
        klettern.IncreaseCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        window.Width = 390;
        window.Height = 844;
        Dispatcher.UIThread.RunJobs();
        Shot(window, "phase-correction-narrow");
        Assert.True(Find<Button>(window, "AcceptCorrectionButton").IsVisible);
    }

    [AvaloniaFact]
    public void MagicPage_Renders()
    {
        var window = Show(1400, 900);
        var editor = _app.Editor;
        Core.Rules.Templates.ChangeProfession(editor.Character.Creation, "profession.gildenloser-magier-nach-agrimeton", TestShell.Rules);
        Core.Rules.Templates.ChooseProfessionSpell(editor.Character.Creation, 0, "cantrip.feuerfinger");
        editor.Refresh();
        editor.SelectedSection = editor.Sections.Single(s => s.Page == editor.Magic);
        Dispatcher.UIThread.RunJobs();
        Shot(window, "magic-wide");
        Assert.Equal(7, Find<ItemsControl>(window, "SpellList").ItemCount);

        window.Width = 390;
        window.Height = 844;
        Dispatcher.UIThread.RunJobs();
        Shot(window, "magic-narrow");
    }

    [AvaloniaFact]
    public void SelectingSection_ChangesPage()
    {
        _app.List.NewCommand.Execute(null);
        var editor = _app.Editor;

        Assert.Equal(["Grundlagen", "Eigenschaften", "Vor-/Nachteile", "Sonderfertigkeiten", "Kampftechniken", "Talente",
                "Magie & Götterwirken", "Ausrüstung", "Charakterbogen"],
            editor.Sections.Select(s => s.Title));

        editor.SelectedSection = editor.Sections[^1];

        Assert.Equal("Charakterbogen", Assert.IsType<PlaceholderPageViewModel>(editor.SelectedSection.Page).Title);
    }

    [AvaloniaFact]
    public void CharacterList_RendersCardsAndDeleteConfirmation()
    {
        foreach (var name in new[] { "Alrik", "Geron", "Fenia" })
        {
            _app.List.NewCommand.Execute(null);
            _app.Editor.Name = name;
            _app.Shell.GoBack();
        }
        var window = Show(1000, openEditor: false);
        Shot(window, "list-wide");

        Assert.Equal(3, Find<ItemsControl>(window, "CharacterList").ItemCount);
        Assert.False(Find<Border>(window, "DeleteConfirmation").IsVisible);

        _app.List.Characters[0].DeleteCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Shot(window, "list-delete");
        Assert.True(Find<Border>(window, "DeleteConfirmation").IsVisible);

        window.Width = 390;
        window.Height = 844;
        _app.List.CancelDeleteCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Shot(window, "list-narrow");
    }
}
