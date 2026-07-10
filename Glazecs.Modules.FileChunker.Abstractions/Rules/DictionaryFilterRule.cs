using Glazecs.Modules.FileChunker.Abstractions.Interfaces;
using System.Text;

namespace Glazecs.Modules.FileChunker.Abstractions.Rules
{
    /// <summary>
    /// Правило фильтрации слов по заданному словарю стоп-слов.
    /// Удаляет из текста все слова, присутствующие в списке стоп-слов (без учета регистра).
    /// </summary>
    public sealed class DictionaryFilterRule : IChunkRule
    {
        private readonly HashSet<string> _stopWords;

        /// <summary>
        /// Инициализирует новый экземпляр <see cref="DictionaryFilterRule"/> с указанным списком стоп-слов.
        /// </summary>
        /// <param name="stopWords">Коллекция стоп-слов для фильтрации.</param>
        /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="stopWords"/> равен null.</exception>
        public DictionaryFilterRule(IEnumerable<string> stopWords)
        {
            ArgumentNullException.ThrowIfNull(stopWords);
            _stopWords = new HashSet<string>(
                stopWords.Select(w => w.ToLowerInvariant()),
                StringComparer.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public string RuleName => "DictionaryFilter";

        /// <inheritdoc />
        public string RuleDescription => "Удаляет из текста слова, присутствующие в заданном словаре стоп-слов.";

        /// <inheritdoc />
        /// <remarks>
        /// Разделение текста на слова производится с учетом пробелов, табуляций и переносов строк.
        /// Множественные пробелы сохраняются для поддержания форматирования.
        /// </remarks>
        public string Apply(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return content;
            }

            StringBuilder result = new(content.Length);
            StringBuilder currentWord = new();
            bool isProcessingWord = false;

            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];

                if (char.IsLetterOrDigit(c))
                {
                    currentWord.Append(c);
                    isProcessingWord = true;
                }
                else
                {
                    if (isProcessingWord)
                    {
                        // Завершили слово — проверяем, является ли оно стоп-словом
                        string word = currentWord.ToString();
                        if (!_stopWords.Contains(word))
                        {
                            result.Append(word);
                        }
                        currentWord.Clear();
                        isProcessingWord = false;
                    }
                    // Добавляем небуквенный символ (пробел, перенос строки и т.д.)
                    result.Append(c);
                }
            }

            // Обрабатываем последнее слово, если текст не заканчивается пробелом
            if (isProcessingWord)
            {
                string word = currentWord.ToString();
                if (!_stopWords.Contains(word))
                {
                    result.Append(word);
                }
            }

            return result.ToString();
        }
    }
}