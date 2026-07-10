namespace Glazecs.Shared.Core.Extensions
{
    public static class FormatBytesExtension
    {
        public const double BitsPerByte = 8.0;
        public const double BytesPerKiloByte = 1024.0;
        public const double BytesPerMegaByte = 1024.0 * 1024.0;
        public const double BytesPerGigaByte = 1024.0 * 1024.0 * 1024.0;
        public const double BytesPerTeraByte = 1024.0 * 1024.0 * 1024.0 * 1024.0;
        public const double BytesPerPetaByte = 1024.0 * 1024.0 * 1024.0 * 1024.0 * 1024.0;

        #region ToBits

        /// <summary>
        /// Конвертирует байты в биты
        /// </summary>
        public static double ToBits(this long bytes)
        {
            return bytes * BitsPerByte;
        }

        /// <summary>
        /// Конвертирует байты в биты
        /// </summary>
        public static double ToBits(this double bytes)
        {
            return bytes * BitsPerByte;
        }

        #endregion

        #region ToKiloBytes

        /// <summary>
        /// Конвертирует байты в килобайты
        /// </summary>
        public static double ToKiloBytes(this long bytes)
        {
            return bytes / BytesPerKiloByte;
        }

        /// <summary>
        /// Конвертирует байты в килобайты
        /// </summary>
        public static double ToKiloBytes(this double bytes)
        {
            return bytes / BytesPerKiloByte;
        }

        #endregion

        #region ToMegaBytes

        /// <summary>
        /// Конвертирует байты в мегабайты
        /// </summary>
        public static double ToMegaBytes(this long bytes)
        {
            return bytes / BytesPerMegaByte;
        }

        /// <summary>
        /// Конвертирует байты в мегабайты
        /// </summary>
        public static double ToMegaBytes(this double bytes)
        {
            return bytes / BytesPerMegaByte;
        }

        #endregion

        #region ToGigaBytes

        /// <summary>
        /// Конвертирует байты в гигабайты
        /// </summary>
        public static double ToGigaBytes(this long bytes)
        {
            return bytes / BytesPerGigaByte;
        }

        /// <summary>
        /// Конвертирует байты в гигабайты
        /// </summary>
        public static double ToGigaBytes(this double bytes)
        {
            return bytes / BytesPerGigaByte;
        }

        #endregion

        #region ToTeraBytes

        /// <summary>
        /// Конвертирует байты в терабайты
        /// </summary>
        public static double ToTeraBytes(this long bytes)
        {
            return bytes / BytesPerTeraByte;
        }

        /// <summary>
        /// Конвертирует байты в терабайты
        /// </summary>
        public static double ToTeraBytes(this double bytes)
        {
            return bytes / BytesPerTeraByte;
        }

        #endregion

        #region ToPetaBytes

        /// <summary>
        /// Конвертирует байты в петабайты
        /// </summary>
        public static double ToPetaBytes(this long bytes)
        {
            return bytes / BytesPerPetaByte;
        }

        /// <summary>
        /// Конвертирует байты в петабайты
        /// </summary>
        public static double ToPetaBytes(this double bytes)
        {
            return bytes / BytesPerPetaByte;
        }

        #endregion

        #region ToHumanReadableSize

        /// <summary>
        /// Форматирует размер файла в человекочитаемую строку (например, "1.5 MB")
        /// </summary>
        public static string ToHumanReadableSize(this long bytes)
        {
            return ToHumanReadableSize((double)bytes);
        }

        /// <summary>
        /// Форматирует размер файла в человекочитаемую строку (например, "1.5 MB")
        /// </summary>
        public static string ToHumanReadableSize(this double bytes)
        {
            string[] sizes = ["B", "KB", "MB", "GB", "TB", "PB"];
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        #endregion
    }
}