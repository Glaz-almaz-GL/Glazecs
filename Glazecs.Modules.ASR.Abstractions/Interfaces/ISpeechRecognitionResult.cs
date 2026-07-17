namespace Glazecs.Modules.ASR.Abstractions.Interfaces
{
    /// <summary>
    /// Представляет результат распознавания речи (сегмент, фраза или гипотезу).
    /// </summary>
    public interface ISpeechRecognitionResult
    {
        /// <summary>
        /// Распознанный текст сегмента.
        /// </summary>
        string Text { get; }

        /// <summary>
        /// Время начала сегмента относительно начала аудиопотока/файла.
        /// </summary>
        TimeSpan Start { get; }

        /// <summary>
        /// Время окончания сегмента относительно начала аудиопотока/файла.
        /// </summary>
        TimeSpan End { get; }

        /// <summary>
        /// Указывает, является ли результат финальным (завершенным) или промежуточным (гипотезой).
        /// Критически важно для корректного отображения текста при потоковом распознавании.
        /// </summary>
        bool IsFinal { get; }

        /// <summary>
        /// Уровень уверенности модели в данном результате.
        /// Значение в диапазоне от 0.0 до 1.0.
        /// Метод агрегации (среднее, минимальное и т.д.) зависит от конкретной реализации провайдера.
        /// </summary>
        float Confidence { get; }

        /// <summary>
        /// Список токенов (слов), из которых состоит данный результат.
        /// </summary>
        IReadOnlyList<ISpeechRecognitionToken> Tokens { get; }
    }
}
