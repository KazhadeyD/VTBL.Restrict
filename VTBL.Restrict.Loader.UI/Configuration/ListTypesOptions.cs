using System.Collections.Generic;
using VTBL.Restrict.Loader.Domain.ListTypes;

namespace VTBL.Restrict.Loader.UI.Configuration
{
    /// <summary>
    /// Опции ListTypes, загружаемые из конфигурации.
    /// </summary>
    public sealed class ListTypesOptions
    {
        public IReadOnlyList<ListTypeInfo> Items { get; set; } = new List<ListTypeInfo>();
    }
}

