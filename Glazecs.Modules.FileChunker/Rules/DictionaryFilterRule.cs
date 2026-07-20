using Glazecs.Modules.FileChunker.Abstractions.Attributes;
using Glazecs.Modules.FileChunker.Abstractions.Interfaces;
using Glazecs.Modules.FileChunker.Components.Rules;
using Glazecs.Modules.FileChunker.Resources.Languages;
using Microsoft.Extensions.Localization;
using System.Text;

namespace Glazecs.Modules.FileChunker.Rules
{
    /// <summary>
    /// Правило фильтрации слов по заданному словарю стоп-слов.
    /// </summary>
    [ChunkRuleEditor(typeof(DictionaryFilterRuleEditor))]
    public sealed class DictionaryFilterRule(IStringLocalizer<FileChunkerResources> localizer) : IChunkRule
    {
        public string Name => localizer["Rule_Dictionary_Name"];
        public string Description => localizer["Rule_Dictionary_Desc"];

        private HashSet<string>? _cachedStopWords;

        /// <summary>
        /// Текст стоп-слов, разделенных запятыми, пробелами или переносами строк.
        /// При изменении этого свойства кэш HashSet автоматически сбрасывается.
        /// </summary>
        public string StopWordsText
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    _cachedStopWords = null; // Инвалидация кэша при изменении данных из UI
                }
            }
        } = string.Empty;

        /// <summary>
        /// Получает или инициализирует кэшированный HashSet стоп-слов.
        /// </summary>
        private HashSet<string> GetStopWords()
        {
            if (_cachedStopWords == null)
            {
                if (string.IsNullOrWhiteSpace(StopWordsText))
                {
                    _cachedStopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    string[] words = StopWordsText.Split(
                        [',', ' ', '\r', '\n'],
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                    _cachedStopWords = new HashSet<string>(words, StringComparer.OrdinalIgnoreCase);
                }
            }

            return _cachedStopWords;
        }

        public string Apply(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return content;
            }

            HashSet<string> stopWords = GetStopWords();

            // Оптимизация: если список стоп-слов пуст, возвращаем исходный текст без обработки
            if (stopWords.Count == 0)
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
                else if (isProcessingWord)
                {
                    string word = currentWord.ToString();
                    if (!stopWords.Contains(word))
                    {
                        result.Append(word);
                    }
                    currentWord.Clear();
                    isProcessingWord = false;
                }
                result.Append(c);
            }

            if (isProcessingWord)
            {
                string word = currentWord.ToString();
                if (!stopWords.Contains(word))
                {
                    result.Append(word);
                }
            }

            return result.ToString();
        }
    }
}