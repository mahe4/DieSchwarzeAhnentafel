using Avalonia.Platform;
using DSACharacterCreator.Core.Sheet;
using DSACharacterCreator.Pdf;
using SkiaSharp;

namespace DSACharacterCreator.Services;

/// <summary>Charakterbogen als PDF, mit der Schrift der Oberfläche (Inter).</summary>
public static class PdfExport
{
    private static readonly Lazy<PdfFonts> Fonts = new(LoadFonts);

    public static byte[] Create(CharacterSheet sheet)
    {
        using var stream = new MemoryStream();
        SheetPdf.Write(sheet, stream, Fonts.Value);
        return stream.ToArray();
    }

    private static PdfFonts LoadFonts()
    {
        try
        {
            return new PdfFonts(Typeface("Inter-Regular.ttf"), Typeface("Inter-SemiBold.ttf"));
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            return PdfFonts.System();
        }
    }

    private static SKTypeface Typeface(string file)
    {
        using var stream = AssetLoader.Open(new Uri("avares://Avalonia.Fonts.Inter/Assets/" + file));
        using var data = SKData.Create(stream);
        return SKTypeface.FromData(data) ?? throw new InvalidOperationException($"Schrift {file} nicht lesbar.");
    }
}
