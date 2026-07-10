using Glazecs.Modules.Hash.Abstractions.Interfaces;
using Glazecs.Modules.Hash.Abstractions.Models;
using System.Collections.Concurrent;

namespace Glazecs.Modules.Hash.Abstractions.Factories
{
    /// <summary>
    /// Потокобезопасная фабрика для управления провайдерами хеширования.
    /// Поддерживает регистрацию, поиск и удаление провайдеров по имени алгоритма.
    /// </summary>
    public sealed class HashProviderFactory : IHashProviderFactory
    {
        #region Events

        /// <summary>
        /// Событие возникает при успешной регистрации провайдера
        /// </summary>
        public event EventHandler<IHashProvider>? ProviderRegistered;

        /// <summary>
        /// Событие возникает при успешном удалении провайдера
        /// </summary>
        public event EventHandler<string>? ProviderUnregistered;

        /// <summary>
        /// Событие возникает при успешной регистрации категории
        /// </summary>
        public event EventHandler<HashAlgorithmCategory>? CategoryRegistered;

        /// <summary>
        /// Событие возникает при успешном удалении категории
        /// </summary>
        public event EventHandler<string>? CategoryUnregistered;

        #endregion

        #region Fields

        #region Private

        /// <summary>
        /// Потокобезопасный словарь провайдеров с регистронезависимыми ключами
        /// </summary>
        private readonly ConcurrentDictionary<string, IHashProvider> _providers =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Потокобезопасный словарь категорий алгоритмов с регистронезависимыми ключами
        /// </summary>
        private readonly ConcurrentDictionary<string, HashAlgorithmCategory> _categories =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Объект для синхронизации операций регистрации/удаления
        /// </summary>
        private readonly Lock _syncRoot = new();

        #endregion

        #region Public

        /// <inheritdoc/>
        public int Count => _providers.Count;

        /// <inheritdoc/>
        public int CategoryCount => _categories.Count;

        /// <inheritdoc/>
        public bool HasAnyProviders => !_providers.IsEmpty;

        /// <inheritdoc/>
        public bool HasAnyCategories => !_categories.IsEmpty;

        #endregion

        #endregion

        #region Constructor

        /// <inheritdoc/>
        public HashProviderFactory(IEnumerable<IHashProvider>? providers = null)
        {
            RegisterCategory(HashAlgorithmCategory.Legacy);
            RegisterCategory(HashAlgorithmCategory.SHA2);
            RegisterCategory(HashAlgorithmCategory.SHA3);
            RegisterCategory(HashAlgorithmCategory.XXH);
            RegisterCategory(HashAlgorithmCategory.XXH3);
            RegisterCategory(HashAlgorithmCategory.CRC);
            RegisterCategory(HashAlgorithmCategory.Uncategorized);

            if (providers?.Any() == true)
            {
                TryRegisterProviders(providers);
            }
        }

        #endregion

        #region Provider Management

        /// <inheritdoc/>
        public void RegisterProvider(IHashProvider provider)
        {
            ValidateProvider(provider);

            lock (_syncRoot)
            {
                RegisterProviderInternal(provider);
            }
        }

        /// <inheritdoc/>
        public bool TryRegisterProvider(IHashProvider provider)
        {
            if (provider is null || string.IsNullOrWhiteSpace(provider.Metadata.Name))
            {
                return false;
            }

            lock (_syncRoot)
            {
                if (!_categories.ContainsKey(provider.Metadata.Category))
                {
                    return false;
                }

                if (!_providers.TryAdd(provider.Metadata.Name, provider))
                {
                    return false;
                }

                OnProviderRegistered(provider);
                return true;
            }
        }

        /// <inheritdoc/>
        public int RegisterProviders(IEnumerable<IHashProvider> providers)
        {
            ArgumentNullException.ThrowIfNull(providers);

            int registeredCount = 0;
            lock (_syncRoot)
            {
                foreach (IHashProvider provider in providers)
                {
                    RegisterProviderInternal(provider);
                    registeredCount++;
                }
            }

            return registeredCount;
        }

        /// <inheritdoc/>
        public int TryRegisterProviders(IEnumerable<IHashProvider> providers)
        {
            ArgumentNullException.ThrowIfNull(providers);

            int registeredCount = 0;
            lock (_syncRoot)
            {
                registeredCount += providers.Count(TryRegisterProviderInternal);
            }
            return registeredCount;
        }

        /// <inheritdoc/>
        public void UnregisterProvider(string algorithmName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(algorithmName);

            lock (_syncRoot)
            {
                if (!_providers.TryRemove(algorithmName, out _))
                {
                    throw new KeyNotFoundException(
                        $"Провайдер с именем '{algorithmName}' не найден.");
                }

                OnProviderUnregistered(algorithmName);
            }
        }

        /// <inheritdoc/>
        public bool TryUnregisterProvider(string algorithmName)
        {
            if (string.IsNullOrWhiteSpace(algorithmName))
            {
                return false;
            }

            lock (_syncRoot)
            {
                if (!_providers.TryRemove(algorithmName, out _))
                {
                    return false;
                }

                OnProviderUnregistered(algorithmName);
                return true;
            }
        }

        /// <inheritdoc/>
        public int UnregisterProviders(IEnumerable<string> algorithmNames)
        {
            ArgumentNullException.ThrowIfNull(algorithmNames);

            int removedCount = 0;
            lock (_syncRoot)
            {
                removedCount += algorithmNames.Count(TryUnregisterProviderInternal);
            }
            return removedCount;
        }

        /// <inheritdoc/>
        public void UpdateProvider(IHashProvider provider)
        {
            ValidateProvider(provider);

            lock (_syncRoot)
            {
                if (!_categories.ContainsKey(provider.Metadata.Category))
                {
                    throw new InvalidOperationException(
                        $"Категория '{provider.Metadata.Category}' не зарегистрирована.");
                }

                IHashProvider? existingProvider = _providers.GetValueOrDefault(provider.Metadata.Name) ??
                    throw new KeyNotFoundException($"Провайдер с именем '{provider.Metadata.Name}' не зарегистрирован.");

                if (!_providers.TryUpdate(provider.Metadata.Name, provider, existingProvider))
                {
                    throw new InvalidOperationException(
                        $"Не удалось обновить провайдер '{provider.Metadata.Name}'.");
                }
            }
        }

        /// <inheritdoc/>
        public bool TryUpdateProvider(IHashProvider provider)
        {
            if (provider is null || string.IsNullOrWhiteSpace(provider.Metadata.Name))
            {
                return false;
            }

            lock (_syncRoot)
            {
                if (!_categories.ContainsKey(provider.Metadata.Category))
                {
                    return false;
                }

                IHashProvider? existingProvider = _providers.GetValueOrDefault(provider.Metadata.Name);
                return existingProvider is not null &&
                       _providers.TryUpdate(provider.Metadata.Name, provider, existingProvider);
            }
        }

        /// <inheritdoc/>
        public IHashProvider GetProvider(string algorithmName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(algorithmName);

            return !_providers.TryGetValue(algorithmName, out IHashProvider? provider)
                ? throw new KeyNotFoundException($"Провайдер с именем '{algorithmName}' не найден.")
                : provider;
        }

        /// <inheritdoc/>
        public bool TryGetProvider(string algorithmName, out IHashProvider? provider)
        {
            if (string.IsNullOrWhiteSpace(algorithmName))
            {
                provider = null;
                return false;
            }

            return _providers.TryGetValue(algorithmName, out provider);
        }

        /// <inheritdoc/>
        public HashProviderMetadata GetProviderMetadata(string algorithmName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(algorithmName);

            return !_providers.TryGetValue(algorithmName, out IHashProvider? provider)
                ? throw new KeyNotFoundException(
                    $"Провайдер с именем '{algorithmName}' не найден.")
                : provider.Metadata;
        }

        /// <inheritdoc/>
        public bool TryGetProviderMetadata(string algorithmName, out HashProviderMetadata? metadata)
        {
            if (string.IsNullOrWhiteSpace(algorithmName))
            {
                metadata = null;
                return false;
            }

            if (_providers.TryGetValue(algorithmName, out IHashProvider? provider))
            {
                metadata = provider.Metadata;
                return true;
            }

            metadata = null;
            return false;
        }

        /// <inheritdoc/>
        public bool IsProviderRegistered(string algorithmName)
        {
            return !string.IsNullOrWhiteSpace(algorithmName) && _providers.ContainsKey(algorithmName);
        }

        #endregion

        #region Category Management

        /// <inheritdoc/>
        public void RegisterCategory(HashAlgorithmCategory category)
        {
            ValidateCategory(category);

            lock (_syncRoot)
            {
                RegisterCategoryInternal(category);
            }
        }

        /// <inheritdoc/>
        public bool TryRegisterCategory(HashAlgorithmCategory category)
        {
            if (string.IsNullOrWhiteSpace(category.Name))
            {
                return false;
            }

            lock (_syncRoot)
            {
                if (!_categories.TryAdd(category.Name, category))
                {
                    return false;
                }

                OnCategoryRegistered(category);
                return true;
            }
        }

        /// <inheritdoc/>
        public void UnregisterCategory(string categoryName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);

            lock (_syncRoot)
            {
                if (_providers.Values.Any(p => p.Metadata.Category == categoryName))
                {
                    throw new InvalidOperationException(
                        $"Невозможно удалить категорию '{categoryName}': " +
                        $"существуют зарегистрированные провайдеры, использующие её.");
                }

                if (!_categories.TryRemove(categoryName, out _))
                {
                    throw new KeyNotFoundException(
                        $"Категория с именем '{categoryName}' не найдена.");
                }

                OnCategoryUnregistered(categoryName);
            }
        }

        /// <inheritdoc/>
        public bool TryUnregisterCategory(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return false;
            }

            lock (_syncRoot)
            {
                if (_providers.Values.Any(p => p.Metadata.Category == categoryName))
                {
                    return false;
                }

                if (!_categories.TryRemove(categoryName, out _))
                {
                    return false;
                }

                OnCategoryUnregistered(categoryName);
                return true;
            }
        }

        /// <inheritdoc/>
        public HashAlgorithmCategory GetCategory(string categoryName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);

            return !_categories.TryGetValue(categoryName, out HashAlgorithmCategory category)
                ? throw new KeyNotFoundException(
                    $"Категория с именем '{categoryName}' не найдена.")
                : category;
        }

        /// <inheritdoc/>
        public bool TryGetCategory(string categoryName, out HashAlgorithmCategory? category)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                category = null;
                return false;
            }

            if (_categories.TryGetValue(categoryName, out HashAlgorithmCategory result))
            {
                category = result;
                return true;
            }

            category = null;
            return false;
        }

        /// <inheritdoc/>
        public bool IsCategoryRegistered(string categoryName)
        {
            return !string.IsNullOrWhiteSpace(categoryName) && _categories.ContainsKey(categoryName);
        }

        #endregion

        #region Query Methods

        /// <inheritdoc/>
        public IReadOnlyList<IHashProvider> GetAllProviders()
        {
            return _providers.Values.ToList().AsReadOnly();
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> GetAvailableAlgorithms()
        {
            return _providers.Keys.ToList().AsReadOnly();
        }

        /// <inheritdoc/>
        public IReadOnlyList<HashAlgorithmCategory> GetAllCategories()
        {
            return _categories.Values
                .OrderBy(c => c.Priority)
                .ToList()
                .AsReadOnly();
        }

        /// <inheritdoc/>
        public IReadOnlyList<IHashProvider> GetProvidersByCategory(HashAlgorithmCategory category)
        {
            return _providers.Values
                .Where(m => m.Metadata.Category == category.Name)
                .ToList()
                .AsReadOnly();
        }

        /// <inheritdoc/>
        public IReadOnlyList<IHashProvider> GetProvidersByHashSize(int hashSizeInBits)
        {
            return _providers.Values
                .Where(p => p.Metadata.HashSizeInBits == hashSizeInBits)
                .ToList()
                .AsReadOnly();
        }

        #endregion

        #region Extended Methods

        /// <inheritdoc/>
        public void Clear()
        {
            lock (_syncRoot)
            {
                List<string> removedProviders = [.. _providers.Keys];
                _providers.Clear();

                foreach (string algorithmName in removedProviders)
                {
                    OnProviderUnregistered(algorithmName);
                }
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Внутренняя регистрация провайдера без блокировки (для batch-операций)
        /// </summary>
        private void RegisterProviderInternal(IHashProvider provider)
        {
            if (!_categories.ContainsKey(provider.Metadata.Category))
            {
                throw new InvalidOperationException(
                    $"Категория '{provider.Metadata.Category}' не зарегистрирована. " +
                    $"Сначала вызовите RegisterCategory.");
            }

            if (!_providers.TryAdd(provider.Metadata.Name, provider))
            {
                throw new InvalidOperationException(
                    $"Провайдер с именем '{provider.Metadata.Name}' уже зарегистрирован.");
            }

            OnProviderRegistered(provider);
        }

        /// <summary>
        /// Внутренняя попытка регистрации провайдера без блокировки
        /// </summary>
        private bool TryRegisterProviderInternal(IHashProvider provider)
        {
            if (provider is null || string.IsNullOrWhiteSpace(provider.Metadata.Name))
            {
                return false;
            }

            if (!_categories.ContainsKey(provider.Metadata.Category))
            {
                return false;
            }

            if (!_providers.TryAdd(provider.Metadata.Name, provider))
            {
                return false;
            }

            OnProviderRegistered(provider);
            return true;
        }

        /// <summary>
        /// Внутренняя попытка удаления провайдера без блокировки
        /// </summary>
        private bool TryUnregisterProviderInternal(string algorithmName)
        {
            if (string.IsNullOrWhiteSpace(algorithmName))
            {
                return false;
            }

            if (!_providers.TryRemove(algorithmName, out _))
            {
                return false;
            }

            OnProviderUnregistered(algorithmName);
            return true;
        }

        /// <summary>
        /// Внутренняя регистрация категории без блокировки
        /// </summary>
        private void RegisterCategoryInternal(HashAlgorithmCategory category)
        {
            if (!_categories.TryAdd(category.Name, category))
            {
                throw new InvalidOperationException(
                    $"Категория с именем '{category.Name}' уже зарегистрирована.");
            }

            OnCategoryRegistered(category);
        }

        /// <summary>
        /// Валидация провайдера перед регистрацией
        /// </summary>
        private static void ValidateProvider(IHashProvider provider)
        {
            if (provider is null)
            {
                throw new ArgumentNullException(nameof(provider), "Провайдер хеширования не может быть null");
            }

            if (string.IsNullOrWhiteSpace(provider.Metadata.Name))
            {
                throw new ArgumentException(
                    "Имя провайдера хеширования не может быть пустым или состоять только из пробелов",
                    nameof(provider));
            }

            if (provider.Metadata.HashSizeInBits <= 0)
            {
                throw new ArgumentException(
                    $"Размер хеша должен быть положительным числом. Получено: {provider.Metadata.HashSizeInBits}",
                    nameof(provider));
            }
        }

        /// <summary>
        /// Валидация категории перед регистрацией
        /// </summary>
        private static void ValidateCategory(HashAlgorithmCategory category)
        {
            if (string.IsNullOrWhiteSpace(category.Name))
            {
                throw new ArgumentException(
                    "Имя категории не может быть пустым или состоять только из пробелов",
                    nameof(category));
            }
        }

        /// <summary>
        /// Вызывает событие ProviderRegistered
        /// </summary>
        private void OnProviderRegistered(IHashProvider provider)
        {
            ProviderRegistered?.Invoke(this, provider);
        }

        /// <summary>
        /// Вызывает событие ProviderUnregistered
        /// </summary>
        private void OnProviderUnregistered(string algorithmName)
        {
            ProviderUnregistered?.Invoke(this, algorithmName);
        }

        /// <summary>
        /// Вызывает событие CategoryRegistered
        /// </summary>
        private void OnCategoryRegistered(HashAlgorithmCategory category)
        {
            CategoryRegistered?.Invoke(this, category);
        }

        /// <summary>
        /// Вызывает событие CategoryUnregistered
        /// </summary>
        private void OnCategoryUnregistered(string categoryName)
        {
            CategoryUnregistered?.Invoke(this, categoryName);
        }

        #endregion
    }
}