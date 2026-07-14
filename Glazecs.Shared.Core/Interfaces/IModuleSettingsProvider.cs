namespace Glazecs.Shared.Core.Interfaces
{
    /// <summary>
    /// Интерфейс для предоставления модулем своего интерфейса настроек.
    /// Реализация этого интерфейса модулем является необязательной.
    /// </summary>
    public interface IModuleSettingsProvider
    {
        /// <summary>
        /// Название модуля для вкладки настроек
        /// </summary>
        string ModuleName { get; }

        /// <summary>
        /// Иконка для вкладки настроек
        /// </summary>
        string Icon { get; }

        /// <summary>
        /// Тип Blazor-компонента, который отрисовывает интерфейс настроек этого модуля.
        /// </summary>
        Type SettingsComponentType { get; }

        /// <summary>
        /// Сброс настроек модуля к значениям по умолчанию
        /// </summary>
        Task ResetToDefaultsAsync();
    }
}