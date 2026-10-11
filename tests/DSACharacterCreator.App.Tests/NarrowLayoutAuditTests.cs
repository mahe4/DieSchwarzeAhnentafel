using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DSACharacterCreator.ViewModels;
using DSACharacterCreator.Views;

namespace DSACharacterCreator.UiTests;

/// <summary>
/// Handybreite (das Testgerät hat 392 dp, kleinere Handys 360 dp): Kein Wort darf mitten im Wort umbrechen und kein
/// Text abgeschnitten werden, weil der Platz fehlt. Geprüft werden alle Seiten mit einem Helden, der möglichst viele
/// Elemente zeigt, sowie die Auswahllisten.
/// </summary>
public sealed class NarrowLayoutAuditTests : IDisposable
{
    private readonly TestShell _app = new();

    public void Dispose() => _app.Dispose();

    private static readonly string? ShotDir = Environment.GetEnvironmentVariable("DSA_SHOTS");

    private EditorViewModel Editor => _app.Editor;

    private void Pick(string name) =>
        Editor.Picker!.Groups.SelectMany(g => g.Items).First(i => i.Name == name).AddCommand!.Execute(null);

    private void BuildHero()
    {
        _app.List.NewCommand.Execute(null);
        Editor.Name = "Alrik vom Großen Fluss";
        Editor.Basics.SpeciesOptions.Single(s => s.Name == "Mensch").IsSelected = true;
        Editor.Basics.ChooseCultureCommand.Execute(null);
        Pick("Mittelreicher");
        Editor.Basics.ChooseProfessionCommand.Execute(null);
        Pick("Gildenloser Magier nach Agrimeton");
        Editor.Equipment.AddWeaponCommand.Execute(null);
        Pick("Langschwert");
        Editor.Equipment.AddArmorCommand.Execute(null);
        Pick("Kettenhemd");
        Editor.Equipment.AddItemCommand.Execute(null);
        Pick("Fackel");
        Editor.ClosePicker();
    }

    private static Window Show(MainViewModel shell, double width)
    {
        var window = new Window { Width = width, Height = 780, Content = new MainView { DataContext = shell } };
        window.Show();
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

    /// <summary>Alle sichtbaren Texte, in denen ein Wort umbricht oder Text über den Rand hinausgeht.</summary>
    private static IEnumerable<string> Problems(Window window, string where)
    {
        foreach (var tb in window.GetVisualDescendants().OfType<TextBlock>())
        {
            if (!tb.IsEffectivelyVisible || string.IsNullOrWhiteSpace(tb.Text) || tb.Bounds.Width <= 0) continue;
            if (!IsOnScreen(tb, window)) continue;
            var typeface = new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight);
            double Width(string s) => new TextLayout(s, typeface, tb.FontSize, null).Width;
            var available = tb.Bounds.Width - tb.Padding.Left - tb.Padding.Right + 0.5;
            if (tb.TextWrapping == TextWrapping.NoWrap)
            {
                if (tb.TextTrimming != TextTrimming.None) continue;
                if (Width(tb.Text) > available)
                    yield return $"{where}: abgeschnitten „{tb.Text}“ ({Width(tb.Text):0} > {available:0})";
                else if (ClippedBy(tb, window, Width(tb.Text)) is { } clip)
                    yield return $"{where}: abgeschnitten von {clip} „{tb.Text}“";
                continue;
            }
            foreach (var word in tb.Text.Split((char[]) [' ', '\n', ' '], StringSplitOptions.RemoveEmptyEntries).Distinct())
                if (!word.Contains('/') && Width(word) > available)
                    yield return $"{where}: Wort umbrochen „{word}“ in „{Short(tb.Text)}“ ({Width(word):0} > {available:0})";
        }
    }

    /// <summary>Übergeordnetes Element, das den Text abschneidet (z. B. ein Knopf), sonst <c>null</c>.</summary>
    private static string? ClippedBy(TextBlock tb, Window window, double textWidth)
    {
        var left = tb.TranslatePoint(new Point(0, 0), window)!.Value.X;
        var right = left + textWidth;
        if (tb.TextAlignment is TextAlignment.Right or TextAlignment.Center) return null;
        foreach (var ancestor in tb.GetVisualAncestors().OfType<Control>())
        {
            if (ancestor is Avalonia.Controls.Presenters.ScrollContentPresenter or ScrollViewer) break;
            if (!ancestor.ClipToBounds) continue;
            var ancestorRight = ancestor.TranslatePoint(new Point(ancestor.Bounds.Width, 0), window)!.Value.X;
            if (right > ancestorRight + 0.5) return ancestor.GetType().Name;
        }
        return null;
    }

    private static bool IsOnScreen(Visual visual, Window window)
    {
        var p = visual.TranslatePoint(new Point(0, 0), window);
        return p is { } point && point.Y < window.Height && point.Y + visual.Bounds.Height > 0;
    }

    private static string Short(string text) => text.Length > 60 ? text[..60] + "…" : text;

    private static void Shot(Window window, string name)
    {
        if (ShotDir is null) return;
        Directory.CreateDirectory(ShotDir);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        window.CaptureRenderedFrame()!.Save(Path.Combine(ShotDir, $"audit-{name.Replace("/", "")}.png"), new PngBitmapEncoderOptions());
    }

    /// <summary>Seite ganz durchscrollen und an jeder Position prüfen.</summary>
    private static IEnumerable<string> Scan(Window window, string where)
    {
        var problems = new List<string>();
        var scrollers = window.GetVisualDescendants().OfType<ScrollViewer>()
            .Where(s => s.IsEffectivelyVisible && s.Extent.Height > s.Viewport.Height).ToList();
        problems.AddRange(Problems(window, where));
        Shot(window, where);
        foreach (var scroller in scrollers)
        {
            var step = 0;
            for (var y = scroller.Viewport.Height * 0.8; y < scroller.Extent.Height && step < 60; y += scroller.Viewport.Height * 0.8, step++)
            {
                scroller.Offset = new Vector(0, y);
                Settle();
                problems.AddRange(Problems(window, where));
                if (step < 4) Shot(window, $"{where}-{step + 1}");
            }
            scroller.Offset = default;
        }
        return problems;
    }

    [AvaloniaTheory]
    [InlineData(392)]
    [InlineData(360)]
    public void NoWordBreaksOrClippedTextOnPhones(double width)
    {
        var problems = new List<string>();
        var list = Show(_app.Shell, width);
        _app.List.NewCommand.Execute(null);
        _app.Shell.GoBack();
        Settle();
        problems.AddRange(Scan(list, $"{width}-liste"));
        list.Close();

        BuildHero();
        var window = Show(_app.Shell, width);
        foreach (var section in Editor.Sections)
        {
            Editor.SelectedSection = section;
            Settle();
            problems.AddRange(Scan(window, $"{width}-{section.ShortTitle}"));
        }

        Editor.Basics.ChooseProfessionCommand.Execute(null);
        Settle();
        problems.AddRange(Problems(window, $"{width}-auswahl"));
        Shot(window, $"{width}-auswahl");
        Editor.ClosePicker();
        Editor.SpecialAbilities.AddSpecialAbilityCommand.Execute(null);
        Editor.Picker!.Groups.SelectMany(g => g.Items).First().PickCommand!.Execute(null);
        Settle();
        problems.AddRange(Problems(window, $"{width}-sammelauswahl"));
        Shot(window, $"{width}-sammelauswahl");
        Editor.ClosePicker();
        Editor.OpenSources();
        Settle();
        problems.AddRange(Problems(window, $"{width}-quellen"));
        Editor.ClosePicker();

        // Steigerungsphase: Phasenkasten, Protokoll
        Editor.SelectedSection = Editor.Sections[0];
        Editor.Phase.FinishCommand.Execute(null);
        Editor.Confirmation?.ConfirmCommand.Execute(null);
        Settle();
        problems.AddRange(Scan(window, $"{width}-steigerung"));

        Assert.True(problems.Count == 0, string.Join("\n", problems.Distinct()));
    }
}
