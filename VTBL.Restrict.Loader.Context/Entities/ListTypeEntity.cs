using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace VTBL.Restrict.Loader.Context.Entities
{
    [Table("ListType", Schema = "restrict")]
    public sealed class ListTypeEntity
    {
        public int ListTypeId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string FolderSegment { get; set; }
        public string RoutingKeySuffix { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public ICollection<UploadBatchEntity> UploadBatches { get; set; }
    }
}

