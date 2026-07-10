using Glazecs.Modules.Hash.Abstractions.Abstractions;
using Glazecs.Modules.Hash.Abstractions.Models;
using System.Security.Cryptography;

namespace Glazecs.Modules.Hash.Abstractions.Providers.Cryptographic.Legacy
{
    /// <summary>
    /// Реализация провайдера хеширования с использованием алгоритма MD5
    /// </summary>
    public sealed class MD5HashProvider : HashProvider
    {
        /// <summary>
        /// Метаданные провайдера хеширования MD5
        /// </summary>
        public override HashProviderMetadata Metadata { get; } = new HashProviderMetadata
        {
            Name = "MD5",
            HashSizeInBits = 128,
            Category = HashAlgorithmCategory.Legacy.Name,
            IsCryptographic = true
        };

        /// <summary>
        /// Вычисление хеша для массива байтов с использованием алгоритма MD5
        /// </summary>
        /// <param name="data">Массив байтов для хеширования</param>
        /// <returns>Массив байтов, представляющий хеш</returns>
        protected override byte[] ComputeHash(byte[] data)
        {
            return MD5.HashData(data);
        }

        /// <summary>
        /// Вычисление хеша для потока с использованием алгоритма MD5
        /// </summary>
        /// <param name="inputStream">Поток данных для хеширования</param>
        /// <returns>Массив байтов, представляющий хеш</returns>
        protected override byte[] ComputeHash(Stream inputStream)
        {
            using MD5 md5 = MD5.Create();
            return md5.ComputeHash(inputStream);
        }

        /// <summary>
        /// Вычисление хеша для потока асинхронно с использованием алгоритма MD5
        /// </summary>
        /// <param name="inputStream">Поток данных для хеширования</param>
        /// <param name="cancellationToken">Токен отмены</param>
        /// <returns>Массив байтов, представляющий хеш</returns>
        protected override async Task<byte[]> ComputeHashAsync(Stream inputStream, CancellationToken cancellationToken)
        {
            using MD5 md5 = MD5.Create();
            return await ReadStreamWithTransformAsync(md5, inputStream, cancellationToken);
        }
    }
}