namespace DSACharacterCreator.Core.Content;

/// <summary>Eine Publikation, aus der Regelinhalte stammen.</summary>
/// <param name="Id">Stabile ID, z. B. <c>grw</c>.</param>
/// <param name="Name">Anzeigename.</param>
/// <param name="WikiName">Schreibweise unter „Publikation(en)“ im Regel-Wiki.</param>
/// <param name="Adult">Ü18-Werk; nicht in der Standardauswahl.</param>
public sealed record Book(string Id, string Name, string WikiName, bool Adult = false);

/// <summary>Fundstelle eines Inhalts in einer Publikation.</summary>
public sealed record Publication(string Book, int? Page = null);
