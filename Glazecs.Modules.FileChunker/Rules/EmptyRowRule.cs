using Glazecs.Modules.FileChunker.Abstractions.Interfaces;
using Glazecs.Modules.FileChunker.Resources.Languages;
using Microsoft.Extensions.Localization;
using System.Text;

namespace Glazecs.Modules.FileChunker.Rules
{
    public sealed class EmptyRowRule(IStringLocalizer<FileChunkerResources> localizer) : IChunkRule
    {
        public string Name => localizer["Rule_Empty_Row_Name"];
        public string Description => localizer["Rule_Empty_Row_Desc"];

        public string Apply(string content)
        {
            ArgumentNullException.ThrowIfNull(content);

            using StringReader reader = new(content);
            StringBuilder? sb = new(content.Length);

            string? line;
            bool isFirstLine = true;

            while ((line = reader.ReadLine()) != null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    if (!isFirstLine)
                    {
                        sb.AppendLine(); // Позволяет избежать лишних переносов строк
                    }
                    sb.Append(line);
                    isFirstLine = false;
                }
            }

            return sb.ToString();
        }
    }
}
