namespace DSACharacterCreator.Core.Characters;

public enum CompanionKind
{
    /// <summary>Vertrautentier (SF Vertrautenbindung): Vertrautenboni, Vertrautentricks, bis zu zwei Ausbildungsaufsätze.</summary>
    Familiar,
    /// <summary>Tierischer Begleiter (Fokusregel Abrichten).</summary>
    Companion,
}

/// <summary>
/// Vertrautentier oder tierischer Begleiter eines Helden. Gespeichert werden Tierart und Entscheidungen; die Werte
/// berechnet <c>CompanionCalculator</c> aus der Tierart und dem eigenen Protokoll des Tiers.
/// </summary>
public sealed class Companion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public CompanionKind Kind { get; set; } = CompanionKind.Companion;

    public string Name { get; set; } = "";

    /// <summary>Tierart (<c>animal.…</c>); <c>null</c>, solange keine gewählt ist.</summary>
    public string? Animal { get; set; }

    /// <summary>Schwesternschaft bzw. Element, nach der die Tierart des Vertrauten gewählt wurde (Name aus den Regeldaten).</summary>
    public string? FamiliarChoice { get; set; }

    /// <summary>
    /// Eintrag im Steigerungsprotokoll des Helden, nach dem das Tier gebunden bzw. aufgenommen wurde: Nur spätere
    /// AP-Gutschriften des Helden zählen. <c>null</c> = alle Gutschriften nach der Erschaffung.
    /// </summary>
    public Guid? BoundAfter { get; set; }

    public bool Deceased { get; set; }

    /// <summary>Wann und wie das Tier gestorben ist (freier Text).</summary>
    public string? DeceasedNote { get; set; }

    public string? Gender { get; set; }

    public string? Age { get; set; }

    public string? Appearance { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Protokoll des Tiers: Bonus-AP nach Meisterentscheid und Startausbildung (<see cref="ApAwardEntry"/>), Steigerungen
    /// (<see cref="RaiseEntry"/>: Eigenschaft, Talent, Angriff, VW, LeP/AsP, Loyalität), Tricks, Ausbildungsaufsätze,
    /// Sonderfertigkeiten und Vor-/Nachteile (<see cref="AcquireEntry"/> mit <see cref="EntryKind.AnimalAbility"/>),
    /// Ablegen (<see cref="RemoveEntry"/>) und Vermerke. Kosten werden beim Abspielen berechnet.
    /// </summary>
    public List<LogEntry> Log { get; set; } = [];
}
