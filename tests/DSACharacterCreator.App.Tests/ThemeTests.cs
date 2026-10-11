using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using DSACharacterCreator.Services;
using DSACharacterCreator.ViewModels;
using DSACharacterCreator.Views;

namespace DSACharacterCreator.UiTests;

/// <summary>Stile „Heldendokument“ (Standard) und „Nachtlager“, umschaltbar in den Einstellungen.</summary>
public sealed class ThemeTests : IDisposable
{
    private readonly TestShell _app = new();

    public void Dispose()
    {
        AppTheme.Apply(AppTheme.Heldendokument); // andere Tests erwarten den Standard
        _app.Dispose();
    }

    private static readonly string? ShotDir = Environment.GetEnvironmentVariable("DSA_SHOTS");

    [AvaloniaFact]
    public void Default_IsHeldendokument()
    {
        Assert.Equal(ThemeVariant.Light, Application.Current!.RequestedThemeVariant);
        _app.List.OpenSettingsCommand.Execute(null);
        Assert.True(_app.List.Settings!.IsHeldendokument);
    }

    [AvaloniaFact]
    public void SwitchingTheme_AppliesAndIsSaved()
    {
        _app.List.OpenSettingsCommand.Execute(null);
        _app.List.Settings!.IsNachtlager = true;

        Assert.Equal(ThemeVariant.Dark, Application.Current!.RequestedThemeVariant);
        Assert.Equal(AppTheme.Nachtlager, _app.Shell.Settings.Load().Theme);
        Assert.True(_app.Shell.GoBack());
        Assert.Null(_app.List.Settings);

        // Neustart: gespeicherter Stil wird übernommen
        AppTheme.Apply(AppTheme.Heldendokument);
        _ = new MainViewModel(_app.Shell.Repository, TestShell.Rules, _app.Dialogs, _app.Shell.Settings);
        Assert.Equal(ThemeVariant.Dark, Application.Current!.RequestedThemeVariant);
    }

    [AvaloniaFact]
    public void Settings_OpenDefaultSources()
    {
        _app.List.OpenSettingsCommand.Execute(null);
        _app.List.Settings!.OpenSourcesCommand.Execute(null);

        Assert.Null(_app.List.Settings);
        Assert.NotNull(_app.List.Sources);
    }

    [AvaloniaTheory]
    [InlineData(AppTheme.Heldendokument)]
    [InlineData(AppTheme.Nachtlager)]
    public void BothThemes_Render(string theme)
    {
        AppTheme.Apply(theme);
        _app.List.NewCommand.Execute(null);
        var editor = _app.Editor;
        editor.Name = "Alrik vom Großen Fluss";
        editor.Basics.SpeciesOptions.Single(s => s.Name == "Mensch").IsSelected = true;
        editor.Basics.ChooseProfessionCommand.Execute(null);
        editor.Picker!.Groups.SelectMany(g => g.Items).First(i => i.Name == "Gildenloser Magier nach Agrimeton").AddCommand!.Execute(null);
        editor.ClosePicker();
        editor.SelectedSection = editor.Sections.Single(s => s.Page == editor.Skills);
        var window = new Window { Width = 1400, Height = 860, Content = new MainView { DataContext = _app.Shell } };
        window.Show();
        Settle();
        Shot(window, $"theme-{theme}-wide");
        editor.SelectedSection = editor.Sections.Single(s => s.Page == editor.Magic);
        Settle();
        Shot(window, $"theme-{theme}-magic-wide");
        window.Width = 392;
        window.Height = 820;
        editor.SelectedSection = editor.Sections.Single(s => s.Page == editor.Skills);
        Settle();
        Shot(window, $"theme-{theme}-narrow");
        editor.SelectedSection = editor.Sections[0];
        Settle();
        Shot(window, $"theme-{theme}-basics-narrow");
        editor.BackCommand.Execute(null);
        _app.List.OpenSettingsCommand.Execute(null);
        Settle();
        Shot(window, $"theme-{theme}-settings-narrow");
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
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        window.CaptureRenderedFrame()!.Save(Path.Combine(ShotDir, $"{name}.png"), new PngBitmapEncoderOptions());
    }
}
