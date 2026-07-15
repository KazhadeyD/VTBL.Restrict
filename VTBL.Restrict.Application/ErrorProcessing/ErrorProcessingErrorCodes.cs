namespace VTBL.Restrict.Application.ErrorProcessing
{
    /// <summary>
    /// Коды ошибок сценария обработки ошибок (UC-03 / UC-04).
    /// </summary>
    public static class ErrorProcessingErrorCodes
    {
        public const string Validation = "Validation";
        public const string NotFound = "NotFound";
        public const string Conflict = "Conflict";
        public const string Unavailable = "Unavailable";
        public const string Db = "Db";
        public const string Unexpected = "Unexpected";
    }
}
