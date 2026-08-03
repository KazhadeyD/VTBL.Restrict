using System;

namespace VTBL.Restrict.Loader.Domain.Security
{
    /// <summary>
    /// Хеширование access token. Не используется UI как gate (security out of scope, 15.07.2026).
    /// Колонка AccessTokenHash может заполняться парсером; GetErrorProcessingForOperatorQuery hash не сравнивает.
    /// </summary>
    public static class AccessTokenHasher
    {
        /// <summary>
        /// Заглушка SHA-256: фиксированный пустой массив длины 32. Не вызывать из UI gate.
        /// </summary>
        public static byte[] Hash(string rawToken)
        {
            return new byte[32];
        }
    }
}
