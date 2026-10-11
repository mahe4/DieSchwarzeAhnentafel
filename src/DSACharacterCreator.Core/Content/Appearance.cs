namespace DSACharacterCreator.Core.Content;

/// <summary>Zeile einer W20-Tabelle („schwarz (1–3)“).</summary>
public sealed record RollEntry(string Name, int Min, int Max);

/// <summary>
/// Würfeltabellen für das Aussehen einer Untergruppe (Mittelländer, Waldelf, Olochtai …). Fehlende Angaben gelten von
/// <see cref="BasedOn"/> bzw. von der Spezies.
/// </summary>
/// <param name="Cultures">Kulturen, bei denen diese Tabelle vorausgewählt wird.</param>
/// <param name="HairModifier">Zuschlag auf den Wurf der Haarfarbe (Zholochai: +8, höchstens 20).</param>
public sealed record AppearanceVariant(
    string Name,
    string? BasedOn = null,
    IReadOnlyList<string>? Cultures = null,
    IReadOnlyList<RollEntry>? Hair = null,
    IReadOnlyList<RollEntry>? Eyes = null,
    string? Height = null,
    string? Weight = null,
    int HairModifier = 0);

/// <summary>
/// Aussehen einer Spezies zum Auswürfeln: Haar-/Fell-/Schuppenfarbe und Augenfarbe (je 1W20), Körpergröße in Halbfinger
/// und Gewicht in Stein als Würfelausdruck (<see cref="Rules.DiceExpression"/>, beim Gewicht steht <c>G</c> für die Größe).
/// </summary>
/// <param name="HairRolls">Achaz würfeln die Schuppenfarbe zweimal (quergestreift).</param>
public sealed record SpeciesAppearance(
    string Species,
    IReadOnlyList<Publication> Publications,
    string HairLabel = "Haarfarbe",
    int HairRolls = 1,
    IReadOnlyList<RollEntry>? Hair = null,
    IReadOnlyList<RollEntry>? Eyes = null,
    string? Height = null,
    string? Weight = null,
    IReadOnlyList<AppearanceVariant>? Variants = null)
{
    /// <summary>Wählbare Tabellen; ohne Untergruppen genau eine mit dem Namen der Spezies.</summary>
    public IReadOnlyList<AppearanceVariant> Choices => Variants is { Count: > 0 } v ? v : [new AppearanceVariant("")];

    /// <summary>Tabellen einer Untergruppe, ergänzt um <c>BasedOn</c> und die Angaben der Spezies.</summary>
    public AppearanceVariant Resolve(string? name)
    {
        var variant = Choices.FirstOrDefault(v => v.Name == name) ?? Choices[0];
        var result = variant;
        var seen = new HashSet<string> { variant.Name };
        while (variant.BasedOn is { } baseName && seen.Add(baseName)
               && Choices.FirstOrDefault(v => v.Name == baseName) is { } parent)
        {
            result = result with
            {
                Hair = result.Hair ?? parent.Hair,
                Eyes = result.Eyes ?? parent.Eyes,
                Height = result.Height ?? parent.Height,
                Weight = result.Weight ?? parent.Weight,
            };
            variant = parent;
        }
        return result with
        {
            Hair = result.Hair ?? Hair,
            Eyes = result.Eyes ?? Eyes,
            Height = result.Height ?? Height,
            Weight = result.Weight ?? Weight,
        };
    }

    /// <summary>Vorauswahl nach der Kultur des Helden (erste passende Untergruppe).</summary>
    public string DefaultChoice(string? cultureId) =>
        Choices.FirstOrDefault(v => cultureId is not null && (v.Cultures?.Contains(cultureId) ?? false))?.Name ?? Choices[0].Name;
}

/// <summary>Startalter je Spezies: Erfahrungsgrad-ID → Würfelausdruck („16+1W3“).</summary>
public sealed record SpeciesStartAge(string Species, IReadOnlyDictionary<string, string> Ages);

public sealed record StartAgeTable(IReadOnlyList<SpeciesStartAge> Species, IReadOnlyList<Publication> Publications);

/// <summary>Geburtsmonat per W20; danach Monatsfünftel per W20 (je vier Augen) und der Tag des Fünftels per W6.</summary>
public sealed record BirthdateTable(IReadOnlyList<RollEntry> Months, IReadOnlyList<Publication> Publications);

/// <summary>
/// Typische Namen einer Kultur zum Auswürfeln. Formate mit Platzhaltern <c>{first}</c>, <c>{family}</c>, <c>{father}</c>,
/// <c>{mother}</c> und <c>{parent}</c>; ohne Format „Vorname Familienname“ bzw. nur der Vorname.
/// </summary>
/// <param name="Any">Geschlechtsunabhängige Vornamen.</param>
/// <param name="From">Kulturen, deren Namen verwendet werden (Nordaventurier, Svellttaler …).</param>
public sealed record CultureNames(
    IReadOnlyList<string>? Male = null,
    IReadOnlyList<string>? Female = null,
    IReadOnlyList<string>? Any = null,
    IReadOnlyList<string>? Family = null,
    IReadOnlyList<string>? MaleFormats = null,
    IReadOnlyList<string>? FemaleFormats = null,
    IReadOnlyList<string>? From = null);
