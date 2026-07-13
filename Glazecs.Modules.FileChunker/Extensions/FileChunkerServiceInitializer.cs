using Glazecs.Modules.FileChunker.Abstractions.Formatters;
using Glazecs.Modules.FileChunker.Abstractions.Interfaces;
using MudBlazor.Services;

namespace Glazecs.Modules.FileChunker.Extensions
{
    public static class FileChunkerServiceInitializer
    {
        public static void AddFileChunkerServices(this IServiceCollection services)
        {
            services.AddMudServices();
            services.AddSingleton<IHeaderFormatter, TemplateHeaderFormatter>();
            services.AddTransient<IFileChunker, FileChunker.Services.WordFileChunker>();
            services.AddTransient<IFileChunker, FileChunker.Services.PdfFileChunker>();
        }
    }
}
