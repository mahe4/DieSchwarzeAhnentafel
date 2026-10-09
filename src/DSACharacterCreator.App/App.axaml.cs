using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
        if (!_developerToolsAttached)
        {
            this.AttachDeveloperTools();
            _developerToolsAttached = true;
        }
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime singleViewFactoryApplicationLifetime)
        {
            singleViewFactoryApplicationLifetime.MainViewFactory = () => new MainView { DataContext = new MainViewModel() };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}