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
    string? DetailsText,
    string? Note,
    string? Errata,
    IReadOnlyList<Publication> Publications)
{
    /// <summary>Liturgie, Zeremonie oder Segnung (braucht den Vorteil Geweihter).</summary>
    public bool IsKarmal => Group is SpellGroup.Liturgy or SpellGroup.Ceremony or SpellGroup.Blessing;

    /// <summary>Hat einen Fertigkeitswert (sonst: Zaubertrick, Segnung – einmal 1 AP).</summary>
    public bool HasValue => ImprovementCost is not null;

    /// <summary>Zählt zur Höchstzahl der Zauber und Liturgien bei der Erschaffung (Zaubertricks und Segnungen nicht).</summary>
    public bool CountsForCreationLimit => Group is not (SpellGroup.Cantrip or SpellGroup.Blessing);
}
