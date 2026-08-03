using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VTBL.Restrict.Loader.Infrastructure.Messaging
{
    /// <summary>
    /// JSON-сообщение о загрузке файла для RabbitMQ.
    /// </summary>
    public sealed class RestrictFileUploadedMessage
    {
        [JsonPropertyName("messageType")]
        public string MessageType { get; set; }

        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonPropertyName("correlationId")]
        public Guid CorrelationId { get; set; }

        [JsonPropertyName("listType")]
        public string ListType { get; set; }

        [JsonPropertyName("filePath")]
        public string FilePath { get; set; }

        [JsonPropertyName("originalFileName")]
        public string OriginalFileName { get; set; }

        [JsonPropertyName("uploadedAtUtc")]
        public DateTime UploadedAtUtc { get; set; }

        [JsonPropertyName("uploadedBy")]
        public string UploadedBy { get; set; }

        public static RestrictFileUploadedMessage FromAppMessage(Application.Abstractions.RestrictFileUploadedMessage source)
        {
            return new RestrictFileUploadedMessage
            {
                MessageType = source.MessageType,
                SchemaVersion = source.SchemaVersion,
                CorrelationId = source.CorrelationId,
                ListType = source.ListType,
                FilePath = source.FilePath,
                OriginalFileName = source.OriginalFileName,
                UploadedAtUtc = source.UploadedAtUtc,
                UploadedBy = source.UploadedBy
            };
        }

        public byte[] ToUtf8Json()
        {
            return JsonSerializer.SerializeToUtf8Bytes(this, SerializerOptions);
        }

        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
}
