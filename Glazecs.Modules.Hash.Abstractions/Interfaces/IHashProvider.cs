using Glazecs.Modules.Hash.Abstractions.Enums;
using Glazecs.Modules.Hash.Abstractions.Models;
using System.Text;

namespace Glazecs.Modules.Hash.Abstractions.Interfaces
{
    /// <summary>
    /// Контракт для провайдеров хеширования.
    /// </summary>
    public interface IHashProvider
    {
        /// <summary>
        /// Метаданные провайдера
        /// </summary>
        HashProviderMetadata Metadata { get; }

        #region Calculate Methods

        /// <summary>
        /// Вычисление хеша для строки
        /// </summary>
        string Calculate(string input, HashOutputFormat format = HashOutputFormat.LowerHex, Encoding? encoding = null);

        /// <summary>
        /// Вычисление хеша для байтового массива
        /// </summary>
        string Calculate(byte[] data, HashOutputFormat format = HashOutputFormat.LowerHex);

        /// <summary>
        /// Вычисление хеша для потока
        /// </summary>
        string Calculate(Stream inputStream, HashOutputFormat format = HashOutputFormat.LowerHex);

        /// <summary>
        /// Вычисление хеша для файла
        /// </summary>
        string CalculateFile(string filePath, HashOutputFormat format = HashOutputFormat.LowerHex);

        #endregion

        #region CalculateAsync Methods

        /// <summary>
        /// Асинхронное вычисление хеша для строки
        /// </summary>
        Task<string> CalculateAsync(string input, HashOutputFormat format = HashOutputFormat.LowerHex, Encoding? encoding = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Асинхронное вычисление хеша для байтового массива
        /// </summary>
        Task<string> CalculateAsync(byte[] data, HashOutputFormat format = HashOutputFormat.LowerHex, CancellationToken cancellationToken = default);

        /// <summary>
        /// Асинхронное вычисление хеша для потока
        /// </summary>
        Task<string> CalculateAsync(Stream inputStream, HashOutputFormat format = HashOutputFormat.LowerHex, CancellationToken cancellationToken = default);

        /// <summary>
        /// Асинхронное вычисление хеша для файла
        /// </summary>
        Task<string> CalculateFileAsync(string filePath, HashOutputFormat format = HashOutputFormat.LowerHex, CancellationToken cancellationToken = default);

        #endregion

        #region CalculateRaw Methods

        /// <summary>
        /// Получение "сырого" хеша для строки
        /// </summary>
        byte[] CalculateRaw(string input, Encoding? encoding = null);

        /// <summary>
        /// Получение "сырого" хеша для байтового массива
        /// </summary>
        byte[] CalculateRaw(byte[] data);

        /// <summary>
        /// Получение "сырого" хеша для потока
        /// </summary>
        byte[] CalculateRaw(Stream inputStream);

        /// <summary>
        /// Получение "сырого" хеша для файла
        /// </summary>
        byte[] CalculateRawFile(string filePath);

        #endregion

        #region CalculateRawAsync Methods

        /// <summary>
        /// Асинхронное получение "сырого" хеша для строки
        /// </summary>
        Task<byte[]> CalculateRawAsync(string input, Encoding? encoding = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Асинхронное получение "сырого" хеша для байтового массива
        /// </summary>
        Task<byte[]> CalculateRawAsync(byte[] data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Асинхронное получение "сырого" хеша для потока
        /// </summary>
        Task<byte[]> CalculateRawAsync(Stream inputStream, CancellationToken cancellationToken = default);

        /// <summary>
        /// Асинхронное получение "сырого" хеша для файла
        /// </summary>
        Task<byte[]> CalculateRawFileAsync(string filePath, CancellationToken cancellationToken = default);

        #endregion

        #region Verification Methods

        /// <summary>
        /// Проверка хеша строки
        /// </summary>
        bool Verify(string input, string expectedHash, HashOutputFormat format = HashOutputFormat.LowerHex, Encoding? encoding = null);

        /// <summary>
        /// Проверка хеша байтового массива
        /// </summary>
        bool Verify(byte[] data, string expectedHash, HashOutputFormat format = HashOutputFormat.LowerHex);

        /// <summary>
        /// Проверка хеша потока
        /// </summary>
        bool Verify(Stream inputStream, string expectedHash, HashOutputFormat format = HashOutputFormat.LowerHex);

        /// <summary>
        /// Проверка хеша файла
        /// </summary>
        bool VerifyFile(string filePath, string expectedHash, HashOutputFormat format = HashOutputFormat.LowerHex);

        #endregion

        #region VerificationAsync Methods

        /// <summary>
        /// Асинхронная проверка хеша строки
        /// </summary>
        Task<bool> VerifyAsync(string input, string expectedHash, HashOutputFormat format = HashOutputFormat.LowerHex, Encoding? encoding = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Асинхронная проверка хеша байтового массива
        /// </summary>
        Task<bool> VerifyAsync(byte[] data, string expectedHash, HashOutputFormat format = HashOutputFormat.LowerHex, CancellationToken cancellationToken = default);

        /// <summary>
        /// Асинхронная проверка хеша потока
        /// </summary>
        Task<bool> VerifyAsync(Stream inputStream, string expectedHash, HashOutputFormat format = HashOutputFormat.LowerHex, CancellationToken cancellationToken = default);

        /// <summary>
        /// Асинхронная проверка хеша файла
        /// </summary>
        Task<bool> VerifyFileAsync(string filePath, string expectedHash, HashOutputFormat format = HashOutputFormat.LowerHex, CancellationToken cancellationToken = default);

        #endregion
    }
}