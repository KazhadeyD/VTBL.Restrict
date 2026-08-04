namespace VTBL.Restrict.Loader.Domain.ListTypes
{
    /// <summary>
    /// Справочная информация о типе рестриктивного списка для UI и маршрутизации.
    /// </summary>
    public sealed class ListTypeInfo
    {
        public string Code { get; set; }
        public string Name { get; set; }
        /// <summary>
        /// Каталог (RemoteRoot), куда нужно выкладывать файл для данного типа списка.
        /// </summary>
        public string RemoteRoot { get; set; }
    }
}
