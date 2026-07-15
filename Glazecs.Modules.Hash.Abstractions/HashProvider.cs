using Glazecs.Modules.Hash.Abstractions.Enums;
using Glazecs.Modules.Hash.Abstractions.Extensions;
using Glazecs.Modules.Hash.Abstractions.Interfaces;
using Glazecs.Modules.Hash.Abstractions.Models;
using System.Text;

namespace Glazecs.Modules.Hash.Abstractions
{
    /// <summary>
    /// Базовый класс для провайдеров хеширования.
    /// Реализует общую логику работы с файлами, потоками и форматированием.
    /// </summary>
    public abstract class HashProvider : IHashProvider
    {
        /// <summary>
        /// Размер буфера по умолчанию для чтения потоков (80 KB)
        /// </summary>
        protected const int DefaultBufferSize = 81920;

        /// <summary>
        /// Сообщение об ошибке, когда поток не поддерживает чтение
        /// </summary>
        protected const string StreamCannotBeReadMessage = "The stream does not support reading.";

        /// <summary>
        /// Сообщение об ошибке, когда файл не найден
        /// </summary>
        protected const string FileNotFoundMessage = "File not found";

        /// <inheritdoc/>
        public abstract HashProviderMetadata Metadata { get; }

        #region Calculate Methods

        /// <inheritdoc/>
        public string Calculate(string input, HashOutputFormat format = HashOutputFormat.LowerHex, Encoding? encoding = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(input);

            encoding ??= Encoding.UTF8;
            byte[] bytes = encoding.GetBytes(input);
            byte[] hash = ComputeHash(bytes);
            return hash.ToFormattedString(format);
        }

        /// <inheritdoc/>
        public string Calculate(byte[] data, HashOutputFormat format = HashOutputFormat.LowerHex)
        {
            ArgumentNullException.ThrowIfNull(data);
            byte[] hash = ComputeHash(data);
            return hash.ToFormattedString(format);
        }

        /// <inheritdoc/>
        public string Calculate(Stream inputStream, HashOutputFormat format = HashOutputFormat.LowerHex)
        {
            ArgumentNullException.ThrowIfNull(inputStream);

            if (!inputStream.CanRead)
            {
                throw new ArgumentException(StreamCannotBeReadMessage, nameof(inputStream));
            }

            byte[] hash = ComputeHash(inputStream);
            return hash.ToFormattedString(format);
        }

        /// <inheritdoc/>
        public string CalculateFile(string filePath, HashOutputFormat format = HashOutputFormat.LowerHex)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(FileNotFoundMessage, filePath);
            }

            using FileStream stream = File.OpenRead(filePath);
            return Calculate(stream, format);
        }

        #endregion

        #region CalculateAsync Methods

        /// <inheritdoc/>
        public Task<string> CalculateAsync(string input, HashOutputFormat format = HashOutputFormat.LowerHex, Encoding? encoding = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Calculate(input, format, encoding));
        }

        /// <inheritdoc/>
        public Task<string> CalculateAsync(byte[] data, HashOutputFormat format = HashOutputFormat.LowerHex, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Calculate(data, format));
        }

        /// <inheritdoc/>
        public async Task<string> CalculateAsync(Stream inputStream, HashOutputFormat format = HashOutputFormat.LowerHex, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(inputStream);

            if (!inputStream.CanRead)
            {
                throw new ArgumentException(StreamCannotBeReadMessage, nameof(inputStream));
            }

            byte[] hash = await ComputeHashAsync(inputStream, cancellationToken).ConfigureAwait(false);
            return hash.ToFormattedString(format);
        }

        /// <inheritdoc/>
        public async Task<string> CalculateFileAsync(string filePath, HashOutputFormat format = HashOutputFormat.LowerHex, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(FileNotFoundMessage, filePath);
            }

            await using FileStream stream = new(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: DefaultBufferSize,
                useAsync: true);

            return await CalculateAsync(stream, format, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region CalculateRaw Methods

        /// <inheritdoc/>
        public byte[] CalculateRaw(string input, Encoding? encoding = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(input);

            encoding ??= Encoding.UTF8;
            byte[] bytes = encoding.GetBytes(input);
            return ComputeHash(bytes);
        }

        /// <inheritdoc/>
        public byte[] CalculateRaw(byte[] data)
        {
            ArgumentNullException.ThrowIfNull(data);
            return ComputeHash(data);
        }

        /// <inheritdoc/>
        public byte[] CalculateRaw(Stream inputStream)
        {
            ArgumentNullException.ThrowIfNull(inputStream);

            return !inputStream.CanRead ? throw new ArgumentException(StreamCannotBeReadMessage, nameof(inputStream)) : ComputeHash(inputStream);
        }

        /// <inheritdoc/>
        public byte[] CalculateRawFile(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(FileNotFoundMessage, filePath);
            }

            using FileStream stream = File.OpenRead(filePath);
            return CalculateRaw(stream);
        }

        #endregion

        #region CalculateRawAsync Methods

        /// <inheritdoc/>
        public Task<byte[]> CalculateRawAsync(string input, Encoding? encoding = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CalculateRaw(input, encoding));
        }

        /// <inheritdoc/>
        public Task<byte[]> CalculateRawAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CalculateRaw(data));
        }

        /// <inheritdoc/>
        public async Task<byte[]> CalculateRawAsync(Stream inputStream, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(inputStream);

            return !inputStream.CanRead
                ? throw new ArgumentException(StreamCannotBeReadMessage, nameof(inputStream))
                : await ComputeHashAsync(inputStream, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<byte[]> CalculateRawFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(FileNotFoundMessage, filePath);
            }

            await using FileStream stream = new(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: DefaultBufferSize,
                useAsync: true);

            return await CalculateRawAsync(stream, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Verification Methods

        /// <inheritdoc/>
        public bool Verify(string input, string expectedHash, HashOutputFormat format = HashOutputFormat.LowerHex, Encoding? encoding = null)
        {
            string actualHash = Calculate(input, format, encoding);
            return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public bool Verify(byte[] data, string expectedHash, HashOutputFormat format = HashOutputFormat.LowerHex)
        {
            string actualHash = Calculate(data, format);
            return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public bool Verify(Stream inputStream, string expectedHash, HashOutputFormat format = HashOutputFormat.LowerHex)
        {
            string actualHash = Calculate(inputStream, format);
            return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public bool VerifyFile(string filePath, string expectedHash, HashOutputFormat format = HashOutputFormat.LowerHex)
        {
            string actualHash = CalculateFile(filePath, format);
            return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region VerificationAsync Methods

        /// <inheritdoc/>
        public async Task<bool> VerifyAsync(string input, string expectedHash, HashOutputFormat format = HashOutputFormat.LowerHex, Encoding? encoding = null, CancellationToken cancellationToken = default)
        {
            string actualHash = await CalculateAsync(input, format, encoding, cancellationToken).ConfigureAwait(false);
            return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public async Task<bool> VerifyAsync(byte[] data, string expectedHash, HashOutputFormat format = HashOutputFormat.LowerHex, CancellationToken cancellationToken = default)
        {
            string actualHash = await CalculateAsync(data, format, cancellationToken).ConfigureAwait(false);
            return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public async Task<bool> VerifyAsync(Stream inputStream, string expectedHash, HashOutputFormat format = HashOutputFormat.LowerHex, CancellationToken cancellationToken = default)
        {
            string actualHash = await CalculateAsync(inputStream, format, cancellationToken).ConfigureAwait(false);
            return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public async Task<bool> VerifyFileAsync(string filePath, string expectedHash, HashOutputFormat format = HashOutputFormat.LowerHex, CancellationToken cancellationToken = default)
        {
            string actualHash = await CalculateFileAsync(filePath, format, cancellationToken).ConfigureAwait(false);
            return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Abstract Methods

        /// <summary>
        /// Вычислить хеш для массива байтов (синхронно)
        /// </summary>
        protected abstract byte[] ComputeHash(byte[] data);

        /// <summary>
        /// Вычислить хеш для потока (синхронно)
        /// </summary>
        protected abstract byte[] ComputeHash(Stream inputStream);

        /// <summary>
        /// Вычислить хеш для потока (асинхронно)
        /// </summary>
        /// <remarks>
        /// Операции хеширования могут быть синхронными внутри,
        /// так как большинство алгоритмов быстрые.
        /// Асинхронность обеспечивается за счёт асинхронного чтения из потока.
        /// </remarks>
        protected abstract Task<byte[]> ComputeHashAsync(Stream inputStream, CancellationToken cancellationToken);

        #endregion

        #region Protected Helper Methods

        /// <summary>
        /// Чанковое чтение потока для асинхронного вычисления хеша через <see cref="System.Security.Cryptography.HashAlgorithm"/>
        /// </summary>
        protected static async Task<byte[]> ReadStreamWithTransformAsync(
            System.Security.Cryptography.HashAlgorithm algorithm,
            Stream inputStream,
            CancellationToken cancellationToken,
            int bufferSize = DefaultBufferSize)
        {
            byte[] buffer = new byte[bufferSize];
            int bytesRead;

            while ((bytesRead = await inputStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                algorithm.TransformBlock(buffer, 0, bytesRead, null, 0);
            }

            algorithm.TransformFinalBlock([], 0, 0);
            return algorithm.Hash!;
        }

        /// <summary>
        /// Чанковое чтение потока для асинхронного вычисления хеша через <see cref="System.IO.Hashing.NonCryptographicHashAlgorithm"/>
        /// </summary>
        protected static async Task<byte[]> ReadStreamWithTransformAsync(
            System.IO.Hashing.NonCryptographicHashAlgorithm algorithm,
            Stream inputStream,
            CancellationToken cancellationToken,
            int bufferSize = DefaultBufferSize)
        {
            byte[] buffer = new byte[bufferSize];
            int bytesRead;

            while ((bytesRead = await inputStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                algorithm.Append(buffer.AsSpan(0, bytesRead));
            }

            return algorithm.GetCurrentHash();
        }

        #endregion
    }
}