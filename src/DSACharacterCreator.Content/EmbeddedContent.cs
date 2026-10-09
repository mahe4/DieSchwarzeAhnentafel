using System.Reflection;

namespace DSACharacterCreator.Content;

/// <summary>Zugriff auf die eingebetteten Regeldaten unter <c>data/</c>.</summary>
public static class EmbeddedContent
{
    private static readonly Assembly Assembly = typeof(EmbeddedContent).Assembly;

    /// <param name="path">Pfad relativ zu <c>data/</c>, z. B. <c>books.json</c>.</param>
    public static Stream Open(string path) =>
        Assembly.GetManifestResourceStream(path)
        ?? throw new FileNotFoundException($"Eingebettete Regeldatei '{path}' nicht gefunden.", path);

    public static IEnumerable<string> List() => Assembly.GetManifestResourceNames().Order(StringComparer.Ordinal);
}
