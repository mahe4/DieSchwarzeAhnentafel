using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.DarkAid;
using DSACharacterCreator.Core.Optolith;
using DSACharacterCreator.Core.Persistence;

namespace DSACharacterCreator.ViewModels;

/// <summary>Hauptmenü: alle gespeicherten Helden.</summary>
public partial class CharacterListViewModel : ViewModelBase
{
    private const string FileTypeName = "DSA5-Held";

    private readonly MainViewModel _shell;

    public CharacterListViewModel(MainViewModel shell)
    {
        _shell = shell;
        Reload();
    }

    public ObservableCollection<CharacterCardViewModel> Characters { get; } = [];

    public bool HasCharacters => Characters.Count > 0;

    public string StorageLocation => _shell.Repository.Directory;

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    /// <summary>Held, dessen Löschung gerade bestätigt werden muss.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDeletePending), nameof(PendingDeleteText))]
    public partial CharacterCardViewModel? PendingDelete { get; set; }

    public bool IsDeletePending => PendingDelete is not null;

    public string PendingDeleteText => PendingDelete is null
        ? ""
        : $"„{PendingDelete.Name}“ wird endgültig gelöscht. Das lässt sich nicht rückgängig machen.";

    private void Reload()
    {
        var result = _shell.Repository.LoadAll();
        Characters.Clear();
        foreach (var character in result.Characters) Characters.Add(new CharacterCardViewModel(character, this));
        OnPropertyChanged(nameof(HasCharacters));
        if (result.Failed.Count > 0)
            StatusText = $"{result.Failed.Count} Datei(en) im Speicherordner konnten nicht gelesen werden.";
    }

    [RelayCommand]
    private void New()
    {
        var character = new Character();
        SourceSelection.ForSettings(_shell.Settings.Load(), _shell.Catalog).ApplyTo(character, _shell.Catalog);
        _shell.Repository.Save(character);
        _shell.OpenCharacter(character);
    }

    public void Open(CharacterCardViewModel card) => _shell.OpenCharacter(card.Character);

    public void Duplicate(CharacterCardViewModel card)
    {
        var copy = _shell.Repository.Duplicate(card.Character);
        Reload();
        StatusText = $"„{CharacterCardViewModel.DisplayName(copy)}“ angelegt.";
    }

    public void RequestDelete(CharacterCardViewModel card) => PendingDelete = card;

    [RelayCommand]
    private void ConfirmDelete()
    {
        if (PendingDelete is not { } card) return;
        _shell.Repository.Delete(card.Character.Id);
        PendingDelete = null;
        Reload();
        StatusText = $"„{card.Name}“ gelöscht.";
    }

    [RelayCommand]
    private void CancelDelete() => PendingDelete = null;

    /// <summary>Schließt Löschbestätigung, Standardquellen bzw. Einstellungen, falls offen (Android-Zurück-Taste).</summary>
    public bool CloseOverlay()
    {
        if (Sources is not null)
        {
            Sources = null;
            return true;
        }
        if (Settings is not null)
        {
            Settings = null;
            return true;
        }
        if (PendingDelete is null) return false;
        PendingDelete = null;
        return true;
    }

    /// <summary>Offene Auswahl der Standardquellen; <c>null</c> = geschlossen.</summary>
    [ObservableProperty]
    public partial SourcesViewModel? Sources { get; private set; }

    /// <summary>Offene Einstellungen; <c>null</c> = geschlossen.</summary>
    [ObservableProperty]
    public partial AppSettingsViewModel? Settings { get; private set; }

    [RelayCommand]
    private void OpenSettings() => Settings = new AppSettingsViewModel(_shell.Settings, OpenSources, () => Settings = null);

    [RelayCommand]
    private void OpenSources()
    {
        var settings = _shell.Settings.Load();
        Sources = new SourcesViewModel("Standardquellen",
            "Diese Publikationen sind bei neuen Helden aktiv. Jeder Held kann seine Quellen später unter „Grundlagen“ " +
            "selbst festlegen. Ü18-Werke werden nie automatisch aktiviert.",
            _shell.Catalog, _shell.BookUsage, SourceSelection.ForSettings(settings, _shell.Catalog),
            selection =>
            {
                selection.ApplyTo(settings, _shell.Catalog);
                _shell.Settings.Save(settings);
            },
            () => Sources = null);
    }

    [RelayCommand]
    private async Task Import()
    {
        // Dateien dieser App und Optolith-Helden (beide *.json) sowie Dark-Aid-Helden (*.tdc)
        var json = await _shell.FileDialogs.OpenTextAsync("Held importieren", FileTypeName + ", Optolith- oder Dark-Aid-Held",
            "*.json", "*" + DarkAidImport.FileExtension);
        if (json is null) return;
        try
        {
            if (DarkAidImport.IsDarkAidHero(json))
            {
                var result = DarkAidImport.Read(json, _shell.Rules, _shell.DarkAid,
                    SourceSelection.ForSettings(_shell.Settings.Load(), _shell.Catalog));
                _shell.Repository.Save(result.Character);
                Reload();
                StatusText = $"„{CharacterCardViewModel.DisplayName(result.Character)}“ aus Dark Aid importiert." +
                             string.Concat(result.Notes.Select(n => "\n• " + n));
                return;
            }
            if (OptolithImport.IsOptolithHero(json))
            {
                var result = OptolithImport.Read(json, _shell.Rules, _shell.Optolith,
                    SourceSelection.ForSettings(_shell.Settings.Load(), _shell.Catalog));
                _shell.Repository.Save(result.Character);
                Reload();
                StatusText = $"„{CharacterCardViewModel.DisplayName(result.Character)}“ aus Optolith importiert." +
                             string.Concat(result.Notes.Select(n => "\n• " + n));
                return;
            }
            var character = _shell.Repository.Import(json);
            Reload();
            StatusText = $"„{CharacterCardViewModel.DisplayName(character)}“ importiert.";
        }
        catch (InvalidDataException ex)
        {
            StatusText = "Import fehlgeschlagen: " + ex.Message;
        }
    }

    public async Task Export(CharacterCardViewModel card)
    {
        var fileName = string.Concat(card.Name.Split(Path.GetInvalidFileNameChars())) + CharacterSerializer.FileExtension;
        var saved = await _shell.FileDialogs.SaveTextAsync("Held exportieren", fileName, FileTypeName,
            CharacterSerializer.FileExtension, CharacterSerializer.Serialize(card.Character));
        if (saved) StatusText = $"„{card.Name}“ exportiert.";
    }

    /// <summary>Export im Format von Optolith (nur der Endstand, ohne Protokoll und Tiergefährten).</summary>
    public async Task ExportOptolith(CharacterCardViewModel card)
    {
        OptolithExportResult result;
        try
        {
            result = OptolithExport.Write(card.Character, _shell.Rules, _shell.Optolith);
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            StatusText = "Export fehlgeschlagen: " + ex.Message;
            return;
        }
        var fileName = string.Concat(card.Name.Split(Path.GetInvalidFileNameChars())) + ".json";
        var saved = await _shell.FileDialogs.SaveTextAsync("Für Optolith exportieren", fileName, "Optolith-Held", ".json",
            result.Json);
        if (saved)
            StatusText = $"„{card.Name}“ für Optolith exportiert." + string.Concat(result.Notes.Select(n => "\n• " + n));
    }

    /// <summary>Export im Format von The Dark Aid (nur der Endstand, AP-Gutschriften als AP-Boni).</summary>
    public async Task ExportDarkAid(CharacterCardViewModel card)
    {
        DarkAidExportResult result;
        try
        {
            result = DarkAidExport.Write(card.Character, _shell.Rules, _shell.DarkAid);
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            StatusText = "Export fehlgeschlagen: " + ex.Message;
            return;
        }
        var fileName = string.Concat(card.Name.Split(Path.GetInvalidFileNameChars())) + DarkAidImport.FileExtension;
        var saved = await _shell.FileDialogs.SaveTextAsync("Für Dark Aid exportieren", fileName, "Dark-Aid-Held",
            DarkAidImport.FileExtension, result.Json);
        if (saved)
            StatusText = $"„{card.Name}“ für Dark Aid exportiert." + string.Concat(result.Notes.Select(n => "\n• " + n));
    }
}
