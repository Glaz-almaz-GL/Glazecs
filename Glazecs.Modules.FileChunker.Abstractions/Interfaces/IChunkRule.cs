namespace Glazecs.Modules.FileChunker.Abstractions.Interfaces
{
    /// <summary>
    /// Интерфейс правила трансформации текстового содержимого.
    /// </summary>
    public interface IChunkRule
    {
        /// <summary>
        /// Имя правила.
        /// </summary>
        string RuleName { get; }

        /// <summary>
        /// Описание правила
        /// </summary>
        string RuleDescription { get; }

        /// <summary>
        /// Применяет правило к текстовому фрагменту.
        /// </summary>
        /// <param name="content">Исходный текст.</param>
        /// <returns>Трансформированный текст.</returns>
        string Apply(string content);
    }
}
