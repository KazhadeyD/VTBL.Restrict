namespace VTBL.Restrict.Loader.Application.Options
{
    /// <summary>
    /// Настройки оболочки загрузки (расширения, лимит размера, RemoteRoot).
    /// </summary>
    public sealed class RestrictStorageOptions
    {
        public const string SectionName = "RestrictStorage";

        public string RemoteRoot { get; set; }

        public string[] AllowedExtensions { get; set; } =
        {
            ".xlsx",
            ".xls",
            ".csv"
        };

        public long MaxFileSizeBytes { get; set; } = 52_428_800L;
    }
}
