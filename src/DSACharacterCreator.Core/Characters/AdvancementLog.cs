using System.Text.Json.Serialization;

namespace DSACharacterCreator.Core.Characters;

/// <summary>
/// Eintrag im Steigerungsprotokoll (PLAN.md Abschnitt 4 und 5). AP-Kosten werden nicht gespeichert, sondern beim
/// Abspielen gegen den jeweils aktuellen Stand berechnet.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(CreationFinishedEntry), "creationFinished")]
[JsonDerivedType(typeof(ApAwardEntry), "apAward")]
[JsonDerivedType(typeof(RaiseEntry), "raise")]
[JsonDerivedType(typeof(AcquireEntry), "acquire")]
[JsonDerivedType(typeof(LevelEntry), "level")]
[JsonDerivedType(typeof(RemoveEntry), "remove")]
[JsonDerivedType(typeof(NoteEntry), "note")]
[JsonDerivedType(typeof(LearnEntry), "learn")]
public abstract class LogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset Date { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Bewusst trotz Konflikt behalten („SL erlaubt“, Korrektur der Erschaffung).</summary>
    public bool Kept { get; set; }
}

/// <summary>Abschluss der Erschaffung: Über 10 AP hinaus übrige Start-AP verfallen (Heldenerschaffung, Schritt 11).</summary>
public sealed class CreationFinishedEntry : LogEntry
{
    public int ForfeitedAp { get; set; }

    /// <summary>Anzahl der Fehler, die beim Abschluss bewusst akzeptiert wurden (Hausregel, SL-Erlaubnis).</summary>
    public int AcceptedErrors { get; set; }
}

/// <summary>AP-Gutschrift, z. B. nach einem Abenteuer.</summary>
public sealed class ApAwardEntry : LogEntry
{
    public int Amount { get; set; }

    public string? Note { get; set; }
}

public enum RaiseTarget
{
    Attribute,
    Skill,
    CombatTechnique,
    /// <summary>Zugekaufte Lebens-, Astral- bzw. Karmapunkte (Ziel: <see cref="Energies"/>).</summary>
    Energy,
    /// <summary>Zauber, Ritual, Liturgie … (Ziel: ID).</summary>
    Spell,
}

/// <summary>Zukaufbare Energien (Erfahrung: Kostentabelle D, Höchstwert KO bzw. Leiteigenschaft).</summary>
public static class Energies
{
    public const string LifePoints = "lifePoints";
    public const string Astral = "astral";
    public const string Karma = "karma";
}

/// <summary>Steigerung eines Werts von <see cref="From"/> auf <see cref="To"/>.</summary>
public sealed class RaiseEntry : LogEntry
{
    public RaiseTarget Kind { get; set; }

    public string Target { get; set; } = "";

    public int From { get; set; }

    public int To { get; set; }
}

public enum EntryKind
{
    Advantage,
    Disadvantage,
    SpecialAbility,
}

/// <summary>Neu erworbene Sonderfertigkeit bzw. (mit SL-Erlaubnis) Vor- oder Nachteil.</summary>
public sealed class AcquireEntry : LogEntry
{
    public EntryKind Kind { get; set; }

    public TraitEntry Entry { get; set; } = new();
}

/// <summary>Stufe eines Eintrags aus der Erschaffung ändern (z. B. Finte I → II, Nachteil verringern).</summary>
public sealed class LevelEntry : LogEntry
{
    public EntryKind Kind { get; set; }

    public string EntryId { get; set; } = "";

    public string? Option { get; set; }

    public int From { get; set; }

    public int To { get; set; }
}

/// <summary>Eintrag aus der Erschaffung ablegen, vor allem einen Nachteil abbauen (mit SL-Erlaubnis).</summary>
public sealed class RemoveEntry : LogEntry
{
    public EntryKind Kind { get; set; }

    public string EntryId { get; set; } = "";

    public string? Option { get; set; }
}

/// <summary>Zauber, Liturgie, Zaubertrick o. Ä. aktivieren (Wert 0; Steigerungen folgen als <see cref="RaiseEntry"/>).</summary>
public sealed class LearnEntry : LogEntry
{
    public string SpellId { get; set; } = "";
}

/// <summary>Vermerk ohne Wirkung, z. B. „Erschaffung korrigiert“.</summary>
public sealed class NoteEntry : LogEntry
{
    public string Text { get; set; } = "";
}

/// <summary>Sicherung beim Wiederöffnen der Erschaffung (PLAN.md Abschnitt 5.3).</summary>
public sealed class CreationSnapshot
{
    public DateTimeOffset Date { get; set; } = DateTimeOffset.UtcNow;

    public CreationLayer Creation { get; set; } = new();

    public List<LogEntry> Log { get; set; } = [];
}
