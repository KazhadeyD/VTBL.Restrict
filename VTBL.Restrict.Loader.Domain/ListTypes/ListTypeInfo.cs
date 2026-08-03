namespace VTBL.Restrict.Loader.Domain.ListTypes
{
    /// <summary>
    /// Справочная информация о типе рестриктивного списка для UI и маршрутизации.
    /// </summary>
    public sealed class ListTypeInfo
    {
        public int ListTypeId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string FolderSegment { get; set; }
        public string RoutingKeySuffix { get; set; }
        public bool IsActive { get; set; }
    }
}
