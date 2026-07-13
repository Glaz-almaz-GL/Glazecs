using Glazecs.Shared.UI.Models;

namespace Glazecs.Shared.UI.Interfaces
{
    /// <summary>
    ///
    /// </summary>
    public interface IAppSettingsService
    {
        AppSettings Settings { get; }

        /// <summary>
        /// Событие, уведомляющее об изменении настроек.
        /// </summary>
        event Action<AppSettings>? OnSettingsChanged;

        Task LoadAsync(CancellationToken cancellationToken = default);
        Task SaveAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Сбрасывает настройки к значениям по умолчанию.
        /// </summary>
        Task ResetToDefaultsAsync(CancellationToken cancellationToken = default);

    }
}
