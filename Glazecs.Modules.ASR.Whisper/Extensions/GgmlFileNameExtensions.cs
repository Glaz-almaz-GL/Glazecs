using System.Text.RegularExpressions;
using Whisper.net.Ggml;

namespace Glazecs.Modules.ASR.Whisper.Extensions;

/// <summary>
/// Предоставляет методы расширения для работы с типами моделей GGML.
/// </summary>
public static partial class GgmlTypeExtensions
{
    /// <summary>
    /// Стандартное расширение файла для классических моделей GGML.
    /// </summary>
    public const string ModelFileExtension = ".bin";

    /// <summary>
    /// Сгенерированное регулярное выражение для преобразования PascalCase в kebab-case.
    /// Правила разделения:
    /// 1. Между строчной буквой и следующей за ней заглавной буквой или цифрой (напр., "e-V" или "e-3").
    /// 2. Между цифрой и следующей за ней заглавной буквой (напр., "3-T").
    /// Это гарантирует, что "LargeV3Turbo" превратится в "Large-V3-Turbo", а не в "Large-V-3-Turbo".
    /// </summary>
    [GeneratedRegex(@"(?<=[a-z])(?=[A-Z0-9])|(?<=[0-9])(?=[A-Z])")]
    private static partial Regex PascalCaseToKebabCaseRegex();

    /// <summary>
    /// Динамически преобразует тип модели GGML в стандартное имя файла модели.
    /// Работает для всех текущих и будущих значений enum, корректно расставляя дефисы.
    /// </summary>
    /// <param name="ggmlType">Тип модели GGML.</param>
    /// <returns>Имя файла модели в формате kebab-case с расширением (например, "ggml-large-v3-turbo.bin").</returns>
    public static string ToModelFileName(this GgmlType ggmlType)
    {
        // LargeV3Turbo
        string enumName = ggmlType.ToString();

        // Large-V3-Turbo
        string kebabCaseName = PascalCaseToKebabCaseRegex().Replace(enumName, "-");

        // ggml-large-v3-turbo.bin
        return $"ggml-{kebabCaseName.ToLowerInvariant()}{ModelFileExtension}";
    }
}