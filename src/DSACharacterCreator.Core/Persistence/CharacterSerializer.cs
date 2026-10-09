using System.Text.Json;
using System.Text.Json.Serialization;
using DSACharacterCreator.Core.Characters;

namespace DSACharacterCreator.Core.Persistence;

/// <summary>Speicherformat <c>*.dsa5hero.json</c>: ein Umschlag mit Formatversion um den Charakter.</summary>
public static class CharacterSerializer
{
    public const int CurrentFormatVersion = 1;

    public const string FileExtension = ".dsa5hero.json";

    public static string Serialize(Character character) =>
        JsonSerializer.Serialize(new CharacterFile(CurrentFormatVersion, character), CharacterJsonContext.Default.CharacterFile);

    /// <exception cref="InvalidDataException">Datei ist kein lesbarer Charakter oder stammt aus einer neueren App-Version.</exception>
    public static Character Deserialize(string json)
    {
        CharacterFile? file;
        try
        {
            file = JsonSerializer.Deserialize(json, CharacterJsonContext.Default.CharacterFile);
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
}

internal sealed record CharacterFile(int FormatVersion, Character Character);

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(CharacterFile))]
internal partial class CharacterJsonContext : JsonSerializerContext;
