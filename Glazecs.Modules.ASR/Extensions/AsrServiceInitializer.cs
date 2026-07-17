using Glazecs.Modules.ASR.Abstractions.Interfaces;
using Glazecs.Modules.ASR.Whisper.Resources.Languages;
using Glazecs.Modules.ASR.Whisper.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using Whisper.net.Ggml;

namespace Glazecs.Modules.ASR.Extensions
{
    public static class ASRServiceInitializer
    {
        public static void AddAsrServices(this IServiceCollection services)
        {
            services.AddMudServices();

            services.AddSingleton<ISpeechRecognitionService>(sp =>
            {
                ILogger<WhisperRecognitionService> logger = sp.GetRequiredService<ILogger<WhisperRecognitionService>>();
                IStringLocalizer<WhisperResources> localizer = sp.GetRequiredService<IStringLocalizer<WhisperResources>>();
                return new WhisperRecognitionService(GgmlType.Base, localizer, logger);
            });

            services.AddSingleton<ISpeechRecognitionService>(sp =>
            {
                ILogger<WhisperRecognitionService> logger = sp.GetRequiredService<ILogger<WhisperRecognitionService>>();
                IStringLocalizer<WhisperResources> localizer = sp.GetRequiredService<IStringLocalizer<WhisperResources>>();
                return new WhisperRecognitionService(GgmlType.Small, localizer, logger);
            });

            services.AddSingleton<ISpeechRecognitionService>(sp =>
            {
                ILogger<WhisperRecognitionService> logger = sp.GetRequiredService<ILogger<WhisperRecognitionService>>();
                IStringLocalizer<WhisperResources> localizer = sp.GetRequiredService<IStringLocalizer<WhisperResources>>();
                return new WhisperRecognitionService(GgmlType.Medium, localizer, logger);
            });

            services.AddSingleton<ISpeechRecognitionService>(sp =>
            {
                ILogger<WhisperRecognitionService> logger = sp.GetRequiredService<ILogger<WhisperRecognitionService>>();
                IStringLocalizer<WhisperResources> localizer = sp.GetRequiredService<IStringLocalizer<WhisperResources>>();
                return new WhisperRecognitionService(GgmlType.LargeV3Turbo, localizer, logger);
            });
        }
    }
}
