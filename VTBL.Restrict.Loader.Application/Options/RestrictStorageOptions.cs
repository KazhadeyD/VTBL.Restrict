namespace VTBL.Restrict.Loader.Application.Options
{
    /// <summary>
    /// Правила оболочки файла при загрузке (расширения и лимит размера).
    /// Каталог выкладки задаётся в ListTypes[].remoteRoot, не здесь.
    /// </summary>
    public sealed class RestrictStorageOptions
    {
        public const string SectionName = "RestrictStorage";

        public string[] AllowedExtensions { get; set; } =
        {
            ".xlsx",
            ".xls",
            ".csv"
        };

        public long MaxFileSizeBytes { get; set; } = 52_428_800L;
    }
}
