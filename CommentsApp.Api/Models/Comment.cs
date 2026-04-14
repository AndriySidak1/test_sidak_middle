using System.ComponentModel.DataAnnotations;

namespace CommentsApp.Api.Models;

public sealed class Comment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(64)]
    public required string UserName { get; set; }

    [MaxLength(255)]
    public required string Email { get; set; }

    [MaxLength(255)]
    public string? HomePage { get; set; }

    [MaxLength(10_000)]
    public required string Text { get; set; }

    [MaxLength(64)]
    public required string IpAddress { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? ParentCommentId { get; set; }
    public Comment? ParentComment { get; set; }

    public List<Comment> Replies { get; set; } = [];
    public List<CommentAttachment> Attachments { get; set; } = [];
}
