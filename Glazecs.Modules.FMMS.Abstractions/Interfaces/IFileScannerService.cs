using Glazecs.Modules.FMMS.Abstractions.Models;

namespace Glazecs.Modules.FMMS.Abstractions.Interfaces
{
    /// <summary>
    /// Контракт для сервиса рекурсивного сканирования файловых систем.
    /// </summary>
    /// <remarks>
    /// <para>Предоставляет асинхронный потоковый API для обработки файлов,
    /// что позволяет начинать работу с результатами до завершения полного обхода директории.</para>
    /// <para>Поддерживает вычисление метаданных файлов, подсчёт страниц (для PDF и других форматов),
    /// а также вычисление хешей с использованием настраиваемых алгоритмов.</para>
    /// <para>Реализация должна обеспечивать потокобезопасность и корректную обработку отмены операции.</para>
    /// </remarks>
    public interface IFileScannerService
    {
        /// <summary>
        /// Рекурсивно сканирует указанную директорию и возвращает метаданные найденных файлов
        /// в виде асинхронного потока.
        /// </summary>
        /// <param name="directoryPath">Путь к корневой директории для сканирования.</param>
        /// <param name="settings">
        /// Настройки сканирования, определяющие:
        /// <list type="bullet">
        /// <item><description>Алгоритмы хеширования для вычисления</description></item>
        /// <item><description>Лимиты размера файлов для хеширования</description></item>
        /// <item><description>Пользовательские расширения архивов</description></item>
        /// <item><description>Правила подсчёта страниц для нестандартных форматов</description></item>
        /// <item><description>Режим параллелизма при вычислении хешей</description></item>
        /// </list>
        /// </param>
        /// <param name="progress">
        /// Прогресс-репортёр для отслеживания количества обработанных файлов.
        /// Получает абсолютное число обработанных файлов (не процент),
        /// так как общее количество файлов заранее неизвестно из-за ленивого перечисления.
        /// </param>
        /// <param name="cancellationToken">
        /// Токен отмены для прерывания операции сканирования.
        /// При отмене генерируется <see cref="OperationCanceledException"/>.
        /// </param>
        /// <returns>
        /// Асинхронный поток объектов <see cref="ScannedFile"/>, содержащих метаданные каждого найденного файла:
        /// <list type="bullet">
        /// <item><description>Относительный и полный путь</description></item>
        /// <item><description>Расширение и размер</description></item>
        /// <item><description>Количество страниц (для PDF и настроенных форматов)</description></item>
        /// <item><description>Признак архива</description></item>
        /// <item><description>Вычисленные хеши (словарь по имени алгоритма)</description></item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// <para><strong>Ленивая обработка:</strong> Метод работает как генератор — файлы обрабатываются
        /// по мере перечисления, что позволяет начинать обработку до завершения полного обхода директории.</para>
        /// <para><strong>Устойчивость к ошибкам:</strong> При ошибке доступа к файлу или директории
        /// операция не прерывается — проблемный элемент пропускается с записью в лог.</para>
        /// <para><strong>Безопасность:</strong> Репарс-пойнты (симлинки, junction) игнорируются
        /// для предотвращения зацикливания при рекурсивном обходе.</para>
        /// <para><strong>Производительность:</strong> Провайдеры хешей инициализируются один раз
        /// перед началом обработки для минимизации накладных расходов.</para>
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">
        /// Если <paramref name="directoryPath"/> или <paramref name="settings"/> равны <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.OperationCanceledException">
        /// Если операция была отменена через <paramref name="cancellationToken"/>.
        /// </exception>
        IAsyncEnumerable<ScannedFile> ScanDirectoryAsync(
            string directoryPath,
            FilesScanningSettings settings,
            IProgress<int> progress,
            CancellationToken cancellationToken = default);
    }
}