namespace Glazecs.Modules.Hash.Abstractions.Enums
{
    /// <summary>
    /// Формат вывода хеша
    /// </summary>
    public enum HashOutputFormat
    {
        /// <summary>Шестнадцатеричный нижний регистр (abcdef01...)</summary>
        LowerHex,

        /// <summary>Шестнадцатеричный верхний регистр (ABCDEF01...)</summary>
        UpperHex,

        /// <summary>Base64</summary>
        Base64,

        /// <summary>Base64 URL-safe</summary>
        Base64Url
    }
}
