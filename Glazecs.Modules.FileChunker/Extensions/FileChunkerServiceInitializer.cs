using Glazecs.Modules.FileChunker.Abstractions.Formatters;
using Glazecs.Modules.FileChunker.Abstractions.Interfaces;
using Glazecs.Modules.FileChunker.Rules;
using Glazecs.Modules.FileChunker.Services;
using MudBlazor.Services;

namespace Glazecs.Modules.FileChunker.Extensions
{
    public static class FileChunkerServiceInitializer
    {
        public static void AddFileChunkerServices(this IServiceCollection services)
        {
            services.AddMudServices();
            services.AddSingleton<IHeaderFormatter, TemplateHeaderFormatter>();
            services.AddTransient<IFileChunker, WordFileChunker>();
            services.AddTransient<IFileChunker, PdfFileChunker>();
            services.AddTransient<IFileChunker, TextFileChunker>();
            services.AddTransient<IChunkRule, CSharpCommentRemovalRule>();
            services.AddTransient<IChunkRule, CSharpDocumentationCommentRemovalRule>();
            services.AddTransient<IChunkRule, DictionaryFilterRule>();
            services.AddTransient<IChunkRule, EmptyRowRule>();
            services.AddTransient<IChunkRule, RegexRowRule>();
            services.AddTransient<IChunkRule, RemovePunctuationRule>();
        }
    }
}
