using System;

namespace VTBL.Restrict.Loader.Application.Uploads
{
    public sealed class UploadRestrictFileResult
    {
        public bool Success { get; set; }
        public Guid? CorrelationId { get; set; }
        public string StoredFilePath { get; set; }
        public string ErrorCode { get; set; }
        public string Message { get; set; }
    }
}

