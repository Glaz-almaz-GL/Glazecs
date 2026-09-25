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
    )
    {
        /// <summary>
        /// Колонка — номер строки: при копировании в ней пишется порядковый номер среди скопированных (1, 2, 3…),
        /// а не значение из модели.
        /// </summary>
        public bool IsRowNumber { get; init; }
    }
}
