namespace Glazecs.Modules.Hash.Abstractions.Models
{
    /// <summary>
    /// Категория алгоритма хеширования
    /// </summary>
    public readonly record struct HashAlgorithmCategory(string Name, bool IsCryptographic, int Priority = 0)
    {
        /// <summary>
        /// Имя категории (например, "SHA-2", "Legacy")
        /// </summary>
        public string Name { get; init; } = Name;

        /// <summary>
        /// Определяет, является ли категория криптографической (например, SHA-2) или нет (например, Legacy)
        /// </summary>
        public bool IsCryptographic { get; init; } = IsCryptographic;

        /// <summary>
        /// Приоритет категории. Алгоритмы с более высоким приоритетом могут быть выбраны по умолчанию при наличии нескольких алгоритмов в одной категории.
        /// </summary>
        public int Priority { get; init; } = Priority;

        // Предопределённые категории (встроенные)
        public static readonly HashAlgorithmCategory Legacy =
            new("Legacy", false, 100);

        public static readonly HashAlgorithmCategory SHA2 =
            new("SHA-2", true, 10);

        public static readonly HashAlgorithmCategory SHA3 =
            new("SHA-3", true, 10);

        public static readonly HashAlgorithmCategory XXH =
            new("XXH", false, 50);

        public static readonly HashAlgorithmCategory XXH3 =
            new("XXH3", false, 50);

        public static readonly HashAlgorithmCategory CRC =
            new("CRC", false, 50);

        public static readonly HashAlgorithmCategory Uncategorized =
            new("Uncategorized", false, 50);

        public override string ToString()
        {
            return Name;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
    }
}
