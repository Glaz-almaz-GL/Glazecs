using Glazecs.Modules.FileChunker.Abstractions.Models;

namespace Glazecs.Modules.FileChunker.Abstractions.Interfaces
{
    /// <summary>
    /// Интерфейс для форматирования заголовков чанков на основе шаблонов.
    /// </summary>
    public interface IHeaderFormatter
    {
        /// <summary>
        /// Форматирует заголовок, подставляя значения метаданных в шаблон.
        /// </summary>
        /// <param name="template">Шаблон строки с плейсхолдерами (например, "{FileName} - Part {FilePart}").</param>
        /// <param name="context">Контекст метаданных для подстановки.</param>
        /// <returns>Отформатированная строка заголовка.</returns>
        string Format(string template, ChunkMetadataContext context);

        /// <summary>
        /// Проверяет, что шаблон содержит только валидные (известные) плейсхолдеры.
        /// </summary>
        bool ValidateTemplate(string template, out IReadOnlyCollection<string> unknownPlaceholders);
    }
}
