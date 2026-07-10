using Glazecs.Modules.Hash.Abstractions.Abstractions;
using Glazecs.Modules.Hash.Abstractions.Models;
using System.IO.Hashing;

namespace Glazecs.Modules.Hash.Abstractions.Providers.NonCryptographic.XXH3
{
    /// <summary>
    /// Реализация провайдера хеширования с использованием алгоритма XxHash3
    /// </summary>
    public sealed class XxHash3Provider : HashProvider
    {
        /// <summary>
        /// Метаданные провайдера хеширования XxHash3
        /// </summary>
        public override HashProviderMetadata Metadata { get; } = new HashProviderMetadata
        {
            Name = "XxHash3",
            HashSizeInBits = 128,
            Category = HashAlgorithmCategory.XXH.Name,
            IsCryptographic = false
        };

        /// <summary>
        /// Вычисление хеша для массива байтов с использованием алгоритма XxHash3
        /// </summary>
        /// <param name="data">Массив байтов для хеширования</param>
        /// <returns>Массив байтов, представляющий хеш</returns>
        protected override byte[] ComputeHash(byte[] data)
        {
            return XxHash3.Hash(data);
        }

        /// <summary>
        /// Вычисление хеша для потока с использованием алгоритма XxHash3
        /// </summary>
        /// <param name="inputStream">Поток данных для хеширования</param>
        /// <returns>Массив байтов, представляющий хеш</returns>
        protected override byte[] ComputeHash(Stream inputStream)
        {
            XxHash3 xxHash3 = new();
            xxHash3.Append(inputStream);
            return xxHash3.GetCurrentHash();
        }

        /// <summary>
        /// Вычисление хеша для потока асинхронно с использованием алгоритма XxHash3
        /// </summary>
        /// <param name="inputStream">Поток данных для хеширования</param>
        /// <param name="cancellationToken">Токен отмены</param>
        /// <returns>Массив байтов, представляющий хеш</returns>
        protected override async Task<byte[]> ComputeHashAsync(Stream inputStream, CancellationToken cancellationToken)
        {
            XxHash3 xxHash3 = new();
            return await ReadStreamWithTransformAsync(xxHash3, inputStream, cancellationToken);
        }
    }
}