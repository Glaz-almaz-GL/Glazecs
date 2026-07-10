using Glazecs.Modules.Hash.Abstractions.Enums;

namespace Glazecs.Modules.Hash.Abstractions.Extensions
{
    /// <summary>
    /// Методы расширения для форматирования байтовых массивов хешей
    /// </summary>
    /// <remarks>
    /// Подключайте этот namespace явно в местах, где требуется форматирование хешей.
    /// </remarks>
    public static class HashBytesExtensions
    {
        /// <summary>
        /// Преобразовать байтовый массив хеша в шестнадцатеричную строку в нижнем регистре
        /// </summary>
        /// <example>"5d41402abc4b2a76b9719d911017c592"</example>
        public static string ToHexString(this byte[] hash)
        {
            ArgumentNullException.ThrowIfNull(hash);
            return Convert.ToHexStringLower(hash);
        }

        /// <summary>
        /// Преобразовать байтовый массив хеша в шестнадцатеричную строку в нижнем регистре
        /// </summary>
        public static string ToHexString(this ReadOnlySpan<byte> hash)
        {
            return Convert.ToHexStringLower(hash);
        }

        /// <summary>
        /// Преобразовать байтовый массив хеша в шестнадцатеричную строку в верхнем регистре
        /// </summary>
        /// <example>"5D41402ABC4B2A76B9719D911017C592"</example>
        public static string ToHexStringUpper(this byte[] hash)
        {
            ArgumentNullException.ThrowIfNull(hash);
            return Convert.ToHexString(hash);
        }

        /// <summary>
        /// Преобразовать байтовый массив хеша в шестнадцатеричную строку в верхнем регистре
        /// </summary>
        public static string ToHexStringUpper(this ReadOnlySpan<byte> hash)
        {
            return Convert.ToHexString(hash);
        }

        /// <summary>
        /// Преобразовать байтовый массив хеша в Base64 строку
        /// </summary>
        /// <example>"XUFAKrxLKna5cZ2REBfFkg=="</example>
        public static string ToBase64String(this byte[] hash)
        {
            ArgumentNullException.ThrowIfNull(hash);
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Преобразовать байтовый массив хеша в Base64 строку
        /// </summary>
        public static string ToBase64String(this ReadOnlySpan<byte> hash)
        {
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Преобразовать байтовый массив хеша в URL-safe Base64 строку
        /// </summary>
        /// <example>"XUFAKrxLKna5cZ2REBfFkg"</example>
        public static string ToBase64UrlString(this byte[] hash)
        {
            ArgumentNullException.ThrowIfNull(hash);
            return Convert.ToBase64String(hash)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        /// <summary>
        /// Преобразовать байтовый массив хеша в URL-safe Base64 строку
        /// </summary>
        public static string ToBase64UrlString(this ReadOnlySpan<byte> hash)
        {
            return Convert.ToBase64String(hash)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        /// <summary>
        /// Преобразовать байтовый массив хеша в строку заданного формата
        /// </summary>
        /// <param name="hash">Байтовый массив хеша</param>
        /// <param name="format">Формат вывода</param>
        /// <returns>Строковое представление хеша</returns>
        /// <exception cref="ArgumentOutOfRangeException">Если формат не поддерживается</exception>
        public static string ToFormattedString(this byte[] hash, HashOutputFormat format)
        {
            ArgumentNullException.ThrowIfNull(hash);

            return format switch
            {
                HashOutputFormat.LowerHex => ToHexString(hash),
                HashOutputFormat.UpperHex => ToHexStringUpper(hash),
                HashOutputFormat.Base64 => ToBase64String(hash),
                HashOutputFormat.Base64Url => ToBase64UrlString(hash),
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported output format")
            };
        }

        /// <summary>
        /// Преобразовать байтовый массив хеша в строку заданного формата
        /// </summary>
        public static string ToFormattedString(this ReadOnlySpan<byte> hash, HashOutputFormat format)
        {
            return format switch
            {
                HashOutputFormat.LowerHex => ToHexString(hash),
                HashOutputFormat.UpperHex => ToHexStringUpper(hash),
                HashOutputFormat.Base64 => ToBase64String(hash),
                HashOutputFormat.Base64Url => ToBase64UrlString(hash),
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported output format")
            };
        }
    }
}