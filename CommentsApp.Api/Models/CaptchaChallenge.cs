namespace CommentsApp.Api.Models;

public sealed class CaptchaChallenge
{
    public required string ChallengeId { get; init; }
    public required string Code { get; init; }
    public required string ImageBase64 { get; init; }
}
