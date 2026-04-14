using System.ComponentModel.DataAnnotations;

namespace CommentsApp.Api.Models;

public enum AttachmentType
{
    Image = 1,
    Text = 2
}

public sealed class CommentAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CommentId { get; set; }
    public Comment Comment { get; set; } = null!;

    [MaxLength(260)]
    public required string OriginalFileName { get; set; }

    [MaxLength(260)]
    public required string StoredFileName { get; set; }

    [MaxLength(100)]
    public required string ContentType { get; set; }

    public AttachmentType Type { get; set; }
    public long SizeBytes { get; set; }
}
