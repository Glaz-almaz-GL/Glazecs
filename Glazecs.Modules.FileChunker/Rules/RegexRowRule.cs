using Glazecs.Modules.FileChunker.Abstractions.Attributes;
using Glazecs.Modules.FileChunker.Abstractions.Interfaces;
using Glazecs.Modules.FileChunker.Components.Rules;
using Glazecs.Modules.FileChunker.Resources.Languages;
using Microsoft.Extensions.Localization;
using System.Text.RegularExpressions;

namespace Glazecs.Modules.FileChunker.Rules
{
    [ChunkRuleEditor(typeof(RegexRuleEditor))]
    public sealed class RegexRowRule(IStringLocalizer<FileChunkerResources> localizer) : IChunkRule
    {
        public string Name => localizer["Rule_Regex_Name"];
        public string Description => localizer["Rule_Regex_Desc"];

        private Regex? _compiledRegex;
        private bool _isRegexValid;

        public string Pattern
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    // Сбрасываем кэш при изменении паттерна
                    _compiledRegex = null;
                    _isRegexValid = false;
                }
            }
        } = string.Empty;

        public string Apply(string content)
        {
            ArgumentNullException.ThrowIfNull(content);

            if (string.IsNullOrWhiteSpace(Pattern))
            {
                return content;
            }

            // Ленивая инициализация и валидация Regex
            if (!_isRegexValid || _compiledRegex == null)
            {
                try
                {
                    _compiledRegex = new Regex(Pattern, RegexOptions.Compiled);
                    _isRegexValid = true;
                }
                catch (ArgumentException)
                {
                    return content;
                }
            }

            string[] lines = content.Split('\n');
            IEnumerable<string> resultLines = lines.Where(line => !_compiledRegex.IsMatch(line.TrimEnd('\r')));

            return string.Join('\n', resultLines);
        }
    }
}