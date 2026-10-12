using System.Text.Json;
using System.Text.Json.Serialization;

namespace DSACharacterCreator.Core.Persistence;

/// <summary>Einstellungen der App (nicht je Held).</summary>
public sealed class AppSettings
{
    /// <summary>Standardquellen für neue Helden; <c>null</c> = Standardregel (alles außer Ü18).</summary>
    public List<string>? DefaultBooks { get; set; }

    /// <summary>Beim Festlegen der Standardquellen bekannte Bücher (später hinzugekommene gelten außer Ü18 als aktiv).</summary>
    public List<string> KnownBooks { get; set; } = [];

    /// <summary>Stil der Oberfläche („heldendokument“ = Standard, „nachtlager“); <c>null</c> = Standard.</summary>
    public string? Theme { get; set; }

    /// <summary>Würfelknöpfe im Charakterbogen (Talente, Zauber, Angriffe) anzeigen.</summary>
    public bool ShowDiceButtons { get; set; } = true;
}

/// <summary>Liest und schreibt <see cref="AppSettings"/> als JSON-Datei; unlesbare Dateien ergeben die Standardwerte.</summary>
public sealed class SettingsStore(string path)
{
    public string Path { get; } = path;

    /// <summary><c>settings.json</c> neben dem Heldenordner (im App-Datenverzeichnis).</summary>
    public static SettingsStore Beside(CharacterRepository repository) =>
        new(System.IO.Path.GetFullPath(System.IO.Path.Combine(repository.Directory, "..", "settings.json")));

    public AppSettings Load()
    {
        try
        {
            return File.Exists(Path)
                ? JsonSerializer.Deserialize(File.ReadAllText(Path), SettingsJsonContext.Default.AppSettings) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var tmp = Path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings));
        File.Move(tmp, Path, overwrite: true);
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal partial class SettingsJsonContext : JsonSerializerContext;
