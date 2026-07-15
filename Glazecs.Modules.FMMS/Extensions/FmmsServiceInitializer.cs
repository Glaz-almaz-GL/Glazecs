using Glazecs.Modules.FMMS.Abstractions.Interfaces;
using Glazecs.Modules.FMMS.Components.Pages;
using Glazecs.Modules.FMMS.Services;
using Glazecs.Modules.Hash.Abstractions.Factories;
using Glazecs.Modules.Hash.Abstractions.Interfaces;
using Glazecs.Modules.Hash.Providers.Cryptographic.Legacy;
using Glazecs.Modules.Hash.Providers.Cryptographic.SHA2;
using Glazecs.Modules.Hash.Providers.Cryptographic.SHA3;
using Glazecs.Modules.Hash.Providers.NonCryptographic;
using Glazecs.Modules.Hash.Providers.NonCryptographic.XXH;
using Glazecs.Modules.Hash.Providers.NonCryptographic.XXH3;
using Glazecs.Shared.Core.Interfaces;
using MudBlazor.Services;

namespace Glazecs.Modules.FMMS.Extensions
{
    public static class FmmsServiceInitializer
    {
        public static void AddFmmsServices(this IServiceCollection services)
        {
            services.AddMudServices();
            services.AddSingleton<FmmsSettingsService>();
            services.AddTransient<IFileScannerService, FileScannerService>();
            services.AddTransient<IDirectoryScannerService, DirectoryScannerService>();
            services.AddTransient<IFilePageService, FilePageService>();
            services.AddSingleton<IModuleSettingsProvider, FmmsSettingsView>();

            // <-------- HASH -------->

            // Legacy hash providers
            services.AddTransient<IHashProvider, MD5HashProvider>();
            services.AddTransient<IHashProvider, SHA1HashProvider>();

            // SHA-2 hash providers
            services.AddTransient<IHashProvider, SHA256HashProvider>();
            services.AddTransient<IHashProvider, SHA384HashProvider>();
            services.AddTransient<IHashProvider, SHA512HashProvider>();

            // SHA-3 hash providers
            services.AddTransient<IHashProvider, SHA3_256HashProvider>();
            services.AddTransient<IHashProvider, SHA3_384HashProvider>();
            services.AddTransient<IHashProvider, SHA3_512HashProvider>();

            // CRC hash providers
            services.AddTransient<IHashProvider, Crc32HashProvider>();

            // XXH hash providers
            services.AddTransient<IHashProvider, XxHash32Provider>();
            services.AddTransient<IHashProvider, XxHash64Provider>();
            services.AddTransient<IHashProvider, XxHash128Provider>();

            // XXH3 hash providers
            services.AddTransient<IHashProvider, XxHash3Provider>();

            // Register the HashProviderFactory as a singleton
            services.AddSingleton<IHashProviderFactory, HashProviderFactory>();
        }
    }
}
