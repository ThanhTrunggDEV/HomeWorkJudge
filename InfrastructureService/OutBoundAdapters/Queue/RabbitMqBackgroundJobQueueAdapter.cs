using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using InfrastructureService.Common.Errors;
using InfrastructureService.Common.Resilience;
using InfrastructureService.Configuration.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ports.DTO.Common;
using Ports.OutBoundPorts.Queue;
using RabbitMQ.Client;

namespace InfrastructureService.OutBoundAdapters.Queue;

public sealed class RabbitMqBackgroundJobQueueAdapter : IBackgroundJobQueuePort
{
    private readonly IOperationExecutor _operationExecutor;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqBackgroundJobQueueAdapter> _logger;

    public RabbitMqBackgroundJobQueueAdapter(
        IOperationExecutor operationExecutor,
        IOptions<QueueOptions> queueOptions,
        ILogger<RabbitMqBackgroundJobQueueAdapter> logger)
    {
        _operationExecutor = operationExecutor;
        _options = queueOptions.Value.RabbitMq;
        _logger = logger;
    }

    public Task EnqueueAsync(JobEnvelopeDto envelope, CancellationToken cancellationToken = default)
        => _operationExecutor.ExecuteAsync(
            "queue.rabbitmq.enqueue",
            ct => EnqueueInternalAsync(envelope, ct),
            cancellationToken);

    private Task EnqueueInternalAsync(JobEnvelopeDto envelope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (envelope is null)
        {
            throw new ArgumentNullException(nameof(envelope));
        }

        if (string.IsNullOrWhiteSpace(_options.Host) ||
            string.IsNullOrWhiteSpace(_options.Username) ||
            string.IsNullOrWhiteSpace(_options.Password) ||
            string.IsNullOrWhiteSpace(_options.QueueName) ||
            string.IsNullOrWhiteSpace(_options.DeadLetterQueueName))
        {
            throw new InfrastructureException(
                "QUEUE_CONFIGURATION_INVALID",
                "RabbitMQ settings are invalid. Ensure Host, Username, Password, QueueName, and DeadLetterQueueName are configured.");
        }

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                UserName = _options.Username,
                Password = _options.Password,
                DispatchConsumersAsync = true
            };

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            DeclareTopology(channel);

            var payload = JsonSerializer.SerializeToUtf8Bytes(envelope);
            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.MessageId = envelope.CorrelationId;
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            channel.BasicPublish(
                exchange: string.Empty,
                routingKey: _options.QueueName,
                mandatory: false,
                basicProperties: properties,
                body: payload);

            _logger.LogInformation(
                "Published background job {JobName} to RabbitMQ queue {QueueName}.",
                envelope.JobName,
                _options.QueueName);

            return Task.CompletedTask;
        }
        catch (InfrastructureException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish background job {JobName} to RabbitMQ.", envelope.JobName);
            throw new InfrastructureException(
                "QUEUE_PUBLISH_FAILED",
                "Failed to publish background job to RabbitMQ.",
                ex);
        }
    }

    private void DeclareTopology(IModel channel)
    {
        channel.QueueDeclare(
            queue: _options.DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var queueArgs = new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = string.Empty,
            ["x-dead-letter-routing-key"] = _options.DeadLetterQueueName
        };

        channel.QueueDeclare(
            queue: _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArgs);
    }
}
