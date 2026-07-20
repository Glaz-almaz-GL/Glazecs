using Glazecs.Modules.FileChunker.Abstractions.Interfaces;
using Glazecs.Modules.FileChunker.Resources.Languages;
using Microsoft.Extensions.Localization;
using System.Text.RegularExpressions;

namespace Glazecs.Modules.FileChunker.Rules
{
    /// <summary>
    /// Правило удаления знаков препинания из текста.
    /// Удаляет все символы, кроме букв, цифр, пробелов и переносов строк.
    /// </summary>
    public sealed partial class RemovePunctuationRule(IStringLocalizer<FileChunkerResources> localizer) : IChunkRule
    {
        /// <inheritdoc />
        public string Name => localizer["Rule_Punctuation_Name"];

        /// <inheritdoc />
        public string Description => localizer["Rule_Punctuation_Desc"];

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