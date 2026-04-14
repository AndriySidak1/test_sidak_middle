using System.ComponentModel.DataAnnotations;

namespace CommentsApp.Api.Contracts;

public sealed class CreateCommentRequest
{
    [Required]
    [RegularExpression("^[a-zA-Z0-9]+$")]
    public required string UserName { get; init; }

    [Required]
    [EmailAddress]
    public required string Email { get; init; }

    [Url]
    public string? HomePage { get; init; }

    [Required]
    public required string CaptchaChallengeId { get; init; }

    [Required]
    [RegularExpression("^[a-zA-Z0-9]+$")]
    public required string CaptchaCode { get; init; }

    [Required]
    public required string Text { get; init; }

    public Guid? ParentCommentId { get; init; }
}

public sealed class CommentQueryRequest
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 25;

    public string SortBy { get; init; } = "createdAt";
    public string SortDirection { get; init; } = "desc";
}
