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
            for (var i = 0; i < 4; i++) editor.Values.Attributes.Single(a => a.Id == id).IncreaseCommand.Execute(null);
        var klettern = editor.Values.SkillGroups.SelectMany(g => g.Rows).Single(r => r.Id == "skill.klettern");
        for (var i = 0; i < 7; i++) klettern.IncreaseCommand.Execute(null);
        klettern.ToggleDetailsCommand.Execute(null);
        editor.SelectedSection = editor.Sections[1];
        Dispatcher.UIThread.RunJobs();
        Shot(window, "values-wide");

        Assert.Equal(8, Find<ItemsControl>(window, "AttributeList").ItemCount);

        window.Width = 390;
        window.Height = 844;
        Dispatcher.UIThread.RunJobs();
        Shot(window, "values-narrow");

        editor.SelectedSection = editor.Sections[0];
        Dispatcher.UIThread.RunJobs();
        Shot(window, "basics-narrow");
        Assert.Equal(7, Find<ItemsControl>(window, "ExperienceLevels").ItemCount);
    }

    [AvaloniaFact]
    public void SelectingSection_ChangesPage()
    {
        _app.List.NewCommand.Execute(null);
        var editor = _app.Editor;

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
