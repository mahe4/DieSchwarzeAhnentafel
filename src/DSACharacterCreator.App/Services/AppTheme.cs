using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace DSACharacterCreator.Services;

/// <summary>
/// Die beiden Stile der App: „Heldendokument“ (hell, Standard) und „Nachtlager“ (dunkel). Farben stehen als
/// Fluent-Paletten und Theme-Ressourcen in App.axaml/ThemeResources.axaml; Schriften und Radien setzt
/// <see cref="Apply"/>, weil sie am Stil hängen und nicht an der Farbvariante.
/// </summary>
public static class AppTheme
{
    public const string Heldendokument = "heldendokument";
    public const string Nachtlager = "nachtlager";

    private const string Fonts = "avares://DSACharacterCreator.App/Assets/Fonts#";

    public static string Normalize(string? id) => id == Nachtlager ? Nachtlager : Heldendokument;

    public static void Apply(string? id)
    {
        // Nur auf dem UI-Thread (in der App immer; Tests ohne UI-Thread erzeugen MainViewModel auch anderswo)
        if (Application.Current is not { } app || !app.CheckAccess()) return;
        var night = Normalize(id) == Nachtlager;
        app.RequestedThemeVariant = night ? ThemeVariant.Dark : ThemeVariant.Light;
        var resources = app.Resources;
        resources["BodyFont"] = new FontFamily(Fonts + (night ? "Barlow Semi Condensed" : "Source Sans 3"));
        resources["HeadingFont"] = new FontFamily(Fonts + (night ? "Barlow Semi Condensed" : "Spectral"));
        resources["ControlCornerRadius"] = new CornerRadius(night ? 10 : 3);
        resources["OverlayCornerRadius"] = new CornerRadius(night ? 12 : 4);
        resources["NavItemCornerRadius"] = new CornerRadius(night ? 14 : 0);
    }
}
