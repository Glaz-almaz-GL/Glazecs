using Glazecs.Modules.FileChunker.Abstractions.Formatters;
using Glazecs.Modules.FileChunker.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace Glazecs.Modules.FileChunker.Extensions
{
    public static class FileChunkerServiceInitializer
    {
        public static void AddFileChunkerServices(this IServiceCollection services)
        {
            services.AddLocalization();
            services.AddMudServices();
            services.AddSingleton<IHeaderFormatter, TemplateHeaderFormatter>();
            services.AddTransient<IFileChunker, FileChunker.Services.WordFileChunker>();
            services.AddTransient<IFileChunker, FileChunker.Services.PdfFileChunker>();
        }
    }
}
