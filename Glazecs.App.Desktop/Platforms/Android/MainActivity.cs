using Android.App;
using Android.Content.PM;

#pragma warning disable IDE0130 // Пространство имен (namespace) не соответствует структуре папок.
namespace Glazecs.App.Desktop
#pragma warning restore IDE0130 // Пространство имен (namespace) не соответствует структуре папок.
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
    }
}
