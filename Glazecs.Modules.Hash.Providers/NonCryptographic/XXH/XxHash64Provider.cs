using Glazecs.Modules.Hash.Abstractions;
using Glazecs.Modules.Hash.Abstractions.Models;
using System.IO.Hashing;

namespace Glazecs.Modules.Hash.Providers.NonCryptographic.XXH
{
    /// <summary>
    /// Реализация провайдера хеширования с использованием алгоритма XxHash64
    /// </summary>
    public sealed class XxHash64Provider : HashProvider
    {
        /// <summary>
        /// Метаданные провайдера хеширования XxHash64
        /// </summary>
        public override HashProviderMetadata Metadata { get; } = new HashProviderMetadata
        {
            Name = "XxHash64",
            HashSizeInBits = 64,
            Category = HashAlgorithmCategory.XXH.Name,
            IsCryptographic = false
        };

        /// <summary>
        /// Вычисление хеша для массива байтов с использованием алгоритма XxHash64
        /// </summary>
        /// <param name="data">Массив байтов для хеширования</param>
        /// <returns>Массив байтов, представляющий хеш</returns>
        protected override byte[] ComputeHash(byte[] data)
        {
            return XxHash64.Hash(data);
        }

        /// <summary>
        /// Вычисление хеша для потока с использованием алгоритма XxHash64
        /// </summary>
        /// <param name="inputStream">Поток данных для хеширования</param>
        /// <returns>Массив байтов, представляющий хеш</returns>
        protected override byte[] ComputeHash(Stream inputStream)
        {
            XxHash64 xxHash64 = new();
            xxHash64.Append(inputStream);
            return xxHash64.GetCurrentHash();
        }

        /// <summary>
        /// Вычисление хеша для потока асинхронно с использованием алгоритма XxHash64
        /// </summary>
        /// <param name="inputStream">Поток данных для хеширования</param>
        /// <param name="cancellationToken">Токен отмены</param>
        /// <returns>Массив байтов, представляющий хеш</returns>
        protected override async Task<byte[]> ComputeHashAsync(Stream inputStream, CancellationToken cancellationToken)
        {
            XxHash64 xxHash64 = new();
            return await ReadStreamWithTransformAsync(xxHash64, inputStream, cancellationToken);
        }
    }
}