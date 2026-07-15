using Glazecs.Modules.Hash.Abstractions;
using Glazecs.Modules.Hash.Abstractions.Models;
using System.Security.Cryptography;

namespace Glazecs.Modules.Hash.Providers.Cryptographic.SHA2
{
    /// <summary>
    /// Реализация провайдера хеширования с использованием алгоритма SHA-512
    /// </summary>
    public sealed class SHA512HashProvider : HashProvider
    {
        /// <summary>
        /// Метаданные провайдера хеширования SHA-512
        /// </summary>
        public override HashProviderMetadata Metadata { get; } = new HashProviderMetadata
        {
            Name = "SHA-512",
            HashSizeInBits = 512,
            Category = HashAlgorithmCategory.SHA2.Name,
            IsCryptographic = true
        };

        /// <summary>
        /// Вычисление хеша для массива байтов с использованием алгоритма SHA-512
        /// </summary>
        /// <param name="data">Массив байтов для хеширования</param>
        /// <returns>Массив байтов, представляющий хеш</returns>
        protected override byte[] ComputeHash(byte[] data)
        {
            return SHA512.HashData(data);
        }

        /// <summary>
        /// Вычисление хеша для потока с использованием алгоритма SHA-512
        /// </summary>
        /// <param name="inputStream">Поток данных для хеширования</param>
        /// <returns>Массив байтов, представляющий хеш</returns>
        protected override byte[] ComputeHash(Stream inputStream)
        {
            using SHA512 sha512 = SHA512.Create();
            return sha512.ComputeHash(inputStream);
        }

        /// <summary>
        /// Вычисление хеша для потока асинхронно с использованием алгоритма SHA-512
        /// </summary>
        /// <param name="inputStream">Поток данных для хеширования</param>
        /// <param name="cancellationToken">Токен отмены</param>
        /// <returns>Массив байтов, представляющий хеш</returns>
        protected override async Task<byte[]> ComputeHashAsync(Stream inputStream, CancellationToken cancellationToken)
        {
            using SHA512 sha512 = SHA512.Create();
            return await ReadStreamWithTransformAsync(sha512, inputStream, cancellationToken);
        }
    }
}