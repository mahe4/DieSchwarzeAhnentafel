using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSACharacterCreator.Core.Persistence;
using DSACharacterCreator.Services;

namespace DSACharacterCreator.ViewModels;

/// <summary>Einstellungen der App (Hauptmenü): Stil der Oberfläche, Würfelknöpfe und Standardquellen für neue Helden.</summary>
public sealed partial class AppSettingsViewModel : ObservableObject
{
    private readonly SettingsStore _store;
    private readonly Action _openSources;
    private readonly Action _onClose;

    public AppSettingsViewModel(SettingsStore store, Action openSources, Action onClose)
    {
        _store = store;
        _openSources = openSources;
        _onClose = onClose;
        var settings = store.Load();
        _theme = AppTheme.Normalize(settings.Theme);
        _showDiceButtons = settings.ShowDiceButtons;
    }

    private bool _showDiceButtons;

    /// <summary>Würfelknöpfe im Charakterbogen (Talente, Zauber, Angriffe).</summary>
    public bool ShowDiceButtons
    {
        get => _showDiceButtons;
        set
        {
            if (!SetProperty(ref _showDiceButtons, value)) return;
            var settings = _store.Load();
            settings.ShowDiceButtons = value;
            _store.Save(settings);
        }
    }

    private string _theme;

    /// <summary>Heldendokument: hell, Papier und Ochsenblut (Standard).</summary>
    public bool IsHeldendokument
    {
        get => _theme == AppTheme.Heldendokument;
        set { if (value) SetTheme(AppTheme.Heldendokument); }
    }

    /// <summary>Nachtlager: dunkel mit Bernstein.</summary>
    public bool IsNachtlager
    {
        get => _theme == AppTheme.Nachtlager;
        set { if (value) SetTheme(AppTheme.Nachtlager); }
    }

    public string Theme => _theme;

    private void SetTheme(string theme)
    {
        if (_theme == theme) return;
        _theme = theme;
        var settings = _store.Load();
        settings.Theme = theme;
        _store.Save(settings);
        AppTheme.Apply(theme);
        OnPropertyChanged(nameof(IsHeldendokument));
        OnPropertyChanged(nameof(IsNachtlager));
        OnPropertyChanged(nameof(Theme));
    }

    [RelayCommand]
    private void OpenSources()
    {
        _onClose();
        _openSources();
    }

    [RelayCommand]
    private void Close() => _onClose();
}
