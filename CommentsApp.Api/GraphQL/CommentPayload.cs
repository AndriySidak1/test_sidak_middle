namespace CommentsApp.Api.GraphQL;

public sealed class CreateCommentInput
{
    public required string UserName { get; init; }
    public required string Email { get; init; }
    public string? HomePage { get; init; }
    public required string Text { get; init; }
    public Guid? ParentCommentId { get; init; }
}

public sealed class CommentPayload
{
    public CommentPayload(CommentGqlDto comment) => Comment = comment;
    public CommentPayload(string error) => Errors = [error];

    public CommentGqlDto? Comment { get; }
    public IReadOnlyList<string>? Errors { get; }
}

public sealed class CommentGqlDto
{
    public Guid Id { get; init; }
    public required string UserName { get; init; }
    public required string Email { get; init; }
    public string? HomePage { get; init; }
    public required string Text { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public Guid? ParentCommentId { get; init; }
}
