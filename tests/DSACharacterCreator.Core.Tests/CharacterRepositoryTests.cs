using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Persistence;

namespace DSACharacterCreator.Core.Tests;

public sealed class CharacterRepositoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dsa-repo-tests-" + Guid.NewGuid());
    private readonly CharacterRepository _repo;

    public CharacterRepositoryTests()
    {
        _repo = new CharacterRepository(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void LoadAll_EmptyWhenDirectoryMissing()
    {
        var result = _repo.LoadAll();

        Assert.Empty(result.Characters);
        Assert.Empty(result.Failed);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsCharacter()
    {
        var character = new Character
        {
            Name = "Alrik",
            Phase = CharacterPhase.Advancement,
            EnabledBooks = ["grw", "wdv"],
        };

        _repo.Save(character);
        var loaded = Assert.Single(_repo.LoadAll().Characters);

        Assert.Equal(character.Id, loaded.Id);
        Assert.Equal("Alrik", loaded.Name);
        Assert.Equal(CharacterPhase.Advancement, loaded.Phase);
        Assert.Equal(["grw", "wdv"], loaded.EnabledBooks);
    }

    [Fact]
    public void LoadAll_OrdersByLastModified()
    {
        var older = new Character { Name = "Alt" };
        var newer = new Character { Name = "Neu" };
        _repo.Save(older);
        _repo.Save(newer);
        _repo.Save(older);

        Assert.Equal(["Alt", "Neu"], _repo.LoadAll().Characters.Select(c => c.Name));
    }

    [Fact]
    public void LoadAll_ReportsUnreadableFiles()
    {
        _repo.Save(new Character { Name = "Gut" });
        File.WriteAllText(Path.Combine(_dir, "kaputt" + CharacterSerializer.FileExtension), "{ nicht json");

        var result = _repo.LoadAll();

        Assert.Single(result.Characters);
        Assert.Single(result.Failed);
    }

    [Fact]
    public void Delete_RemovesFile()
    {
        var character = new Character();
        _repo.Save(character);

        _repo.Delete(character.Id);

        Assert.False(_repo.Exists(character.Id));
        Assert.Empty(_repo.LoadAll().Characters);
    }

    [Fact]
    public void Duplicate_CreatesSavedCopyWithNewId()
    {
        var original = new Character { Name = "Alrik", EnabledBooks = ["grw"] };
        _repo.Save(original);

        var copy = _repo.Duplicate(original);

        Assert.NotEqual(original.Id, copy.Id);
        Assert.Equal("Alrik (Kopie)", copy.Name);
        Assert.Equal(["grw"], copy.EnabledBooks);
        Assert.Equal(2, _repo.LoadAll().Characters.Count);
    }

    [Fact]
    public void Import_AssignsNewIdWhenCharacterExists()
    {
        var character = new Character { Name = "Alrik" };
        _repo.Save(character);

        var imported = _repo.Import(CharacterSerializer.Serialize(character));

        Assert.NotEqual(character.Id, imported.Id);
        Assert.Equal(2, _repo.LoadAll().Characters.Count);
    }

    [Fact]
    public void Deserialize_RejectsNewerFormatAndGarbage()
    {
        var json = CharacterSerializer.Serialize(new Character())
            .Replace($"\"formatVersion\": {CharacterSerializer.CurrentFormatVersion}", "\"formatVersion\": 999");

        Assert.Throws<InvalidDataException>(() => CharacterSerializer.Deserialize(json));
        Assert.Throws<InvalidDataException>(() => CharacterSerializer.Deserialize("[]"));
        Assert.Throws<InvalidDataException>(() => CharacterSerializer.Deserialize("{}"));
    }

    [Fact]
    public void FormatVersion1_IsMigrated()
    {
        var json = """
            {
              "formatVersion": 1,
              "character": {
                "id": "6f1f2a2e-1111-4a0e-9c9e-1a2b3c4d5e6f",
                "name": "Alt",
                "creation": {
                  "attributes": { "MU": 12 },
                  "skills": { "skill.klettern": 5 },
                  "combatTechniques": { "ct.dolche": 9 }
                }
              }
            }
            """;

        var c = CharacterSerializer.Deserialize(json);

        Assert.Equal(12, c.Creation.Attributes["MU"]);
        Assert.Equal(5, c.Creation.SkillAdjustments["skill.klettern"]);
        Assert.Equal(3, c.Creation.CombatTechniqueAdjustments["ct.dolche"]);
    }
}
