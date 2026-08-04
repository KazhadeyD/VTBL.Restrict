using System.IO;

namespace VTBL.Restrict.Loader.Application.Uploads
{
    public sealed class UploadRestrictFileRequest
    {
        public string ListTypeCode { get; set; }
        public string OriginalFileName { get; set; }
        public long ContentLength { get; set; }
        public Stream Content { get; set; }
        public string UploadedBy { get; set; }
    }
}

