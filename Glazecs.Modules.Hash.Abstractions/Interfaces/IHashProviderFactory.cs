using Glazecs.Modules.Hash.Abstractions.Models;

namespace Glazecs.Modules.Hash.Abstractions.Interfaces
{
    /// <summary>
    /// Фабрика для управления провайдерами хеширования.
    /// Обеспечивает потокобезопасную регистрацию, поиск, обновление и удаление провайдеров и категорий алгоритмов.
    /// </summary>
    /// <remarks>
    /// <para>Методы без префикса <c>Try</c> бросают исключения при неудаче.</para>
    /// <para>Методы с префиксом <c>Try</c> возвращают <see cref="bool"/> и не бросают исключений при штатных отказах.</para>
    /// </remarks>
    public interface IHashProviderFactory
    {
        #region Events

        /// <summary>
        /// Событие возникает при успешной регистрации провайдера
        /// </summary>
        event EventHandler<IHashProvider>? ProviderRegistered;

        /// <summary>
        /// Событие возникает при успешном удалении провайдера
        /// </summary>
        event EventHandler<string>? ProviderUnregistered;

        /// <summary>
        /// Событие возникает при успешной регистрации категории
        /// </summary>
        event EventHandler<HashAlgorithmCategory>? CategoryRegistered;

        /// <summary>
        /// Событие возникает при успешном удалении категории
        /// </summary>
        event EventHandler<string>? CategoryUnregistered;

        #endregion

        #region Properties

        /// <summary>
        /// Получить количество зарегистрированных провайдеров
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Получить количество зарегистрированных категорий
        /// </summary>
        int CategoryCount { get; }

        /// <summary>
        /// Проверить, есть ли зарегистрированные провайдеры
        /// </summary>
        bool HasAnyProviders { get; }

        /// <summary>
        /// Проверить, есть ли зарегистрированные категории
        /// </summary>
        bool HasAnyCategories { get; }

        #endregion

        #region Provider Management

        /// <summary>
        /// Зарегистрировать новый провайдер
        /// </summary>
        /// <param name="provider">Провайдер для регистрации</param>
        /// <exception cref="System.ArgumentNullException">Если <paramref name="provider"/> равен null</exception>
        /// <exception cref="System.ArgumentException">Если метаданные провайдера некорректны</exception>
        /// <exception cref="System.InvalidOperationException">
        /// Если категория провайдера не зарегистрирована, либо провайдер с таким именем уже существует
        /// </exception>
        void RegisterProvider(IHashProvider provider);

        /// <summary>
        /// Попытаться зарегистрировать провайдер без выбрасывания исключений
        /// </summary>
        /// <param name="provider">Провайдер для регистрации</param>
        /// <returns><see langword="true"/>, если провайдер успешно зарегистрирован; иначе <see langword="false"/></returns>
        bool TryRegisterProvider(IHashProvider provider);

        /// <summary>
        /// Зарегистрировать несколько провайдеров одновременно
        /// </summary>
        /// <param name="providers">Коллекция провайдеров для регистрации</param>
        /// <returns>Количество успешно зарегистрированных провайдеров</returns>
        /// <exception cref="System.ArgumentNullException">Если <paramref name="providers"/> равен null</exception>
        /// <exception cref="System.InvalidOperationException">
        /// Если один из провайдеров не может быть зарегистрирован (категория не найдена или имя уже занято)
        /// </exception>
        int RegisterProviders(IEnumerable<IHashProvider> providers);

        /// <summary>
        /// Попытаться зарегистрировать несколько провайдеров, пропуская невалидных
        /// </summary>
        /// <param name="providers">Коллекция провайдеров для регистрации</param>
        /// <returns>Количество успешно зарегистрированных провайдеров</returns>
        /// <exception cref="System.ArgumentNullException">Если <paramref name="providers"/> равен null</exception>
        int TryRegisterProviders(IEnumerable<IHashProvider> providers);

        /// <summary>
        /// Удалить зарегистрированный провайдер
        /// </summary>
        /// <param name="algorithmName">Имя алгоритма</param>
        /// <exception cref="System.ArgumentException">
        /// Если <paramref name="algorithmName"/> равен null, пуст или состоит только из пробелов
        /// </exception>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">
        /// Если провайдер с указанным именем не найден
        /// </exception>
        void UnregisterProvider(string algorithmName);

        /// <summary>
        /// Попытаться удалить провайдер без выбрасывания исключений
        /// </summary>
        /// <param name="algorithmName">Имя алгоритма</param>
        /// <returns><see langword="true"/>, если провайдер был удалён; иначе <see langword="false"/></returns>
        bool TryUnregisterProvider(string algorithmName);

        /// <summary>
        /// Удалить несколько провайдеров одновременно
        /// </summary>
        /// <param name="algorithmNames">Коллекция имён алгоритмов для удаления</param>
        /// <returns>Количество успешно удалённых провайдеров</returns>
        /// <exception cref="System.ArgumentNullException">Если <paramref name="algorithmNames"/> равен null</exception>
        int UnregisterProviders(IEnumerable<string> algorithmNames);

        /// <summary>
        /// Обновить существующий провайдер
        /// </summary>
        /// <param name="provider">Новая версия провайдера (должна иметь то же имя)</param>
        /// <exception cref="System.ArgumentNullException">Если <paramref name="provider"/> равен null</exception>
        /// <exception cref="System.ArgumentException">Если метаданные провайдера некорректны</exception>
        /// <exception cref="System.InvalidOperationException">Если категория провайдера не зарегистрирована</exception>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">
        /// Если провайдер с указанным именем не зарегистрирован
        /// </exception>
        void UpdateProvider(IHashProvider provider);

        /// <summary>
        /// Попытаться обновить провайдер без выбрасывания исключений
        /// </summary>
        /// <param name="provider">Новая версия провайдера</param>
        /// <returns><see langword="true"/>, если провайдер успешно обновлён; иначе <see langword="false"/></returns>
        bool TryUpdateProvider(IHashProvider provider);

        /// <summary>
        /// Проверить, зарегистрирован ли провайдер с указанным именем
        /// </summary>
        /// <param name="algorithmName">Имя алгоритма</param>
        /// <returns><see langword="true"/>, если провайдер зарегистрирован</returns>
        bool IsProviderRegistered(string algorithmName);

        #endregion

        #region Category Management

        /// <summary>
        /// Зарегистрировать новую категорию алгоритмов
        /// </summary>
        /// <param name="category">Категория алгоритмов для регистрации</param>
        /// <exception cref="System.ArgumentNullException">Если <paramref name="category"/> равен null</exception>
        /// <exception cref="System.ArgumentException">Если имя категории пустое или состоит только из пробелов</exception>
        /// <exception cref="System.InvalidOperationException">Если категория с таким именем уже зарегистрирована</exception>
        void RegisterCategory(HashAlgorithmCategory category);

        /// <summary>
        /// Попытаться зарегистрировать категорию без выбрасывания исключений
        /// </summary>
        /// <param name="category">Категория алгоритмов для регистрации</param>
        /// <returns><see langword="true"/>, если категория успешно зарегистрирована; иначе <see langword="false"/></returns>
        bool TryRegisterCategory(HashAlgorithmCategory category);

        /// <summary>
        /// Удалить зарегистрированную категорию алгоритмов
        /// </summary>
        /// <param name="categoryName">Имя категории</param>
        /// <exception cref="System.ArgumentException">
        /// Если <paramref name="categoryName"/> равен null, пуст или состоит только из пробелов
        /// </exception>
        /// <exception cref="System.InvalidOperationException">
        /// Если существуют зарегистрированные провайдеры, использующие данную категорию
        /// </exception>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">Если категория не найдена</exception>
        void UnregisterCategory(string categoryName);

        /// <summary>
        /// Попытаться удалить категорию без выбрасывания исключений
        /// </summary>
        /// <param name="categoryName">Имя категории</param>
        /// <returns><see langword="true"/>, если категория была удалена; иначе <see langword="false"/></returns>
        bool TryUnregisterCategory(string categoryName);

        /// <summary>
        /// Проверить, зарегистрирована ли категория с указанным именем
        /// </summary>
        /// <param name="categoryName">Имя категории</param>
        /// <returns><see langword="true"/>, если категория зарегистрирована</returns>
        bool IsCategoryRegistered(string categoryName);

        #endregion

        #region Query Methods

        /// <summary>
        /// Получить все зарегистрированные провайдеры
        /// </summary>
        /// <returns>Снимок списка провайдеров (безопасен для итерации)</returns>
        IReadOnlyList<IHashProvider> GetAllProviders();

        /// <summary>
        /// Получить список имён всех зарегистрированных алгоритмов
        /// </summary>
        /// <returns>Снимок списка имён алгоритмов (безопасен для итерации)</returns>
        IReadOnlyList<string> GetAvailableAlgorithms();

        /// <summary>
        /// Получить все зарегистрированные категории алгоритмов
        /// </summary>
        /// <returns>Снимок списка категорий, отсортированный по приоритету (безопасен для итерации)</returns>
        IReadOnlyList<HashAlgorithmCategory> GetAllCategories();

        /// <summary>
        /// Получить список провайдеров, принадлежащих указанной категории
        /// </summary>
        /// <param name="category">Категория алгоритмов</param>
        /// <returns>Снимок списка провайдеров (безопасен для итерации)</returns>
        IReadOnlyList<IHashProvider> GetProvidersByCategory(HashAlgorithmCategory category);

        /// <summary>
        /// Получить все провайдеры с указанным размером хеша
        /// </summary>
        /// <param name="hashSizeInBits">Размер хеша в битах</param>
        /// <returns>Снимок списка провайдеров (безопасен для итерации)</returns>
        IReadOnlyList<IHashProvider> GetProvidersByHashSize(int hashSizeInBits);

        #endregion

        #region Lookup Methods

        /// <summary>
        /// Получить провайдер по имени алгоритма (регистронезависимый поиск)
        /// </summary>
        /// <param name="algorithmName">Имя алгоритма (например, "SHA-256", "MD5")</param>
        /// <returns>Найденный провайдер</returns>
        /// <exception cref="System.ArgumentException">
        /// Если <paramref name="algorithmName"/> равен null, пуст или состоит только из пробелов
        /// </exception>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">Если провайдер не найден</exception>
        IHashProvider GetProvider(string algorithmName);

        /// <summary>
        /// Попытаться получить провайдер по имени алгоритма
        /// </summary>
        /// <param name="algorithmName">Имя алгоритма</param>
        /// <param name="provider">Найденный провайдер или <see langword="null"/></param>
        /// <returns><see langword="true"/>, если провайдер найден</returns>
        bool TryGetProvider(string algorithmName, out IHashProvider? provider);

        /// <summary>
        /// Получить метаданные провайдера по имени алгоритма
        /// </summary>
        /// <param name="algorithmName">Имя алгоритма</param>
        /// <returns>Метаданные найденного провайдера</returns>
        /// <exception cref="System.ArgumentException">
        /// Если <paramref name="algorithmName"/> равен null, пуст или состоит только из пробелов
        /// </exception>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">Если провайдер не найден</exception>
        HashProviderMetadata GetProviderMetadata(string algorithmName);

        /// <summary>
        /// Попытаться получить метаданные провайдера по имени алгоритма
        /// </summary>
        /// <param name="algorithmName">Имя алгоритма</param>
        /// <param name="metadata">Найденные метаданные или <see langword="null"/></param>
        /// <returns><see langword="true"/>, если провайдер найден</returns>
        bool TryGetProviderMetadata(string algorithmName, out HashProviderMetadata? metadata);

        /// <summary>
        /// Получить категорию алгоритмов по имени
        /// </summary>
        /// <param name="categoryName">Имя категории</param>
        /// <returns>Найденная категория</returns>
        /// <exception cref="System.ArgumentException">
        /// Если <paramref name="categoryName"/> равен null, пуст или состоит только из пробелов
        /// </exception>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">Если категория не найдена</exception>
        HashAlgorithmCategory GetCategory(string categoryName);

        /// <summary>
        /// Попытаться получить категорию алгоритмов по имени
        /// </summary>
        /// <param name="categoryName">Имя категории</param>
        /// <param name="category">Найденная категория или <see langword="null"/></param>
        /// <returns><see langword="true"/>, если категория найдена</returns>
        bool TryGetCategory(string categoryName, out HashAlgorithmCategory? category);

        #endregion

        #region Extended Methods

        /// <summary>
        /// Очистить все зарегистрированные провайдеры
        /// </summary>
        /// <remarks>
        /// Категории при этом сохраняются. Для каждого удалённого провайдера генерируется событие
        /// <see cref="ProviderUnregistered"/>.
        /// </remarks>
        void Clear();

        #endregion
    }
}