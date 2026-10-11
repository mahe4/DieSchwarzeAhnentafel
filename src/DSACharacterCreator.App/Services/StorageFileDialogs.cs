using System.Text;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace DSACharacterCreator.Services;

/// <summary>
/// Dateidialoge über den Avalonia-<see cref="IStorageProvider"/>. Auf Android ist eine gewählte Datei eine
/// Content-URI ohne lokalen Pfad, deshalb wird nur über Streams gelesen und geschrieben.
/// </summary>
public sealed class StorageFileDialogs : IFileDialogs
{
    /// <summary>Fenster bzw. Android-Activity, in der das <c>MainView</c> gerade angezeigt wird; setzt das MainView.</summary>
    public static TopLevel? Host { get; set; }

    private static IStorageProvider? Provider => Host?.StorageProvider;

    public async Task<string?> OpenTextAsync(string title, string typeName, string pattern)
    {
        if (Provider is not { } provider) return null;
        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(typeName) { Patterns = [pattern] }],
        });
        if (files.FirstOrDefault() is not { } file) return null;

        await using var stream = await file.OpenReadAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    public Task<bool> SaveTextAsync(string title, string suggestedName, string typeName, string extension, string content) =>
        SaveBytesAsync(title, suggestedName, typeName, extension, new UTF8Encoding(false).GetBytes(content));

    public async Task<bool> SaveBytesAsync(string title, string suggestedName, string typeName, string extension, byte[] content)
    {
        if (Provider is not { } provider) return false;
        var file = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = SuggestedName(suggestedName, extension, OperatingSystem.IsAndroid()),
            DefaultExtension = extension,
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                new FilePickerFileType(typeName) { Patterns = ["*" + extension], MimeTypes = MimeType(extension) },
            ],
        });
        if (file is null) return false;

        await using var stream = await file.OpenWriteAsync();
        // Eine bestehende, längere Datei würde sonst ihr Ende behalten.
        if (stream.CanSeek) stream.SetLength(0);
        await stream.WriteAsync(content);
        return true;
    }

    /// <summary>
    /// Avalonia hängt auf Android die Standard-Endung immer an den vorgeschlagenen Namen an („Alrik.pdf.pdf“), am Desktop
    /// nur, wenn sie fehlt. Auf Android deshalb ohne Endung vorschlagen.
    /// </summary>
    public static string SuggestedName(string name, string extension, bool android) =>
        android && name.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? name[..^extension.Length] : name;

    private static string[]? MimeType(string extension) => extension switch
    {
        ".pdf" => ["application/pdf"],
        _ when extension.EndsWith(".json", StringComparison.OrdinalIgnoreCase) => ["application/json"],
        _ => null,
    };
}
