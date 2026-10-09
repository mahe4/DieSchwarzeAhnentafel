namespace DSACharacterCreator.Services;

/// <summary>Dateiauswahl zum Importieren/Exportieren. Abbruch durch den Nutzer ergibt <c>null</c> bzw. <c>false</c>.</summary>
public interface IFileDialogs
{
    Task<string?> OpenTextAsync(string title, string typeName, string pattern);

    Task<bool> SaveTextAsync(string title, string suggestedName, string typeName, string extension, string content);
}
