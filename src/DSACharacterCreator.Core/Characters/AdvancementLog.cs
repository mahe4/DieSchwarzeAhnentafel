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

    /// <summary>Inhalt als Text, um unveränderte Einträge beim Abspielen wiederzuverwenden (siehe <c>AdvancementReplay</c>).</summary>
    public string Fingerprint() => $"{GetType().Name}|{Id}|{Kept}|{Content()}";

    protected abstract string Content();
}

/// <summary>Abschluss der Erschaffung: Über 10 AP hinaus übrige Start-AP verfallen (Heldenerschaffung, Schritt 11).</summary>
public sealed class CreationFinishedEntry : LogEntry
{
    public int ForfeitedAp { get; set; }

    /// <summary>Anzahl der Fehler, die beim Abschluss bewusst akzeptiert wurden (Hausregel, SL-Erlaubnis).</summary>
    public int AcceptedErrors { get; set; }

    protected override string Content() => $"{ForfeitedAp}|{AcceptedErrors}";
}

/// <summary>AP-Gutschrift, z. B. nach einem Abenteuer.</summary>
public sealed class ApAwardEntry : LogEntry
{
    public int Amount { get; set; }

    public string? Note { get; set; }

    protected override string Content() => $"{Amount}";
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

/// <summary>
/// Schlüssel in <c>CreationLayer.BoughtEnergies</c> (Erfahrung): Zukauf (Kostentabelle D, höchstens KO bzw.
/// Leiteigenschaft), Rückkauf permanent verlorener AsP/KaP (2 AP je Punkt) und sonstige permanente Verluste (z. B.
/// Artefakte; die Verluste durch Bindungen ergeben sich aus den Sonderfertigkeiten).
/// </summary>
public static class Energies
{
    public const string LifePoints = "lifePoints";
    public const string Astral = "astral";
    public const string Karma = "karma";
    public const string AstralRepurchased = "astralRepurchased";
    public const string KarmaRepurchased = "karmaRepurchased";
    public const string AstralLost = "astralLost";
    public const string KarmaLost = "karmaLost";
}

/// <summary>Steigerung eines Werts von <see cref="From"/> auf <see cref="To"/>.</summary>
public sealed class RaiseEntry : LogEntry
{
    public RaiseTarget Kind { get; set; }

    public string Target { get; set; } = "";

    public int From { get; set; }

    public int To { get; set; }

    protected override string Content() => $"{Kind}|{Target}|{From}|{To}";
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

    protected override string Content() =>
        $"{Kind}|{Entry.Id}|{Entry.Level}|{Entry.Option}|{Entry.Specification}|{Entry.ManualAp}|{Entry.Free}";
}

/// <summary>Stufe eines Eintrags aus der Erschaffung ändern (z. B. Finte I → II, Nachteil verringern).</summary>
public sealed class LevelEntry : LogEntry
{
    public EntryKind Kind { get; set; }

    public string EntryId { get; set; } = "";

    public string? Option { get; set; }

    public int From { get; set; }

    public int To { get; set; }

    protected override string Content() => $"{Kind}|{EntryId}|{Option}|{From}|{To}";
}

/// <summary>Eintrag aus der Erschaffung ablegen, vor allem einen Nachteil abbauen (mit SL-Erlaubnis).</summary>
public sealed class RemoveEntry : LogEntry
{
    public EntryKind Kind { get; set; }

    public string EntryId { get; set; } = "";

    public string? Option { get; set; }

    protected override string Content() => $"{Kind}|{EntryId}|{Option}";
}

/// <summary>Zauber, Liturgie, Zaubertrick o. Ä. aktivieren (Wert 0; Steigerungen folgen als <see cref="RaiseEntry"/>).</summary>
public sealed class LearnEntry : LogEntry
{
    public string SpellId { get; set; } = "";

    protected override string Content() => SpellId;
}

/// <summary>Vermerk ohne Wirkung, z. B. „Erschaffung korrigiert“.</summary>
public sealed class NoteEntry : LogEntry
{
    public string Text { get; set; } = "";

    protected override string Content() => "";
}

/// <summary>Sicherung beim Wiederöffnen der Erschaffung (PLAN.md Abschnitt 5.3).</summary>
public sealed class CreationSnapshot
{
    public DateTimeOffset Date { get; set; } = DateTimeOffset.UtcNow;

    public CreationLayer Creation { get; set; } = new();

    public List<LogEntry> Log { get; set; } = [];
}
