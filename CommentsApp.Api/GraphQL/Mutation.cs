using CommentsApp.Api.Data;
using CommentsApp.Api.Models;
using CommentsApp.Api.Services;
using HotChocolate.Subscriptions;

namespace CommentsApp.Api.GraphQL;

public sealed class Mutation
{
    public async Task<CommentPayload> CreateCommentAsync(
        CreateCommentInput input,
        [Service] AppDbContext dbContext,
        [Service] ICommentSanitizer sanitizer,
        [Service] ICaptchaService captchaService,
        [Service] ITopicEventSender sender,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.UserName) ||
            !System.Text.RegularExpressions.Regex.IsMatch(input.UserName, @"^[a-zA-Z0-9]+$"))
        {
            return new CommentPayload("UserName must be alphanumeric.");
        }

        if (string.IsNullOrWhiteSpace(input.Email) ||
            !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(input.Email))
        {
            return new CommentPayload("Invalid email format.");
        }

        if (!await captchaService.ValidateAsync(input.CaptchaChallengeId, input.CaptchaCode, cancellationToken))
        {
            return new CommentPayload("Invalid captcha.");
        }

        if (!sanitizer.TrySanitize(input.Text, out var sanitizedText))
        {
            return new CommentPayload("Invalid message markup.");
        }

        var ip = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var comment = new Comment
        {
            UserName = input.UserName,
            Email = input.Email,
            HomePage = input.HomePage,
            Text = sanitizedText,
            ParentCommentId = input.ParentCommentId,
            IpAddress = ip
        };

        dbContext.Comments.Add(comment);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = new CommentGqlDto
        {
            Id = comment.Id,
            UserName = comment.UserName,
            Email = comment.Email,
            HomePage = comment.HomePage,
            Text = comment.Text,
            CreatedAtUtc = comment.CreatedAtUtc,
            ParentCommentId = comment.ParentCommentId
        };

        // Publish to GraphQL subscription topic
        await sender.SendAsync(nameof(Subscription.OnCommentCreated), dto, cancellationToken);

        return new CommentPayload(dto);
    }
}
