using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.ViewModels;
using DSACharacterCreator.Views;

namespace DSACharacterCreator.UiTests;

/// <summary>Zauber- und Liturgieerweiterungen auf der Seite „Magie &amp; Götterwirken“.</summary>
public sealed class SpellEnhancementTests : IDisposable
{
    private readonly TestShell _app = new();

    private static readonly string? ShotDir = Environment.GetEnvironmentVariable("DSA_SHOTS");

    public SpellEnhancementTests()
    {
        _app.List.NewCommand.Execute(null);
        Editor.Apply(c =>
        {
            c.Traits.Add(new TraitEntry { Id = "adv.zauberer" });
            c.SpecialAbilities.Add(new TraitEntry { Id = "sf.tradition-gildenmagier" });
            c.Attributes["MU"] = 14;
            c.Attributes["KL"] = 14;
            c.Attributes["CH"] = 13;
            c.Spells.Add(new SpellEntry { Id = "spell.ignifaxius", Value = 10 });
        });
    }

    public void Dispose() => _app.Dispose();

    private EditorViewModel Editor => _app.Editor;

    private SpellRowViewModel Row => Editor.Magic.Spells.Single();

    [Fact]
    public void Enhancements_ShowWhenExpanded_AndCanBeLearned()
    {
        Assert.False(Row.ShowEnhancements);
        Assert.Empty(Row.Enhancements);
        Row.ToggleDetailsCommand.Execute(null);
        Assert.True(Row.ShowEnhancements);
        Assert.Equal("Zaubererweiterungen", Row.EnhancementsTitle);
        Assert.Equal(5, Row.Enhancements.Count);
        Assert.Equal("FW 8 · 3 AP", Row.Enhancements[0].Info);

        var before = Editor.State.Ap.Spells;
        Row.Enhancements.Single(e => e.Name == "Größere Reichweite").IsLearned = true;
        Assert.Equal(before + 3, Editor.State.Ap.Spells);
        Assert.Contains("+ Größere Reichweite", Row.Detail);
        Assert.True(Row.Enhancements.Single(e => e.Name == "Größere Reichweite").IsLearned);

        // Zu niedriger FW: wird gewählt, aber gemeldet
        Row.Enhancements.Single(e => e.Name == "Zwei Ziele").IsLearned = true;
        Assert.Contains("setzt FW 16 voraus", Row.IssueText);

        Row.Enhancements.Single(e => e.Name == "Zwei Ziele").IsLearned = false;
        Row.Enhancements.Single(e => e.Name == "Größere Reichweite").IsLearned = false;
        Assert.Equal(before, Editor.State.Ap.Spells);
        Assert.Null(Row.IssueText);
    }

    [AvaloniaFact]
    public void Screenshot_ExpandedSpell()
    {
        Row.ToggleDetailsCommand.Execute(null);
        Row.Enhancements[0].IsLearned = true;
        Editor.SelectedSection = Editor.Sections.First(s => s.Page is MagicPageViewModel);
        foreach (var width in new[] { 1200, 392 })
        {
            var window = new Window { Width = width, Height = 1100, Content = new MainView { DataContext = _app.Shell } };
            window.Show();
            for (var i = 0; i < 3; i++)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
            if (ShotDir is not null)
            {
                System.IO.Directory.CreateDirectory(ShotDir);
                window.CaptureRenderedFrame()!.Save(Path.Combine(ShotDir, $"spell-enhancements-{width}.png"), new PngBitmapEncoderOptions());
            }
            window.Close();
        }
    }
}
