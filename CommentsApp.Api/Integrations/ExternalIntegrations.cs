using CommentsApp.Api.Models;

namespace CommentsApp.Api.Integrations;

public interface IMessageBrokerPublisher
{
    Task PublishCommentCreatedAsync(Comment comment, CancellationToken cancellationToken = default);
}

public interface ISearchIndexer
{
    Task IndexCommentAsync(Comment comment, CancellationToken cancellationToken = default);
}
