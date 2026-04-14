using CommentsApp.Api.Models;
using Elastic.Clients.Elasticsearch;

namespace CommentsApp.Api.Integrations;

public sealed class ElasticsearchIndexer : ISearchIndexer
{
    private const string IndexName = "comments";
    private readonly ElasticsearchClient _client;
    private readonly ILogger<ElasticsearchIndexer> _logger;

    public ElasticsearchIndexer(IConfiguration configuration, ILogger<ElasticsearchIndexer> logger)
    {
        _logger = logger;
        var url = configuration["Elasticsearch:Url"] ?? "http://elasticsearch:9200";
        var settings = new ElasticsearchClientSettings(new Uri(url))
            .DefaultIndex(IndexName);
        _client = new ElasticsearchClient(settings);
    }

    public async Task IndexCommentAsync(Comment comment, CancellationToken cancellationToken = default)
    {
        try
        {
            var doc = new CommentDocument
            {
                Id = comment.Id,
                UserName = comment.UserName,
                Email = comment.Email,
                Text = comment.Text,
                CreatedAtUtc = comment.CreatedAtUtc,
                ParentCommentId = comment.ParentCommentId
            };

            var response = await _client.IndexAsync(doc, i => i.Index(IndexName).Id(comment.Id.ToString()), cancellationToken);
            if (!response.IsSuccess())
            {
                _logger.LogWarning("Elasticsearch index failed for {CommentId}: {Error}",
                    comment.Id, response.ElasticsearchServerError?.Error?.Reason);
            }
            else
            {
                _logger.LogInformation("Indexed comment {CommentId} in Elasticsearch", comment.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index comment {CommentId} in Elasticsearch", comment.Id);
        }
    }

    public async Task<IReadOnlyList<CommentDocument>> SearchAsync(
        string query,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.SearchAsync<CommentDocument>(s => s
                .Indices(IndexName)
                .From((page - 1) * pageSize)
                .Size(pageSize)
                .Query(q => q.MultiMatch(m => m
                    .Query(query)
                    .Fields(new[] { "userName", "email", "text" }))),
                cancellationToken);

            if (!response.IsSuccess())
            {
                _logger.LogWarning("Elasticsearch search failed: {Error}",
                    response.ElasticsearchServerError?.Error?.Reason);
                return [];
            }

            return response.Documents.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Elasticsearch search error");
            return [];
        }
    }
}

public sealed class CommentDocument
{
    public Guid Id { get; set; }
    public required string UserName { get; set; }
    public required string Email { get; set; }
    public required string Text { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid? ParentCommentId { get; set; }
}
