using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;

namespace DSACharacterCreator.Android;

[Activity(
    Label = "Die Schwarze Ahnentafel",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@mipmap/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
}
