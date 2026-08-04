using System;

namespace VTBL.Restrict.Loader.Application.Abstractions
{
    /// <summary>
    /// Payload уведомления о загрузке файла.
    /// </summary>
    public sealed class RestrictFileUploadedMessage
    {
        public string MessageType { get; set; }
        public int SchemaVersion { get; set; }
        public Guid CorrelationId { get; set; }
        public string ListType { get; set; }
        public string FilePath { get; set; }
        public string OriginalFileName { get; set; }
        public DateTime UploadedAtUtc { get; set; }
        public string UploadedBy { get; set; }
    }
}

