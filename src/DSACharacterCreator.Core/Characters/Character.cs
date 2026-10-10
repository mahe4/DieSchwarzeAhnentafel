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

    /// <summary>
    /// Publikationen, die beim letzten Festlegen der Auswahl bekannt waren. Später hinzugekommene Bücher gelten nach
    /// der Standardregel (alles außer Ü18) als aktiv – siehe <c>SourceSelection.ForCharacter</c>.
    /// </summary>
    public List<string> KnownBooks { get; set; } = [];

    /// <summary>Entscheidungen der Heldenerschaffung.</summary>
    public CreationLayer Creation { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Erschaffungsschicht: Erfahrungsgrad, Herkunft und gekaufte Werte.
/// Eigenschaften sind gekaufte Werte ohne Spezies-Änderung (Start 8). Talente und Kampftechniken sind
/// <em>Abweichungen</em> vom Paketwert aus Kultur und Profession (positiv = gesteigert, negativ = gesenkt).
/// </summary>
public sealed class CreationLayer
{
    /// <summary>ID des Erfahrungsgrads; <c>null</c> = Standard aus den Regeldaten.</summary>
    public string? ExperienceLevel { get; set; }

    public string? Species { get; set; }

    /// <summary>Gewählte Eigenschaft je Auswahl-Änderung der Spezies (in Reihenfolge der Auswahlen).</summary>
    public List<string?> SpeciesAttributeChoices { get; set; } = [];

    public string? Culture { get; set; }

    /// <summary>Kulturpaket gekauft (Standard bei Wahl einer Kultur).</summary>
    public bool CulturePackage { get; set; } = true;

    public string? Profession { get; set; }

    public string? ProfessionVariant { get; set; }

    /// <summary>Gewählte Kampftechnik je Auswahl der Profession (in Reihenfolge der Auswahlen).</summary>
    public List<string?> ProfessionCombatTechniqueChoices { get; set; } = [];

    public Dictionary<string, int> Attributes { get; set; } = [];

    public Dictionary<string, int> SkillAdjustments { get; set; } = [];

    public Dictionary<string, int> CombatTechniqueAdjustments { get; set; } = [];
}
