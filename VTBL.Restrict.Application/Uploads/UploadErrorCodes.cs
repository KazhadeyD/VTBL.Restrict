namespace VTBL.Restrict.Application.Uploads
{
    /// <summary>
    /// Коды ошибок сценария загрузки (UC-02).
    /// </summary>
    public static class UploadErrorCodes
    {
        public const string Validation = "Validation";
        public const string Share = "Share";
        public const string Rmq = "Rmq";
        public const string Db = "Db";
        public const string NotFound = "NotFound";
        public const string Conflict = "Conflict";
        public const string Unexpected = "Unexpected";
    }
}
