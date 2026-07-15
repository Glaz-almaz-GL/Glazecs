using Glazecs.Modules.Hash.Abstractions;
using Glazecs.Modules.Hash.Abstractions.Models;
using System.Security.Cryptography;

namespace Glazecs.Modules.Hash.Providers.Cryptographic.SHA3
{
    /// <summary>
    /// Реализация провайдера хеширования с использованием алгоритма SHA-3-256
    /// </summary>
    public sealed class SHA3_256HashProvider : HashProvider
    {
        /// <summary>
        /// Метаданные провайдера хеширования SHA-3-256
        /// </summary>
        public override HashProviderMetadata Metadata { get; } = new HashProviderMetadata
        {
            Name = "SHA-3-256",
            HashSizeInBits = 256,
            Category = HashAlgorithmCategory.SHA3.Name,
            IsCryptographic = true
        };

        /// <summary>
        /// Вычисление хеша для массива байтов с использованием алгоритма SHA-3-256
        /// </summary>
        /// <param name="data">Массив байтов для хеширования</param>
        /// <returns>Массив байтов, представляющий хеш</returns>
        protected override byte[] ComputeHash(byte[] data)
        {
            return SHA3_256.HashData(data);
        }

        /// <summary>
        /// Вычисление хеша для потока с использованием алгоритма SHA-3-256
        /// </summary>
        /// <param name="inputStream">Поток данных для хеширования</param>
        /// <returns>Массив байтов, представляющий хеш</returns>
        protected override byte[] ComputeHash(Stream inputStream)
        {
            using SHA3_256 sha3_256 = SHA3_256.Create();
            return sha3_256.ComputeHash(inputStream);
        }

        /// <summary>
        /// Вычисление хеша для потока асинхронно с использованием алгоритма SHA-3-256
        /// </summary>
        /// <param name="inputStream">Поток данных для хеширования</param>
        /// <param name="cancellationToken">Токен отмены</param>
        /// <returns>Массив байтов, представляющий хеш</returns>
        protected override async Task<byte[]> ComputeHashAsync(Stream inputStream, CancellationToken cancellationToken)
        {
            using SHA3_256 sha3_256 = SHA3_256.Create();
            return await ReadStreamWithTransformAsync(sha3_256, inputStream, cancellationToken);
        }
    }
}