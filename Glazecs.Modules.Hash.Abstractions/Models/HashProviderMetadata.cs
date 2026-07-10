namespace Glazecs.Modules.Hash.Abstractions.Models
{
    public readonly record struct HashProviderMetadata
    {
        /// <summary>
        /// Имя алгоритма (например, "SHA-256", "MD5")
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// Размер хеша в битах (например, 128 для MD5, 256 для SHA-256)
        /// </summary>
        public int HashSizeInBits { get; init; }

        /// <summary>
        /// Категория алгоритма (например, "SHA-2", "SHA-3", "BLAKE", "Custom")
        /// </summary>
        public string Category { get; init; }

        /// <summary>
        /// Определяет, является ли алгоритм криптографическим (например, SHA-2) или нет (например, MD5)
        /// </summary>
        public bool IsCryptographic { get; init; }
    }
}