using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using DSACharacterCreator.Core.Characters;

namespace DSACharacterCreator.Core.Persistence;

/// <summary>Speicherformat <c>*.dsa5hero.json</c>: ein Umschlag mit Formatversion um den Charakter.</summary>
public static class CharacterSerializer
{
    // 3: Steigerungsprotokoll und Sicherung beim Korrigieren der Erschaffung (ältere Versionen würden sie verwerfen).
    public const int CurrentFormatVersion = 3;

    public const string FileExtension = ".dsa5hero.json";

    public static string Serialize(Character character) =>
        JsonSerializer.Serialize(new CharacterFile(CurrentFormatVersion, character), CharacterJsonContext.Default.CharacterFile);

    /// <exception cref="InvalidDataException">Datei ist kein lesbarer Charakter oder stammt aus einer neueren App-Version.</exception>
    public static Character Deserialize(string json)
    {
        CharacterFile? file;
        try
        {
            var node = JsonNode.Parse(json) as JsonObject
                       ?? throw new InvalidDataException("Die Datei ist keine gültige Charakterdatei.");
            Migrate(node);
            file = node.Deserialize(CharacterJsonContext.Default.CharacterFile);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Die Datei ist keine gültige Charakterdatei.", ex);
        }

        if (file?.Character is null)
            throw new InvalidDataException("Die Datei enthält keinen Charakter.");
        if (file.FormatVersion > CurrentFormatVersion)
            throw new InvalidDataException(
                $"Die Datei hat Format {file.FormatVersion}, diese App-Version kennt nur bis {CurrentFormatVersion}.");
        return file.Character;
    }

    /// <summary>Tiefe Kopie über das Speicherformat (für Sicherungen und Rückgängig).</summary>
    public static T Clone<T>(T value) where T : class
    {
        var info = (System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>)CharacterJsonContext.Default.GetTypeInfo(typeof(T))!;
        return JsonSerializer.Deserialize(JsonSerializer.Serialize(value, info), info)!;
    }

    /// <summary>Inhalt als JSON-Text (zum Vergleich, ob sich etwas geändert hat).</summary>
    public static string Fingerprint<T>(T value) where T : class =>
        JsonSerializer.Serialize(value, (System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>)CharacterJsonContext.Default.GetTypeInfo(typeof(T))!);

    /// <summary>Hebt ältere Dateien auf das aktuelle Format.</summary>
    private static void Migrate(JsonObject file)
    {
        var version = file["formatVersion"]?.GetValue<int>() ?? 0;
        if (version >= 2 || file["character"]?["creation"] is not JsonObject creation) return;
        // Format 2 → 3 braucht keine Umwandlung (neue Felder sind leer).

        // Format 1 → 2: Talente/Kampftechniken waren absolute Werte; jetzt Abweichungen vom Paketwert
        // (ohne Profession: Talente 0, Kampftechniken 6).
        if (creation["skills"] is JsonObject skills)
        {
            creation.Remove("skills");
            creation["skillAdjustments"] = skills;
        }
        if (creation["combatTechniques"] is JsonObject combat)
        {
            creation.Remove("combatTechniques");
            var adjusted = new JsonObject();
            foreach (var (id, value) in combat) adjusted[id] = (value?.GetValue<int>() ?? 6) - 6;
            creation["combatTechniqueAdjustments"] = adjusted;
        }
        file["formatVersion"] = 2;
    }
}

internal sealed record CharacterFile(int FormatVersion, Character Character);

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(CharacterFile))]
[JsonSerializable(typeof(Character))]
[JsonSerializable(typeof(CreationLayer))]
[JsonSerializable(typeof(List<LogEntry>))]
internal partial class CharacterJsonContext : JsonSerializerContext;
