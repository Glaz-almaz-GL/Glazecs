using Glazecs.Modules.FMMS.Abstractions.Interfaces;
using Glazecs.Modules.FMMS.Services;
using Glazecs.Modules.Hash.Abstractions.Factories;
using Glazecs.Modules.Hash.Abstractions.Interfaces;
using Glazecs.Modules.Hash.Abstractions.Providers.Cryptographic.Legacy;
using Glazecs.Modules.Hash.Abstractions.Providers.Cryptographic.SHA2;
using Glazecs.Modules.Hash.Abstractions.Providers.Cryptographic.SHA3;
using Glazecs.Modules.Hash.Abstractions.Providers.NonCryptographic;
using Glazecs.Modules.Hash.Abstractions.Providers.NonCryptographic.XXH;
using Glazecs.Modules.Hash.Abstractions.Providers.NonCryptographic.XXH3;
using MudBlazor.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace Glazecs.Modules.FMMS.Extensions
{
    public static class FmmsServiceInitializer
    {
        public static void AddFmmsServices(this IServiceCollection services)
        {
            services.AddMudServices();
            services.AddLocalization();
            services.AddSingleton<FmmsSettingsService>();
            services.AddSingleton<IFileScannerService, FileScannerService>();
            services.AddSingleton<IDirectoryScannerService, DirectoryScannerService>();
            services.AddSingleton<IFilePageService, FilePageService>();

            // <-------- HASH -------->

            // Legacy hash providers
            services.AddSingleton<IHashProvider, MD5HashProvider>();
            services.AddSingleton<IHashProvider, SHA1HashProvider>();

            // SHA-2 hash providers
            services.AddSingleton<IHashProvider, SHA256HashProvider>();
            services.AddSingleton<IHashProvider, SHA384HashProvider>();
            services.AddSingleton<IHashProvider, SHA512HashProvider>();

            // SHA-3 hash providers
            services.AddSingleton<IHashProvider, SHA3_256HashProvider>();
            services.AddSingleton<IHashProvider, SHA3_384HashProvider>();
            services.AddSingleton<IHashProvider, SHA3_512HashProvider>();

            // CRC hash providers
            services.AddSingleton<IHashProvider, Crc32HashProvider>();

            // XXH hash providers
            services.AddSingleton<IHashProvider, XxHash32Provider>();
            services.AddSingleton<IHashProvider, XxHash64Provider>();
            services.AddSingleton<IHashProvider, XxHash128Provider>();

            // XXH3 hash providers
            services.AddSingleton<IHashProvider, XxHash3Provider>();

            // Register the HashProviderFactory as a singleton
            services.AddSingleton<IHashProviderFactory, HashProviderFactory>();
        }
    }
}
