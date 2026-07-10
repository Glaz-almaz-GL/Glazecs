using Glazecs.Modules.FileChunker.Abstractions.Interfaces;
using Glazecs.Modules.FileChunker.Abstractions.Models;
using System.Text.RegularExpressions;

namespace Glazecs.Modules.FileChunker.Abstractions.Formatters
{
    /// <summary>
    /// Форматтер заголовков на основе шаблонов с плейсхолдерами вида {PropertyName}.
    /// Поддерживает стандартные плейсхолдеры (FileName, FilePart, FileSize и др.) и кастомные свойства из контекста.
    /// </summary>
    public sealed partial class TemplateHeaderFormatter : IHeaderFormatter
    {
        private readonly Dictionary<string, Func<ChunkMetadataContext, string>> _placeholders;

        /// <summary>
        /// Инициализирует новый экземпляр <see cref="TemplateHeaderFormatter"/> с набором стандартных плейсхолдеров.
        /// </summary>
        public TemplateHeaderFormatter()
        {
            _placeholders = new Dictionary<string, Func<ChunkMetadataContext, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["{FileName}"] = ctx => string.IsNullOrEmpty(ctx.OriginalPath)
                    ? ctx.FileName
                    : Path.GetFileName(ctx.OriginalPath),
                ["{OriginalPath}"] = ctx => ctx.OriginalPath,
                ["{FilePart}"] = ctx => ctx.FilePartNumber.ToString(),
                ["{TotalParts}"] = ctx => ctx.TotalFileParts.ToString(),
                ["{FileSize}"] = ctx => ctx.FileSizeBytes.ToString("N0"),
                ["{ChunkIndex}"] = ctx => ctx.ChunkIndex.ToString(),
                ["{Date}"] = ctx => ctx.GeneratedAt.ToString("yyyy-MM-dd"),
                ["{DateTime}"] = ctx => ctx.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            };
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="context"/> равен null.</exception>
        public string Format(string template, ChunkMetadataContext context)
        {
            if (string.IsNullOrEmpty(template))
            {
                return string.Empty;
            }

            string result = template;
            foreach (KeyValuePair<string, Func<ChunkMetadataContext, string>> placeholder in _placeholders
                .Where(placeholder => result
                .Contains(placeholder.Key, StringComparison.OrdinalIgnoreCase)))
            {
                string value = placeholder.Value(context);
                result = result.Replace(placeholder.Key, value, StringComparison.OrdinalIgnoreCase);
            }

            // Подстановка кастомных свойств
            if (context.CustomProperties is { Count: > 0 })
            {
                foreach (KeyValuePair<string, object> customProp in context.CustomProperties)
                {
                    string key = $"{{{customProp.Key}}}";
                    if (result.Contains(key, StringComparison.OrdinalIgnoreCase))
                    {
                        string value = customProp.Value?.ToString() ?? string.Empty;
                        result = result.Replace(key, value, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }

            return result;
        }

        /// <inheritdoc />
        public bool ValidateTemplate(string template, out IReadOnlyCollection<string> unknownPlaceholders)
        {
            var unknown = new List<string>();

            if (string.IsNullOrEmpty(template))
            {
                unknownPlaceholders = unknown;
                return true;
            }

            MatchCollection matches = PlaceholderRegex().Matches(template);
            var allKnownPlaceholders = _placeholders.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (string? placeholder in matches.Select(m => m.Value)
                .Where(placeholder => !allKnownPlaceholders
                .Contains(placeholder)))
            {
                // Извлекаем имя свойства без скобок для проверки в кастомных свойствах
                string propertyName = placeholder.Trim('{', '}');

                if (!IsValidCustomPropertyName(propertyName))
                {
                    unknown.Add(placeholder);
                }
            }

            unknownPlaceholders = unknown;
            return unknown.Count == 0;
        }

        /// <summary>
        /// Проверяет, является ли имя свойства допустимым для кастомного плейсхолдера.
        /// </summary>
        /// <param name="propertyName">Имя свойства для проверки.</param>
        /// <returns>True, если имя допустимо; иначе false.</returns>
        private static bool IsValidCustomPropertyName(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            // Имя должно начинаться с буквы и содержать только буквы, цифры и подчеркивания
            return CustomPropertyNameRegex().IsMatch(propertyName);
        }

        /// <summary>
        /// Регулярное выражение для поиска плейсхолдеров в шаблоне.
        /// </summary>
        /// <returns>Скомпилированное регулярное выражение.</returns>
        [GeneratedRegex(@"\{[A-Za-z0-9_]+\}", RegexOptions.Compiled)]
        private static partial Regex PlaceholderRegex();

        /// <summary>
        /// Регулярное выражение для проверки валидности имени кастомного свойства.
        /// </summary>
        /// <returns>Скомпилированное регулярное выражение.</returns>
        [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled)]
        private static partial Regex CustomPropertyNameRegex();
    }
}