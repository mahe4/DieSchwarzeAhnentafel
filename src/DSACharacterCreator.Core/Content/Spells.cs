namespace DSACharacterCreator.Core.Content;

/// <summary>Art eines Zaubers bzw. einer Liturgie (Bereiche „Magie“ und „Götterwirken“ im Wiki).</summary>
public enum SpellGroup
{
    Spell,
    Ritual,
    Cantrip,
    /// <summary>Magische Handlungen: Flüche, Elfenlieder, Zaubermelodien, Zaubertänze, Animistenkräfte …</summary>
    MagicalAction,
    Liturgy,
    Ceremony,
    Blessing,
}

/// <summary>Zauber, Ritual, Zaubertrick, magische Handlung, Liturgie, Zeremonie oder Segnung. Texte aus dem Regel-Wiki.</summary>
/// <param name="Category">Kategorie im Wiki (z. B. „Zaubersprüche“, „Hexenflüche“).</param>
/// <param name="Check">Die drei Probeneigenschaften; <c>null</c> bei Zaubertricks und Segnungen.</param>
/// <param name="ImprovementCost">Steigerungsfaktor; <c>null</c> bei Zaubertricks und Segnungen (fester AP-Wert).</param>
/// <param name="Ap">Fester AP-Wert laut Eintrag (manche magischen Handlungen).</param>
/// <param name="Traditions">Verbreitung: Traditionen bzw. Gottheiten, „Allgemein“ für alle.</param>
/// <param name="Aspects">Aspekte einer Liturgie/Zeremonie (in der Verbreitung in Klammern, z. B. „Praios (Ordnung)“).</param>
public sealed record SpellDefinition(
    string Id,
    string Name,
    SpellGroup Group,
    string Category,
    string RuleText,
    string? CheckText,
    IReadOnlyList<string>? Check,
    ImprovementCost? ImprovementCost,
    int? Ap,
    string? Property,
    string? Aspect,
    string? TraditionsText,
    IReadOnlyList<string>? Traditions,
    IReadOnlyList<string>? Aspects,
    string? DetailsText,
    string? Note,
    string? Errata,
    IReadOnlyList<Publication> Publications)
{
    /// <summary>
    /// Steigerungsfaktor zur Wahl, wenn das Wiki keinen festen nennt („je nach Tierart“, „Bannzeichen wider (Wesenheit)“:
    /// A für Spinnen, B für Untote … C für Dämonen).
    /// </summary>
    public IReadOnlyList<SpellFactorOption>? FactorOptions { get; init; }

    /// <summary>Zauber- bzw. Liturgieerweiterungen (Fokusregel; Aventurische Magie S. 111, Divinarium Liturgia S. 8).</summary>
    public IReadOnlyList<SpellEnhancement> Enhancements
    {
        get => _enhancements ?? [];
        init => _enhancements = value;
    }

    private readonly IReadOnlyList<SpellEnhancement>? _enhancements;

    public SpellEnhancement? FindEnhancement(string? id) => Enhancements.FirstOrDefault(e => e.Id == id);

    /// <summary>Voraussetzungen der Art (z. B. Elfenlieder: Vorteil Zweistimmiger Gesang).</summary>
    public IReadOnlyList<Prerequisite>? Prerequisites { get; init; }

    /// <summary>Steigerungsfaktor für einen Eintrag (bei <see cref="FactorOptions"/> die gewählte Ausprägung).</summary>
    public ImprovementCost? FactorFor(string? option) =>
        ImprovementCost ?? FactorOptions?.FirstOrDefault(o => o.Name == option)?.Factor;

    /// <summary>Liturgie, Zeremonie oder Segnung (braucht den Vorteil Geweihter).</summary>
    public bool IsKarmal => Group is SpellGroup.Liturgy or SpellGroup.Ceremony or SpellGroup.Blessing;

    /// <summary>Hat einen Fertigkeitswert (sonst: Zaubertrick, Segnung – einmal 1 AP).</summary>
    public bool HasValue => ImprovementCost is not null || FactorOptions is not null;

    /// <summary>Zählt zur Höchstzahl der Zauber und Liturgien bei der Erschaffung (Zaubertricks und Segnungen nicht).</summary>
    public bool CountsForCreationLimit => Group is not (SpellGroup.Cantrip or SpellGroup.Blessing);
}

public sealed record SpellFactorOption(string Name, ImprovementCost Factor);

/// <summary>
/// Zauber- oder Liturgieerweiterung (Fokusregel, im Wiki auf der Seite des Zaubers): lernbar ab dem Fertigkeitswert
/// <paramref name="MinValue"/> für <paramref name="Ap"/> AP; manche setzen eine andere Erweiterung desselben Eintrags voraus.
/// Für Fremdzauber gibt es keine Erweiterungen.
/// </summary>
/// <param name="Id">Eindeutig innerhalb des Zaubers.</param>
/// <param name="Requires">Vorausgesetzte Erweiterungen desselben Zaubers (IDs).</param>
/// <param name="PrerequisiteText">Voraussetzung aus dem Regeltext, die keine Erweiterung ist (oder im Wiki nicht zuzuordnen).</param>
public sealed record SpellEnhancement(
    string Id,
    string Name,
    int MinValue,
    int Ap,
    string RuleText,
    IReadOnlyList<string>? Requires = null,
    string? PrerequisiteText = null);
