using Android.App;
using Android.Runtime;

#pragma warning disable IDE0130 // Пространство имен (namespace) не соответствует структуре папок.
namespace Glazecs.App.Desktop
#pragma warning restore IDE0130 // Пространство имен (namespace) не соответствует структуре папок.
{
    [Application]
    public class MainApplication(IntPtr handle, JniHandleOwnership ownership) : MauiApplication(handle, ownership)
    {
        protected override MauiApp CreateMauiApp()
        {
            return MauiProgram.CreateMauiApp();
        }
    }
}
