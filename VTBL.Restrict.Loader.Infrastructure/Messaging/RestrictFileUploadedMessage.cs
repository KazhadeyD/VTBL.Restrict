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
        [JsonPropertyName("Method")]
        public string Method { get; set; }

        [JsonPropertyName("Payload")]
        public string Payload { get; set; }

        public static RestrictFileUploadedMessage FromAppMessage(Application.Abstractions.RestrictFileUploadedMessage source)
        {
            var payload = new RabbitPayload
            {
                SessionId = source.CorrelationId.ToString(),
                UserId = "stub-user-id",
                UserName = "stub-user-name",
                FilePath = source.FilePath,
                AdditionalInfo = "stub-info",
                RequestDate = source.UploadedAtUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };

            return new RestrictFileUploadedMessage
            {
                Method = "IllegalCompaniesLoaderProcessor",
                Payload = JsonSerializer.Serialize(payload, SerializerOptions)
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

        private sealed class RabbitPayload
        {
            [JsonPropertyName("SessionId")]
            public string SessionId { get; set; }

            [JsonPropertyName("UserId")]
            public string UserId { get; set; }

            [JsonPropertyName("UserName")]
            public string UserName { get; set; }

            [JsonPropertyName("FilePath")]
            public string FilePath { get; set; }

            [JsonPropertyName("AdditionalInfo")]
            public string AdditionalInfo { get; set; }

            [JsonPropertyName("RequestDate")]
            public string RequestDate { get; set; }
        }
    }
}
