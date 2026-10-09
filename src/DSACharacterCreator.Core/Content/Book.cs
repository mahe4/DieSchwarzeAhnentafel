namespace DSACharacterCreator.Core.Content;

/// <summary>Eine Publikation, aus der Regelinhalte stammen.</summary>
/// <param name="Id">Stabile ID, z. B. <c>grw</c>.</param>
/// <param name="Name">Anzeigename.</param>
/// <param name="WikiNames">Schreibweisen unter „Publikation(en)“ im Regel-Wiki (z. B. auch „Regelwerk (4. Auflage)“).</param>
/// <param name="Adult">Ü18-Werk; nicht in der Standardauswahl.</param>
public sealed record Book(string Id, string Name, IReadOnlyList<string> WikiNames, bool Adult = false);

/// <summary>Fundstelle eines Inhalts in einer Publikation.</summary>
public sealed record Publication(string Book, int? Page = null);
