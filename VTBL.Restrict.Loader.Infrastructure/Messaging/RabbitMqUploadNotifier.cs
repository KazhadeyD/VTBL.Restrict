using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using VTBL.Restrict.Loader.Application.Abstractions;
using VTBL.Restrict.Loader.Infrastructure.Options;
using AppUploadedMessage = VTBL.Restrict.Loader.Application.Abstractions.RestrictFileUploadedMessage;

namespace VTBL.Restrict.Loader.Infrastructure.Messaging
{
    /// <summary>
    /// Публикация RestrictFileUploaded в topic exchange (EC-04).
    /// </summary>
    public sealed class RabbitMqUploadNotifier : IUploadNotifier
    {
        private readonly RabbitMqOptions _options;

        public RabbitMqUploadNotifier(IOptions<RabbitMqOptions> options)
        {
            _options = options?.Value ?? new RabbitMqOptions();
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

            var payload = RestrictFileUploadedMessage.FromAppMessage(message).ToUtf8Json();
            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";

            channel.BasicPublish(
                exchange: _options.Exchange,
                routingKey: routingKey,
                basicProperties: properties,
                body: payload);
        }
    }
}
