using Glazecs.Modules.Hash.Abstractions.Abstractions;
using Glazecs.Modules.Hash.Abstractions.Models;
using System.Security.Cryptography;

namespace Glazecs.Modules.Hash.Abstractions.Providers.Cryptographic.Legacy
{
    /// <summary>
    /// Реализация провайдера хеширования с использованием алгоритма SHA-1
    /// </summary>
    public sealed class SHA1HashProvider : HashProvider
    {
        /// <summary>
        /// Метаданные провайдера хеширования SHA-1
        /// </summary>
        public override HashProviderMetadata Metadata { get; } = new HashProviderMetadata
        {
            Name = "SHA-1",
            HashSizeInBits = 160,
            Category = HashAlgorithmCategory.Legacy.Name,
            IsCryptographic = true
        };

        /// <summary>
        /// Вычисление хеша для массива байтов с использованием алгоритма SHA-1
        /// </summary>
        /// <param name="data">Массив байтов для хеширования</param>
        /// <returns>Массив байтов, представляющий хеш</returns>
        protected override byte[] ComputeHash(byte[] data)
        {
            return SHA1.HashData(data);
        }

        /// <summary>
        /// Вычисление хеша для потока с использованием алгоритма SHA-1
        /// </summary>
        /// <param name="inputStream">Поток данных для хеширования</param>
        /// <returns>Массив байтов, представляющий хеш</returns>
        protected override byte[] ComputeHash(Stream inputStream)
        {
            using SHA1 sha1 = SHA1.Create();
            return sha1.ComputeHash(inputStream);
        }

        /// <summary>
        /// Вычисление хеша для потока асинхронно с использованием алгоритма SHA-1
        /// </summary>
        /// <param name="inputStream">Поток данных для хеширования</param>
        /// <param name="cancellationToken">Токен отмены</param>
        /// <returns>Массив байтов, представляющий хеш</returns>
        protected override async Task<byte[]> ComputeHashAsync(Stream inputStream, CancellationToken cancellationToken)
        {
            using SHA1 sha1 = SHA1.Create();
            return await ReadStreamWithTransformAsync(sha1, inputStream, cancellationToken);
        }
    }
}