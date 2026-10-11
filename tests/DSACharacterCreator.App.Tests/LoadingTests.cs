using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using DSACharacterCreator.Core.Persistence;
using DSACharacterCreator.ViewModels;

namespace DSACharacterCreator.UiTests;

public sealed class LoadingTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dsa-loading-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [AvaloniaFact]
    public void RulesLoadInBackground_ThenListAppears()
    {
        var gate = new ManualResetEventSlim();
        var shell = new MainViewModel(new CharacterRepository(_directory), () =>
        {
            gate.Wait(TimeSpan.FromSeconds(10));
            return TestShell.Rules;
        }, new FakeFileDialogs(), new SettingsStore(Path.Combine(_directory, "settings.json")));

        Assert.IsType<LoadingViewModel>(shell.CurrentPage);
        Assert.False(shell.GoBack()); // Zurück beendet die App, statt eine leere Liste zu zeigen
        gate.Set();
        var until = DateTime.UtcNow.AddSeconds(10);
        while (shell.CurrentPage is LoadingViewModel && DateTime.UtcNow < until) Dispatcher.UIThread.RunJobs();
        Assert.IsType<CharacterListViewModel>(shell.CurrentPage);
        Assert.Same(TestShell.Rules, shell.Rules);
    }
}
