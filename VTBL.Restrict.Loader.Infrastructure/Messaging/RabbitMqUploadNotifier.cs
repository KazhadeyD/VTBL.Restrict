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
    /// Публикация сообщения о загрузке файла в RabbitMQ (topic exchange).
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

        /// <inheritdoc />
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

        private void PublishCore(AppUploadedMessage message, string routingKey)
        {
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

        private static byte[] BuildRabbitBody(AppUploadedMessage message)
        {
            var envelope = new RabbitEnvelope
            {
                Method = "IllegalCompaniesLoaderProcessor",
                Payload = JsonSerializer.Serialize(new RabbitPayload
                {
                    SessionId = message.CorrelationId.ToString(),
                    UserId = "stub-user-id",
                    UserName = "stub-user-name",
                    FilePath = message.FilePath,
                    AdditionalInfo = "stub-info",
                    RequestDate = message.UploadedAtUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                }, SerializerOptions)
            };

            return JsonSerializer.SerializeToUtf8Bytes(envelope, SerializerOptions);
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
        }
    }
}
