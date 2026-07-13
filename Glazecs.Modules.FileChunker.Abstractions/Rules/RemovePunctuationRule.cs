using Glazecs.Modules.FileChunker.Abstractions.Interfaces;
using System.Text.RegularExpressions;

namespace Glazecs.Modules.FileChunker.Abstractions.Rules
{
    /// <summary>
    /// Правило удаления знаков препинания из текста.
    /// Удаляет все символы, кроме букв, цифр, пробелов и переносов строк.
    /// </summary>
    public sealed partial class RemovePunctuationRule : IChunkRule
    {
        /// <inheritdoc />
        public string RuleName => "RemovePunctuation";

        /// <inheritdoc />
        public string RuleDescription => "Удаляет из текста все знаки препинания, сохраняя буквы, цифры и структуру строк.";

        /// <inheritdoc />
        /// <remarks>
        /// Переносы строк (\r, \n) и табуляции сохраняются для поддержания структуры документа.
        /// </remarks>
        public string Apply(string content)
        {
            return string.IsNullOrEmpty(content) ? content : PunctuationRegex().Replace(content, string.Empty);
        }

        /// <summary>
        /// Регулярное выражение для поиска знаков препинания.
        /// Удаляет все символы, кроме букв (\w), пробелов (\s), переносов строк и табуляций.
        /// </summary>
        /// <returns>Скомпилированное регулярное выражение.</returns>
        [GeneratedRegex(@"[^\w\s\r\n\t]", RegexOptions.Compiled)]
        private static partial Regex PunctuationRegex();
    }
}