using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DSACharacterCreator.Views;

/// <summary>Pfaddaten („M4,4 L15,15 …“) → Geometrie, für Strichsymbole aus ViewModels.</summary>
public sealed class GeometryConverter : IValueConverter
{
    public static GeometryConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string data ? Geometry.Parse(data) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
