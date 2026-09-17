using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using VTBL.Restrict.Loader.Application.Abstractions;
using VTBL.Restrict.Loader.Infrastructure.Options;
using AppUploadedMessage = VTBL.Restrict.Loader.Application.Abstractions.RestrictFileUploadedMessage;

namespace VTBL.Restrict.Loader.Infrastructure.Messaging
{
    /// <summary>
    /// Публикует уведомление о загруженном файле в RabbitMQ (topic exchange).
    /// Вся “кухня” с контрактом для потребителя живёт тут, чтобы application-слой не знал детали Rabbit.
    /// </summary>
    public sealed class RabbitMqUploadNotifier : IUploadNotifier
    {
        private readonly RabbitMqOptions _options;
        private readonly ILogger _logger;

        public RabbitMqUploadNotifier(
            IOptions<RabbitMqOptions> options,
            ILogger<RabbitMqUploadNotifier> logger = null)
        {
            _options = options?.Value ?? new RabbitMqOptions();
            _logger = logger ?? NullLogger<RabbitMqUploadNotifier>.Instance;
        }

        /// <summary>
        /// Публикует уведомление о загруженном файле в RabbitMQ.
        /// </summary>
        /// <remarks>
        /// <para>routingKey уже формируется в application-слое (под конкретный ListType).</para>
        /// <para>Тут мы только убеждаемся, что Rabbit настроен, и отправляем сообщение.</para>
        /// </remarks>
        public Task PublishUploadedAsync(
            AppUploadedMessage message,
            string routingKey,
            CancellationToken cancellationToken)
        {
            if (!_options.IsConfigured)
            {
                throw new InvalidOperationException("RabbitMQ host is not configured.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            return Task.Run(() => PublishCore(message, routingKey), cancellationToken);
        }

        /// <summary>
        /// Непосредственно создаёт соединение/канал и делает BasicPublish.
        /// </summary>
        private void PublishCore(AppUploadedMessage message, string routingKey)
        {
            // Здесь мы реально лезем в RabbitMQ. Вынесли в отдельный метод, чтобы main-метод оставался
            // максимально “тонким” (и легче тестировать/логировать).
            _logger.LogInformation(
                "RabbitMQ publish started for exchange {Exchange} routing key {RoutingKey}",
                _options.Exchange,
                routingKey);

            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _options.Host,
                    VirtualHost = _options.VirtualHost ?? "/",
                    UserName = _options.Username,
                    Password = _options.Password,
                    DispatchConsumersAsync = true
                };

                using var connection = factory.CreateConnection();
                using var channel = connection.CreateModel();

                channel.ExchangeDeclare(
                    exchange: _options.Exchange,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false);

                var payload = BuildRabbitBody(message);
                var properties = channel.CreateBasicProperties();
                properties.Persistent = true;
                properties.ContentType = "application/json";

                channel.BasicPublish(
                    exchange: _options.Exchange,
                    routingKey: routingKey,
                    basicProperties: properties,
                    body: payload);

                _logger.LogInformation(
                    "RabbitMQ publish succeeded for exchange {Exchange} routing key {RoutingKey}",
                    _options.Exchange,
                    routingKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "RabbitMQ publish failed for exchange {Exchange} routing key {RoutingKey}",
                    _options.Exchange,
                    routingKey);
                throw;
            }
        }

        // Потребитель ждёт конверт формата: { Method, Payload }, где Payload — строка JSON.
        // Поэтому сначала строим Payload как JSON-строку, а потом оборачиваем в envelope.
        /// <summary>
        /// Собирает “готовый к отправке” body (envelope с Method+Payload) в виде UTF-8 JSON байтов.
        /// </summary>
        private static byte[] BuildRabbitBody(AppUploadedMessage message)
        {
            var envelope = new RabbitEnvelope
            {
                // Contract for the consumer: фиксированный обработчик для этого типа события.
                Method = "RestrictiveListsLoaderProcessor",
                Payload = JsonSerializer.Serialize(new RabbitPayload
                {
                    SessionId = message.CorrelationId.ToString(),
                    UserId = ResolveUserId(message),
                    UserName = ResolveUserName(message),
                    FilePath = message.FilePath,
                    AdditionalInfo = "stub-info",
                    // Дата нужна в UTC и в стабильном ISO-формате, чтобы потребитель не гадал с часовыми поясами.
                    RequestDate = message.UploadedAtUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    ListType = message.MessageType,
                }, SerializerOptions)
            };

            return JsonSerializer.SerializeToUtf8Bytes(envelope, SerializerOptions);
        }

        private static string ResolveUserName(AppUploadedMessage message)
        {
            if (!string.IsNullOrWhiteSpace(message?.UserName))
            {
                return message.UserName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(message?.UploadedBy))
            {
                return message.UploadedBy.Trim();
            }

            return "stub-user-name";
        }

        private static string ResolveUserId(AppUploadedMessage message)
        {
            if (!string.IsNullOrWhiteSpace(message?.UserId))
            {
                return message.UserId.Trim();
            }

            return "stub-user-id";
        }

        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions();

        private sealed class RabbitEnvelope
        {
            public string Method { get; set; }
            public string Payload { get; set; }
        }

        private sealed class RabbitPayload
        {
            public string SessionId { get; set; }
            public string UserId { get; set; }
            public string UserName { get; set; }
            public string FilePath { get; set; }
            public string AdditionalInfo { get; set; }
            public string RequestDate { get; set; }
            public string ListType { get; set; }
            
        }
    }
}
