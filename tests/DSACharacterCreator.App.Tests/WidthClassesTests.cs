using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using DSACharacterCreator.Views;

namespace DSACharacterCreator.UiTests;

/// <summary>Breitenklassen statt Container Queries (am Handy zu teuer, siehe <see cref="WidthClasses"/>).</summary>
public sealed class WidthClassesTests
{
    [Fact]
    public void Parse_ReadsRanges()
    {
        Assert.Equal([("narrow", 0, 800), ("wide", 820, double.PositiveInfinity), ("mid", 800, 1200)],
            WidthClasses.Parse("narrow:..800 wide:820.. mid:800..1200"));
        Assert.Throws<FormatException>(() => WidthClasses.Parse("narrow<800").ToList());
    }

    [AvaloniaFact]
    public void Classes_FollowWidth()
    {
        var panel = new Panel();
        WidthClasses.SetRules(panel, "compact:..1200 narrow:..800");
        var window = new Window { Width = 1300, Height = 400, Content = panel };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.False(panel.Classes.Contains("compact"));
        Assert.False(panel.Classes.Contains("narrow"));

        window.Width = 1000;
        Dispatcher.UIThread.RunJobs();
        Assert.True(panel.Classes.Contains("compact"));
        Assert.False(panel.Classes.Contains("narrow"));

        window.Width = 390;
        Dispatcher.UIThread.RunJobs();
        Assert.True(panel.Classes.Contains("compact"));
        Assert.True(panel.Classes.Contains("narrow"));
    }
}
