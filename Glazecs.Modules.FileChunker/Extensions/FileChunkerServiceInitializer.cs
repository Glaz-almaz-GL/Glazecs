using Glazecs.Modules.FileChunker.Abstractions.Formatters;
using Glazecs.Modules.FileChunker.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Glazecs.Modules.FileChunker.Extensions
{
    public static class FileChunkerServiceInitializer
    {
        public static void AddFileChunkerServices(this IServiceCollection services)
        {
            services.AddSingleton<IHeaderFormatter, TemplateHeaderFormatter>();
            services.AddSingleton<IFileChunker, FileChunker.Services.FileChunker>();
        }
    }
}
