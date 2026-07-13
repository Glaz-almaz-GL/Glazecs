namespace Glazecs.Modules.FMMS.Models
{
    /// <summary>
    /// Конфигурация колонки для динамического рендеринга в MudDataGrid
    /// </summary>
    /// <param name="HeaderKey">Ключ локализации для заголовка элемента</param>
    /// <param name="ValueSelector">Функция для извлечения значения из модели элемента</param>
    public record ColumnConfig<T>(
        string HeaderKey,
        Func<T, string> ValueSelector
    );
}
