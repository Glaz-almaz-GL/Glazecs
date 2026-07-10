using Glazecs.Modules.Hash.Abstractions.Enums;

namespace Glazecs.Modules.FMMS.Abstractions.Models
{
    /// <summary>
    /// Настройки вычисления хешей
    /// </summary>
    public sealed class HashingSettings
    {
        /// <summary>
        /// Имена алгоритмов для вычисления (должны быть зарегистрированы в IHashProviderFactory)
        /// </summary>
        public HashSet<string> AlgorithmsToCalculate { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            "SHA-256"
        };

        /// <summary>
        /// Формат вывода хеша
        /// </summary>
        public HashOutputFormat OutputFormat { get; set; } = HashOutputFormat.LowerHex;

        /// <summary>
        /// Максимальный размер файла для вычисления хеша (в байтах).
        /// 0 = без ограничений.
        /// </summary>
        public long MaxFileSizeBytes { get; set; } = 0;

        /// <summary>
        /// Вычислять ли хеши параллельно (несколько алгоритмов одновременно)
        /// </summary>
        public bool CalculateInParallel { get; set; } = false;

        /// <summary>
        /// Максимальная степень параллелизма при вычислении хешей.
        /// Ограничивает количество одновременно открытых файлов и вычислений.
        /// </summary>
        /// <remarks>
        /// <para>Значение по умолчанию: 4.</para>
        /// <para>Рекомендуется устанавливать равным количеству логических ядер CPU
        /// или количеству физических дисков (в зависимости от того, что является узким местом).</para>
        /// <para>Значение &lt;= 0 означает "без ограничений" (не рекомендуется).</para>
        /// </remarks>
        public int MaxDegreeOfParallelism { get; set; } = 4;
    }
}
