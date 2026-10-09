using DSACharacterCreator.Core.Characters;

namespace DSACharacterCreator.Core.Persistence;

/// <summary>Speichert Charaktere als einzelne Dateien in einem Verzeichnis.</summary>
public sealed class CharacterRepository(string directory)
{
    public string Directory { get; } = directory;

    /// <summary>Standardablage im App-Datenverzeichnis (auf Android das private Dateiverzeichnis der App).</summary>
    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DSACharacterCreator", "characters");

    private string PathFor(Guid id) => Path.Combine(Directory, id + CharacterSerializer.FileExtension);

    /// <summary>Alle lesbaren Charaktere, zuletzt geänderte zuerst. Unlesbare Dateien landen in <see cref="LoadResult.Failed"/>.</summary>
    public LoadResult LoadAll()
    {
        var characters = new List<Character>();
        var failed = new List<string>();
        if (System.IO.Directory.Exists(Directory))
        {
            foreach (var file in System.IO.Directory.EnumerateFiles(Directory, "*" + CharacterSerializer.FileExtension))
            {
                try
                {
                    characters.Add(CharacterSerializer.Deserialize(File.ReadAllText(file)));
                }
                catch (Exception ex) when (ex is InvalidDataException or IOException)
                {
                    failed.Add(file);
                }
            }
        }
        return new LoadResult(characters.OrderByDescending(c => c.ModifiedAt).ToList(), failed);
    }

    /// <summary>Speichert atomar (erst temporäre Datei, dann umbenennen) und setzt den Änderungszeitpunkt.</summary>
    public void Save(Character character)
    {
        System.IO.Directory.CreateDirectory(Directory);
        character.ModifiedAt = DateTimeOffset.UtcNow;
        var path = PathFor(character.Id);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, CharacterSerializer.Serialize(character));
        File.Move(tmp, path, overwrite: true);
    }

    public void Delete(Guid id)
    {
        var path = PathFor(id);
        if (File.Exists(path)) File.Delete(path);
    }

    public bool Exists(Guid id) => File.Exists(PathFor(id));

    /// <summary>Legt eine gespeicherte Kopie mit neuer ID an.</summary>
    public Character Duplicate(Character character)
    {
        var copy = CharacterSerializer.Deserialize(CharacterSerializer.Serialize(character));
        copy.Id = Guid.NewGuid();
        copy.Name = character.Name + " (Kopie)";
        copy.CreatedAt = DateTimeOffset.UtcNow;
        Save(copy);
        return copy;
    }

    /// <summary>Importiert eine exportierte Datei. Existiert die ID schon, bekommt der Import eine neue.</summary>
    public Character Import(string json)
    {
        var character = CharacterSerializer.Deserialize(json);
        if (Exists(character.Id)) character.Id = Guid.NewGuid();
        Save(character);
        return character;
    }
}

public sealed record LoadResult(IReadOnlyList<Character> Characters, IReadOnlyList<string> Failed);
