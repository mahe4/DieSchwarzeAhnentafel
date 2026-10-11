using System.Globalization;
using System.Text.RegularExpressions;
using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Core.Rules;

public enum NameGender { Male, Female }

/// <summary>
/// Würfelhelfer für die persönlichen Angaben (Startalter, Aussehen, Geburtsdatum, Name). Die Ergebnisse sind Vorschläge
/// für die freien Textfelder; <c>null</c>, wenn es für Spezies/Kultur keine Tabelle gibt.
/// </summary>
public sealed partial class PersonalRolls(RuleData rules, Random random)
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Startalter in Jahren nach Spezies und Erfahrungsgrad.</summary>
    public int? Age(string? speciesId, string levelId) =>
        rules.StartAge(speciesId, levelId) is { } dice ? DiceExpression.Parse(dice).Roll(random) : null;

    /// <summary>Körpergröße in Halbfinger.</summary>
    public int? Height(AppearanceVariant appearance) =>
        appearance.Height is { } h ? DiceExpression.Parse(h).Roll(random) : null;

    /// <summary>Gewicht in Stein bei gegebener Größe in Halbfinger.</summary>
    public int? Weight(AppearanceVariant appearance, int height) =>
        appearance.Weight is { } w ? Math.Max(1, DiceExpression.Parse(w).Roll(random, height)) : null;

    public string? HairColor(SpeciesAppearance species, AppearanceVariant appearance)
    {
        if (appearance.Hair is not { } table) return null;
        var colors = Enumerable.Range(0, Math.Max(1, species.HairRolls))
            .Select(_ => Pick(table, Math.Min(20, D20() + appearance.HairModifier)))
            .Distinct().ToList();
        return colors.Count == 1 ? colors[0] : string.Join(" und ", colors) + " gestreift";
    }

    public string? EyeColor(AppearanceVariant appearance) => appearance.Eyes is { } table ? Pick(table, D20()) : null;

    /// <summary>„17. Phex“: Monat per W20, Monatsfünftel per W20, Tag des Fünftels per W6.</summary>
    public string Birthdate()
    {
        var month = Pick(rules.Birthdates.Months, D20());
        var fifth = (D20() - 1) / 4;
        var day = fifth * 6 + random.Next(1, 7);
        return $"{day}. {month}";
    }

    /// <summary>Name nach den typischen Namen der Kultur; ohne Geschlecht wird es ausgewürfelt.</summary>
    public string? Name(CultureDefinition? culture, NameGender? gender)
    {
        if (culture is null) return null;
        var names = culture.Names;
        if (names?.From is { Count: > 0 } from && names.Male is null && names.Female is null && names.Any is null)
        {
            var source = rules.FindCulture(from[random.Next(from.Count)]);
            return source == culture ? null : Name(source, gender);
        }
        if (names is null) return null;

        var male = names.Male ?? names.Any;
        var female = names.Female ?? names.Any;
        gender ??= (male, female) switch
        {
            ({ Count: > 0 }, { Count: > 0 }) => random.Next(2) == 0 ? NameGender.Male : NameGender.Female,
            ({ Count: > 0 }, _) => NameGender.Male,
            _ => NameGender.Female,
        };
        var firstNames = gender == NameGender.Male ? male : female;
        if (firstNames is not { Count: > 0 }) return null;

        var formats = gender == NameGender.Male ? names.MaleFormats : names.FemaleFormats;
        var format = formats is { Count: > 0 }
            ? formats[random.Next(formats.Count)]
            : names.Family is { Count: > 0 } ? "{first} {family}" : "{first}";
        var first = Any(firstNames)!;
        return format
            .Replace("{first}", first)
            .Replace("{family}", Any(names.Family) ?? "")
            .Replace("{father}", Any(male, first) ?? "")
            .Replace("{mother}", Any(female, first) ?? "")
            .Replace("{parent}", (random.Next(2) == 0 ? Any(male, first) : Any(female, first)) ?? Any(male ?? female, first) ?? "")
            .Trim();
    }

    /// <summary>Geschlecht aus dem freien Textfeld („weiblich“, „m“ …); sonst <c>null</c>.</summary>
    public static NameGender? GenderOf(string? text) => text?.Trim().ToLowerInvariant() switch
    {
        null or "" => null,
        var t when t.StartsWith('w') || t.StartsWith('f') => NameGender.Female,
        var t when t.StartsWith('m') => NameGender.Male,
        _ => null,
    };

    /// <summary>„1,78 Schritt“.</summary>
    public static string FormatHeight(int halfFingers) => $"{(halfFingers / 100m).ToString("0.00", German)} Schritt";

    public static string FormatWeight(int stone) => $"{stone} Stein";

    /// <summary>Größe aus dem Textfeld in Halbfinger („1,78 Schritt“, „178“, „178 cm“); sonst <c>null</c>.</summary>
    public static int? ParseHeight(string? text)
    {
        if (text is null || NumberPattern().Match(text) is not { Success: true } m) return null;
        var value = decimal.Parse(m.Value.Replace(',', '.'), CultureInfo.InvariantCulture);
        return value switch
        {
            > 0 and < 5 => (int)Math.Round(value * 100),
            >= 30 and < 500 => (int)Math.Round(value),
            _ => null,
        };
    }

    [GeneratedRegex(@"\d+(?:[.,]\d+)?")]
    private static partial Regex NumberPattern();

    private int D20() => random.Next(1, 21);

    private static string Pick(IReadOnlyList<RollEntry> table, int roll) =>
        (table.FirstOrDefault(e => roll >= e.Min && roll <= e.Max) ?? table[^1]).Name;

    /// <summary>Zufälliger Eintrag, möglichst verschieden von <paramref name="avoid"/> (Kind heißt nicht wie der Elternteil).</summary>
    private string? Any(IReadOnlyList<string>? list, string? avoid = null)
    {
        if (list is not { Count: > 0 }) return null;
        var candidates = list.Where(n => n != avoid).ToList();
        if (candidates.Count == 0) candidates = [.. list];
        return candidates[random.Next(candidates.Count)];
    }
}
