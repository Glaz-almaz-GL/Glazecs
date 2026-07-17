namespace Glazecs.Modules.ASR.Abstractions.Interfaces
{
    /// <summary>
    /// Представляет собой минимальную единицу распознавания (токен/слово) внутри результата.
    /// </summary>
    public interface ISpeechRecognitionToken
    {
        /// <summary>
        /// Текст токена.
        /// </summary>
        string? Text { get; }

        /// <summary>
        /// Время начала токена относительно начала аудиопотока/файла.
        /// </summary>
        TimeSpan Start { get; }

        /// <summary>
        /// Время окончания токена относительно начала аудиопотока/файла.
        /// </summary>
        TimeSpan End { get; }

        /// <summary>
        /// Уровень уверенности модели в данном токене (от 0.0 до 1.0).
        /// </summary>
        float Confidence { get; }
    }
}
