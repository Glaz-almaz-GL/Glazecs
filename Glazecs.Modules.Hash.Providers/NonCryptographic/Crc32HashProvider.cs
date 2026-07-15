using Glazecs.Modules.Hash.Abstractions;
using Glazecs.Modules.Hash.Abstractions.Models;
using System.IO.Hashing;

namespace Glazecs.Modules.Hash.Providers.NonCryptographic
{
    /// <summary>
    /// Реализация провайдера хеширования с использованием алгоритма CRC32
    /// </summary>
    public sealed class Crc32HashProvider : HashProvider
    {
        /// <summary>
        /// Метаданные провайдера хеширования CRC32
        /// </summary>
        public override HashProviderMetadata Metadata { get; } = new HashProviderMetadata
        {
            Name = "CRC32",
            HashSizeInBits = 32,
            Category = HashAlgorithmCategory.CRC.Name,
            IsCryptographic = false
        };

        /// <summary>
        /// Вычисление хеша для массива байтов с использованием алгоритма CRC32
        /// </summary>
        /// <param name="data">Массив байтов для хеширования</param>
        /// <returns>Массив байтов, представляющий хеш</returns>
        protected override byte[] ComputeHash(byte[] data)
        {
            return Crc32.Hash(data);
        }

        /// <summary>
        /// Вычисление хеша для потока с использованием алгоритма CRC32
        /// </summary>
        /// <param name="inputStream">Поток данных для хеширования</param>
        /// <returns>Массив байтов, представляющий хеш</returns>
        protected override byte[] ComputeHash(Stream inputStream)
        {
            Crc32 crc32 = new();
            crc32.Append(inputStream);
            return crc32.GetCurrentHash();
        }

        /// <summary>
        /// Вычисление хеша для потока асинхронно с использованием алгоритма CRC32
        /// </summary>
        /// <param name="inputStream">Поток данных для хеширования</param>
        /// <param name="cancellationToken">Токен отмены</param>
        /// <returns>Массив байтов, представляющий хеш</returns>
        protected override async Task<byte[]> ComputeHashAsync(Stream inputStream, CancellationToken cancellationToken)
        {
            Crc32 crc32 = new();
            return await ReadStreamWithTransformAsync(crc32, inputStream, cancellationToken);
        }
    }
}