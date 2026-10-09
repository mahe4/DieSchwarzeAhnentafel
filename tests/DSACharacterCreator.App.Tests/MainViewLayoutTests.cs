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

public class MainViewLayoutTests
{
    private static readonly string? ShotDir = Environment.GetEnvironmentVariable("DSA_SHOTS");

    private static Window Show(double width, double height = 800)
    {
        var window = new Window
        {
            Width = width,
            Height = height,
            Content = new MainView { DataContext = new MainViewModel() },
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
    public void SelectingSection_ChangesPage()
    {
        var vm = new MainViewModel();

        vm.SelectedSection = vm.Sections[^1];

        Assert.Equal("Charakterbogen", ((PlaceholderPageViewModel)vm.SelectedSection.Page).Title);
    }
}
