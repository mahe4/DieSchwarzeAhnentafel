using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DSACharacterCreator.Views;

namespace DSACharacterCreator.UiTests;

/// <summary>Die Auswahlliste muss auch mit allen Professionen flüssig filtern (am Handy war sie ruckelig).</summary>
public sealed class PickerPerformanceTests : IDisposable
{
    private readonly TestShell _app = new();

    public void Dispose() => _app.Dispose();

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    [AvaloniaFact]
    public void ProfessionPicker_RealizesOnlyVisibleRows_AndFiltersQuickly()
    {
        _app.List.NewCommand.Execute(null);
        var window = new Window { Width = 390, Height = 844, Content = new MainView { DataContext = _app.Shell } };
        window.Show();
        Render();

        var stopwatch = Stopwatch.StartNew();
        _app.Editor.Basics.ChooseProfessionCommand.Execute(null);
        Render();
        var open = stopwatch.ElapsedMilliseconds;

        // Nur sichtbare Einträge dürfen als Steuerelemente existieren.
        var realized = window.GetVisualDescendants().OfType<Button>().Count(b => b.DataContext is ViewModels.PickerItemViewModel);
        Console.WriteLine($"realisierte Zeilen nach dem Öffnen: {realized}, Öffnen: {open} ms");
        Assert.InRange(realized, 1, 40);

        var picker = _app.Editor.Picker!;
        stopwatch.Restart();
        foreach (var text in new[] { "k", "kr", "kri", "krie", "krieg", "kriege", "krieger" })
        {
            picker.Search = text;
            Render();
        }
        var typing = stopwatch.ElapsedMilliseconds;

        Console.WriteLine($"Picker öffnen: {open} ms, 7 Tastendrücke: {typing} ms, realisierte Zeilen: {realized}");
        Assert.True(typing < 1500, $"Filtern dauerte {typing} ms");
    }
}
