using System.Text;
using System.Text.Json;
using CommentsApp.Api.Models;
using RabbitMQ.Client;

namespace CommentsApp.Api.Integrations;

public sealed class RabbitMqPublisher(IConfiguration configuration, ILogger<RabbitMqPublisher> logger)
    : IMessageBrokerPublisher, IAsyncDisposable
{
    private const string QueueName = "comments.created";
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task PublishCommentCreatedAsync(Comment comment, CancellationToken cancellationToken = default)
    {
        try
        {
            var channel = await GetChannelAsync(cancellationToken);
            var body = JsonSerializer.SerializeToUtf8Bytes(new
            {
                comment.Id,
                comment.UserName,
                comment.Email,
                comment.Text,
                comment.CreatedAtUtc,
                comment.ParentCommentId
            });

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: QueueName,
                body: body,
                cancellationToken: cancellationToken);

            logger.LogInformation("Published comment.created event for {CommentId}", comment.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish RabbitMQ event for comment {CommentId}", comment.Id);
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_channel is { IsOpen: true })
            {
                return _channel;
            }

            var host = configuration["RabbitMQ:Host"] ?? "rabbitmq";
            var port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672");
            var user = configuration["RabbitMQ:User"] ?? "guest";
            var pass = configuration["RabbitMQ:Password"] ?? "guest";

            var factory = new ConnectionFactory
            {
                HostName = host,
                Port = port,
                UserName = user,
                Password = pass,
                AutomaticRecoveryEnabled = true
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
            await _channel.QueueDeclareAsync(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: cancellationToken);

            return _channel;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync();
            _channel.Dispose();
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }

        _lock.Dispose();
    }
}
