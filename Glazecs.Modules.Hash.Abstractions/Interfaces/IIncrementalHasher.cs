namespace Glazecs.Modules.Hash.Abstractions.Interfaces
{
    /// <summary>
    /// Пошаговое вычисление хеша: данные подаются частями, хеш забирается в конце.
    /// </summary>
    /// <remarks>
    /// Позволяет прочитать файл один раз и подать каждый прочитанный блок сразу нескольким алгоритмам.
    /// Экземпляр не потокобезопасен и после <see cref="GetHash"/> не используется.
    /// </remarks>
    public interface IIncrementalHasher : IDisposable
    {
        /// <summary>
        /// Добавить очередной блок данных.
        /// </summary>
        void Append(byte[] buffer, int offset, int count);

        /// <summary>
        /// Получить хеш всех добавленных данных.
        /// </summary>
        byte[] GetHash();
    }
}
