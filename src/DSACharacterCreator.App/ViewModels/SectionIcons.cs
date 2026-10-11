namespace DSACharacterCreator.ViewModels;

/// <summary>
/// Pfaddaten der Strichsymbole der Hauptbereiche (24×24, ohne Füllung), am Handy statt der Beschriftung. Die Ansicht
/// wandelt sie in Geometrien um (<c>Views/GeometryConverter</c>); im ViewModel ginge das ohne Grafiksystem nicht.
/// </summary>
public static class SectionIcons
{
    public const string Basics = "M8,8 A4,4 0 1 1 16,8 A4,4 0 1 1 8,8 Z M4,21 C4,16.6 7.6,14 12,14 C16.4,14 20,16.6 20,21";
    public const string Attributes = "M5,20 L5,11 M12,20 L12,4 M19,20 L19,14";
    public const string Traits = "M12,3 L12,11 M8,7 L16,7 M7,18 L17,18";
    public const string SpecialAbilities = "M12,3 L14.7,8.6 L20.8,9.5 L16.4,13.8 L17.4,19.9 L12,17 L6.6,19.9 L7.6,13.8 L3.2,9.5 L9.3,8.6 Z";
    public const string Combat = "M4,4 L15,15 M12.5,17.5 L17.5,12.5 M15,15 L19.5,19.5 M20,4 L9,15 M6.5,12.5 L11.5,17.5 M9,15 L4.5,19.5";
    public const string Skills = "M10,6 L20,6 M10,12 L20,12 M10,18 L20,18 M3.5,6 L5,7.5 L7.5,5 M3.5,12 L5,13.5 L7.5,11 M3.5,18 L5,19.5 L7.5,17";
    public const string Magic = "M11,2 C11.8,7 13,8.2 18,9 C13,9.8 11.8,11 11,16 C10.2,11 9,9.8 4,9 C9,8.2 10.2,7 11,2 Z M19,15 C19.3,16.8 19.7,17.2 21.5,17.5 C19.7,17.8 19.3,18.2 19,20 C18.7,18.2 18.3,17.8 16.5,17.5 C18.3,17.2 18.7,16.8 19,15 Z";
    public const string Equipment = "M6,21 L6,10 A6,6 0 0 1 18,10 L18,21 Z M9,5 L9,3 L15,3 L15,5 M9,15 L15,15";
    public const string Sheet = "M6,3 L15,3 L19,7 L19,21 L6,21 Z M14,3 L14,8 L19,8 M9,13 L16,13 M9,17 L16,17";
}
