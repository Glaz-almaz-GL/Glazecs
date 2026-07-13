using Glazecs.Modules.FMMS.Abstractions.Models;

namespace Glazecs.Modules.FMMS.Models
{
    /// <summary>
    /// Конфигурация колонки для динамического рендеринга в MudDataGrid
    /// </summary>
    /// <param name="HeaderKey">Ключ локализации для заголовка колонки</param>
    /// <param name="ValueSelector">Функция для извлечения значения из модели файла</param>
    public record FileColumnConfig(string HeaderKey, Func<ScannedFile, string> ValueSelector) : ColumnConfig<ScannedFile>(HeaderKey, ValueSelector);
}
