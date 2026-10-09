namespace DSACharacterCreator.Core.Characters;

/// <summary>Phase eines Charakters, siehe PLAN.md Abschnitt 5.</summary>
public enum CharacterPhase
{
    Creation,
    Advancement,
}

/// <summary>
/// Ein Held. Gespeichert werden nur Entscheidungen; alle Werte werden daraus berechnet (PLAN.md Abschnitt 4).
/// Wird mit den Meilensteinen um Erschaffungsschicht und Steigerungsprotokoll erweitert.
/// </summary>
public sealed class Character
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "";

    public CharacterPhase Phase { get; set; } = CharacterPhase.Creation;

    /// <summary>Für diesen Charakter aktivierte Publikationen (Buch-IDs aus <c>books.json</c>).</summary>
    public List<string> EnabledBooks { get; set; } = [];

    /// <summary>Entscheidungen der Heldenerschaffung.</summary>
    public CreationLayer Creation { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Erschaffungsschicht: Erfahrungsgrad und gekaufte Werte. Nicht gespeicherte Werte gelten als Startwert
/// (Eigenschaften 8, Talente 0, Kampftechniken 6).
/// </summary>
public sealed class CreationLayer
{
    /// <summary>ID des Erfahrungsgrads; <c>null</c> = Standard aus den Regeldaten.</summary>
    public string? ExperienceLevel { get; set; }

    public Dictionary<string, int> Attributes { get; set; } = [];

    public Dictionary<string, int> Skills { get; set; } = [];

    public Dictionary<string, int> CombatTechniques { get; set; } = [];
}
