namespace DSACharacterCreator.Core.Characters;

/// <summary>Phase eines Charakters, siehe PLAN.md Abschnitt 5.</summary>
public enum CharacterPhase
{
    Creation,
    Advancement,
}

/// <summary>
/// Ein Held. Gespeichert werden nur Entscheidungen; alle Werte werden daraus berechnet (PLAN.md Abschnitt 4):
/// die Erschaffungsschicht und danach das Steigerungsprotokoll.
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

    /// <summary>Steigerungsprotokoll (ab dem Abschluss der Erschaffung).</summary>
    public List<LogEntry> Log { get; set; } = [];

    /// <summary>Sicherung, solange die Erschaffung korrigiert wird; <c>null</c> sonst.</summary>
    public CreationSnapshot? CreationSnapshot { get; set; }

    /// <summary>Wird gerade die Erschaffung eines bereits abgeschlossenen Helden korrigiert?</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsCorrectingCreation => Phase == CharacterPhase.Creation && CreationSnapshot is not null;

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

    /// <summary>Gewählte Vor- und Nachteile (ohne die automatischen der Spezies).</summary>
    public List<TraitEntry> Traits { get; set; } = [];

    /// <summary>Gewählte Sonderfertigkeiten (Option = Sprache, Schrift, Talent … je nach Sonderfertigkeit).</summary>
    public List<TraitEntry> SpecialAbilities { get; set; } = [];

    public Dictionary<string, int> SkillAdjustments { get; set; } = [];

    public Dictionary<string, int> CombatTechniqueAdjustments { get; set; } = [];

    /// <summary>
    /// Zugekaufte LeP/AsP/KaP (<see cref="Energies"/>). Nur über das Steigerungsprotokoll, in der Erschaffung nicht
    /// vorgesehen (Heldenerschaffung, Schritt 8).
    /// </summary>
    public Dictionary<string, int> BoughtEnergies { get; set; } = [];

    /// <summary>Gibt es eigene Anpassungen an Talenten oder Kampftechniken, die ein Vorlagenwechsel verwerfen würde?</summary>
    public bool HasValueAdjustments => SkillAdjustments.Count > 0 || CombatTechniqueAdjustments.Count > 0;

    // Kultur und Profession bilden gemeinsam die Vorauswahl der Talente und Kampftechniken. Ändert sich eine der beiden
    // Vorlagen, werden alle eigenen Anpassungen verworfen, sodass die Werte der neu berechneten Vorauswahl entsprechen.
    // Eigenschaften und Spezies bleiben unverändert. Eine Änderung auf den bisherigen Wert bewirkt nichts.

    /// <summary>Wechselt (oder entfernt) die Kultur; das Kulturpaket wird dabei wieder als Vorlage übernommen.</summary>
    public void SetCulture(string? cultureId)
    {
        if (cultureId == Culture) return;
        Culture = cultureId;
        CulturePackage = true;
        ResetValueAdjustments();
    }

    public void SetCulturePackage(bool use)
    {
        if (use == CulturePackage) return;
        CulturePackage = use;
        ResetValueAdjustments();
    }

    /// <summary>Wechselt (oder entfernt) die Profession; Variante und Auswahlen werden ebenfalls zurückgesetzt.</summary>
    public void SetProfession(string? professionId)
    {
        if (professionId == Profession) return;
        Profession = professionId;
        ProfessionVariant = null;
        ProfessionCombatTechniqueChoices.Clear();
        ResetValueAdjustments();
    }

    public void SetProfessionVariant(string? variantId)
    {
        if (variantId == ProfessionVariant) return;
        ProfessionVariant = variantId;
        ResetValueAdjustments();
    }

    /// <summary>Tiefe Kopie; Einträge behalten <see cref="TraitEntry.Key"/> und <see cref="TraitEntry.LogEntryId"/>.</summary>
    public CreationLayer Clone()
    {
        var copy = (CreationLayer)MemberwiseClone();
        copy.SpeciesAttributeChoices = [.. SpeciesAttributeChoices];
        copy.ProfessionCombatTechniqueChoices = [.. ProfessionCombatTechniqueChoices];
        copy.Attributes = new Dictionary<string, int>(Attributes);
        copy.Traits = Traits.Select(t => t.Clone()).ToList();
        copy.SpecialAbilities = SpecialAbilities.Select(t => t.Clone()).ToList();
        copy.SkillAdjustments = new Dictionary<string, int>(SkillAdjustments);
        copy.CombatTechniqueAdjustments = new Dictionary<string, int>(CombatTechniqueAdjustments);
        copy.BoughtEnergies = new Dictionary<string, int>(BoughtEnergies);
        return copy;
    }

    private void ResetValueAdjustments()
    {
        SkillAdjustments.Clear();
        CombatTechniqueAdjustments.Clear();
    }
}

/// <summary>Ein gewählter Vor- oder Nachteil.</summary>
public sealed class TraitEntry
{
    public string Id { get; set; } = "";

    /// <summary>Stufe bei Vor-/Nachteilen mit Stufen (Standard 1).</summary>
    public int? Level { get; set; }

    /// <summary>Gewählte Fertigkeit/Kampftechnik/Sprache/Schrift (ID) bzw. Ausprägung (Name).</summary>
    public string? Option { get; set; }

    /// <summary>Freie Präzisierung, z. B. „Spinnen“ bei „Angst vor …“.</summary>
    public string? Specification { get; set; }

    /// <summary>Selbst eingetragener AP-Wert, wenn das Wiki keinen festen Wert nennt.</summary>
    public int? ManualAp { get; set; }

    /// <summary>Herkunft aus einer Vorlage („culture“, „profession“); <c>null</c> = selbst hinzugefügt.</summary>
    public string? Source { get; set; }

    /// <summary>Kostenlos (z. B. Muttersprache und Ortskenntnis aus der Kultur).</summary>
    public bool Free { get; set; }

    /// <summary>Beim Abspielen gesetzt: Protokolleintrag, der diesen Eintrag erworben hat; <c>null</c> = aus der Erschaffung.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Guid? LogEntryId { get; set; }

    /// <summary>Kennung zum Abgleich von Arbeitskopien (Steigerungsphase); bleibt beim <see cref="Clone"/> erhalten.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Guid Key { get; set; } = Guid.NewGuid();

    public TraitEntry Clone() => (TraitEntry)MemberwiseClone();
}
