using CommunityToolkit.Maui;
using Glazecs.Modules.FileChunker.Extensions;
using Glazecs.Modules.FMMS.Extensions;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

namespace Glazecs.App.Desktop
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            MauiAppBuilder builder = MauiApp.CreateBuilder();
            builder.UseMauiApp<App>().ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            }).UseMauiCommunityToolkit();

            builder.Services.AddMauiBlazorWebView();
            builder.Services.AddMudServices();
            builder.Services.AddFmmsServices();
            builder.Services.AddFileChunkerServices();
            builder.Services.AddLogging();
            builder.Logging.SetMinimumLevel(LogLevel.Information);
#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif
            return builder.Build();
        }
    }
}