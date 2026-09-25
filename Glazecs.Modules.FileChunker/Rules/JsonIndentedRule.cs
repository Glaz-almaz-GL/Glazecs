using Glazecs.Modules.FileChunker.Abstractions.Interfaces;
using Glazecs.Modules.FileChunker.Resources.Languages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Glazecs.Modules.FileChunker.Rules
{
    /// <summary>
    /// Правило для удаления отступов в JSON-контенте, делая его компактным.
    /// </summary>
    public sealed class JsonIndentedRule(ILogger<JsonIndentedRule>? logger, IStringLocalizer<FileChunkerResources> localizer) : IChunkRule
    {
        public string Name => localizer["Rule_JsonIndented_Name"];

        public string Description => localizer["Rule_JsonIndented_Desc"];

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = false,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
            ReadCommentHandling = JsonCommentHandling.Skip, // Позволяет игнорировать комментарии в JSON
            AllowTrailingCommas = true // Позволяет игнорировать запятые в конце объектов и массивов
        };

        public string Apply(string content)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(content);

            if (!IsValidJson(content))
            {
                logger?.LogWarning("Content is not valid JSON. Rule {RuleName} will not be applied.", Name);
                return content;
            }

            using JsonDocument doc = JsonDocument.Parse(content);

            return JsonSerializer.Serialize(doc.RootElement, _jsonOptions);
        }

        private static bool IsValidJson(string input)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(input);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
