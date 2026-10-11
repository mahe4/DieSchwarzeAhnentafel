namespace DSACharacterCreator.Services;

/// <summary>Dateiauswahl zum Importieren/Exportieren. Abbruch durch den Nutzer ergibt <c>null</c> bzw. <c>false</c>.</summary>
public interface IFileDialogs
{
    Task<string?> OpenTextAsync(string title, string typeName, string pattern);

    Task<bool> SaveTextAsync(string title, string suggestedName, string typeName, string extension, string content);

    /// <summary>Speichert Binärdaten (z. B. PDF); <paramref name="content"/> wird erst nach der Dateiwahl geschrieben.</summary>
    Task<bool> SaveBytesAsync(string title, string suggestedName, string typeName, string extension, byte[] content);
}
