using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DSACharacterCreator.Content;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Persistence;
using DSACharacterCreator.Services;
using DSACharacterCreator.ViewModels;
using DSACharacterCreator.Views;

namespace DSACharacterCreator;

public partial class App : Application
{
#if DEBUG
    // Headless-Tests erzeugen mehrere App-Instanzen pro Prozess; DevTools dürfen nur einmal angehängt werden.
    private static bool _developerToolsAttached;
#endif

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        // Auf Android findet die DevTools-Verbindung keinen Gegenpart und bringt die App nach einigen Sekunden zum Absturz.
        if (!_developerToolsAttached && !OperatingSystem.IsAndroid())
        {
            this.AttachDeveloperTools();
            _developerToolsAttached = true;
        }
#endif
    }

    public static MainViewModel CreateMainViewModel()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var rules = RuleData.Load(EmbeddedContent.Open, EmbeddedContent.List());
        Timing.Log("Regeldaten laden", watch);
        watch.Restart();
        var main = new MainViewModel(new CharacterRepository(CharacterRepository.DefaultDirectory), rules, new StorageFileDialogs());
        Timing.Log("Heldenliste", watch);
        return main;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = CreateMainViewModel()
            };
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime singleViewFactoryApplicationLifetime)
        {
            singleViewFactoryApplicationLifetime.MainViewFactory = () => new MainView { DataContext = CreateMainViewModel() };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = CreateMainViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}