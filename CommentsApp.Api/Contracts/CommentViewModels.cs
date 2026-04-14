namespace CommentsApp.Api.Contracts;

public sealed class CommentTreeDto
{
    public Guid Id { get; init; }
    public required string UserName { get; init; }
    public required string Email { get; init; }
    public string? HomePage { get; init; }
    public required string Text { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public Guid? ParentCommentId { get; init; }
    public List<CommentAttachmentDto> Attachments { get; init; } = [];
    public List<CommentTreeDto> Replies { get; init; } = [];
}

public sealed class CommentAttachmentDto
{
    public required string OriginalFileName { get; init; }
    public required string StoredFileName { get; init; }
    public required string ContentType { get; init; }
    public string Type { get; init; } = "unknown";
}
