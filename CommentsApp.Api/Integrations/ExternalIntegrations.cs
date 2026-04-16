using CommentsApp.Api.Models;

namespace CommentsApp.Api.Integrations;

public interface IMessageBrokerPublisher
{
    Task PublishCommentCreatedAsync(Comment comment, CancellationToken cancellationToken = default);
}

public interface ISearchIndexer
{
    Task IndexCommentAsync(Comment comment, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CommentDocument>> SearchAsync(string query, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
