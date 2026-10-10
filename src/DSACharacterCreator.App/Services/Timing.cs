using System.Diagnostics;

namespace DSACharacterCreator.Services;

/// <summary>
/// Zeitmessung (am Handy im Logcat unter „DOTNET“, Präfix „[DSA-Zeit]“). Aktiv in Debug-Builds und in Release mit
/// <c>-p:DsaTiming=true</c>.
/// </summary>
public static class Timing
{
    [Conditional("DSA_TIMING")]
    public static void Log(string what, Stopwatch watch) =>
        Console.WriteLine($"[DSA-Zeit] {what}: {watch.Elapsed.TotalMilliseconds:F1} ms");
}
