using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;

namespace CommentsApp.Api.Integrations;

/// <summary>
/// Background service that consumes "comments.created" events from RabbitMQ.
/// On each event it increments the Redis counter used for fast total-count reads.
/// </summary>
public sealed class CommentCreatedConsumer(
    IConfiguration configuration,
    IConnectionMultiplexer redis,
    ILogger<CommentCreatedConsumer> logger) : BackgroundService
{
    public const string QueueName = "comments.created";
    public const string TotalCountKey = "comments:toplevel:total";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Retry loop — RabbitMQ may not be ready immediately on startup
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "RabbitMQ consumer error — retrying in 5 s");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
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

        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        // Fair dispatch — process one message at a time
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                var msg = JsonSerializer.Deserialize<CommentCreatedMessage>(body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (msg is not null)
                {
                    logger.LogInformation(
                        "Consumed comment.created: Id={Id} User={User} IsReply={IsReply}",
                        msg.Id, msg.UserName, msg.ParentCommentId.HasValue);

                    // Update Redis counter only for top-level comments
                    if (!msg.ParentCommentId.HasValue)
                    {
                        var db = redis.GetDatabase();
                        await db.StringIncrementAsync(TotalCountKey);
                    }
                }

                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing comment.created message");
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        await channel.BasicConsumeAsync(queue: QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

        logger.LogInformation("RabbitMQ consumer started on queue '{Queue}'", QueueName);

        // Hold until app shuts down
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}

public sealed class CommentCreatedMessage
{
    public Guid Id { get; init; }
    public required string UserName { get; init; }
    public required string Email { get; init; }
    public required string Text { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public Guid? ParentCommentId { get; init; }
}
