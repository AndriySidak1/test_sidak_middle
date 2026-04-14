using CommentsApp.Api.Contracts;
using CommentsApp.Api.Data;
using CommentsApp.Api.Hubs;
using CommentsApp.Api.Integrations;
using CommentsApp.Api.Models;
using CommentsApp.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CommentsApp.Api.Controllers;

[ApiController]
[Route("api/comments")]
public sealed class CommentsController(
    AppDbContext dbContext,
    ICaptchaService captchaService,
    ICommentSanitizer sanitizer,
    IFileProcessor fileProcessor,
    IMessageBrokerPublisher brokerPublisher,
    ISearchIndexer searchIndexer,
    IHubContext<CommentsHub> hubContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetTopLevelComments([FromQuery] CommentQueryRequest query, CancellationToken cancellationToken)
    {
        var request = dbContext.Comments
            .AsNoTracking()
            .Where(x => x.ParentCommentId == null)
            .Include(x => x.Attachments);

        var ordered = (query.SortBy.ToLowerInvariant(), query.SortDirection.ToLowerInvariant()) switch
        {
            ("username", "asc") => request.OrderBy(x => x.UserName),
            ("username", _) => request.OrderByDescending(x => x.UserName),
            ("email", "asc") => request.OrderBy(x => x.Email),
            ("email", _) => request.OrderByDescending(x => x.Email),
            ("createdat", "asc") => request.OrderBy(x => x.CreatedAtUtc),
            _ => request.OrderByDescending(x => x.CreatedAtUtc)
        };

        var total = await ordered.CountAsync(cancellationToken);
        var pageItems = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var rootIds = pageItems.Select(x => x.Id).ToHashSet();
        var allReplies = await dbContext.Comments
            .AsNoTracking()
            .Include(x => x.Attachments)
            .Where(x => x.ParentCommentId != null)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var tree = BuildTree(pageItems, allReplies, rootIds);
        return Ok(new { total, page = query.Page, pageSize = query.PageSize, items = tree });
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { message = "Query is required." });
        }

        if (searchIndexer is ElasticsearchIndexer esIndexer)
        {
            var results = await esIndexer.SearchAsync(q, page, pageSize, cancellationToken);
            return Ok(new { total = results.Count, items = results });
        }

        return StatusCode(501, new { message = "Search not available." });
    }

    [HttpPost("preview")]
    public IActionResult Preview([FromBody] PreviewRequest request)
    {
        if (!sanitizer.TrySanitize(request.Text, out var sanitized))
        {
            return BadRequest(new { message = "Invalid message markup." });
        }

        return Ok(new { html = sanitized });
    }

    [HttpPost]
    [RequestFormLimits(MultipartBodyLengthLimit = 20 * 1024 * 1024)]
    public async Task<IActionResult> Create(
        [FromForm] CreateCommentRequest request,
        IFormFile? attachment,
        CancellationToken cancellationToken)
    {
        if (!await captchaService.ValidateAsync(request.CaptchaChallengeId, request.CaptchaCode, cancellationToken))
        {
            return BadRequest(new { message = "Invalid captcha." });
        }

        if (!sanitizer.TrySanitize(request.Text, out var sanitized))
        {
            return BadRequest(new { message = "Invalid message markup." });
        }

        CommentAttachment? processedFile = null;
        try
        {
            processedFile = await fileProcessor.ProcessAsync(attachment, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var comment = new Comment
        {
            UserName = request.UserName,
            Email = request.Email,
            HomePage = request.HomePage,
            Text = sanitized,
            ParentCommentId = request.ParentCommentId,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
        };

        if (processedFile is not null)
        {
            comment.Attachments.Add(processedFile);
        }

        dbContext.Comments.Add(comment);
        await dbContext.SaveChangesAsync(cancellationToken);

        await brokerPublisher.PublishCommentCreatedAsync(comment, cancellationToken);
        await searchIndexer.IndexCommentAsync(comment, cancellationToken);

        var dto = MapRecursive(comment, new Dictionary<Guid, List<Comment>>());
        await hubContext.Clients.All.SendAsync("CommentCreated", dto, cancellationToken);

        return CreatedAtAction(nameof(GetTopLevelComments), new { id = comment.Id }, dto);
    }

    private static List<CommentTreeDto> BuildTree(List<Comment> roots, List<Comment> allReplies, HashSet<Guid> topLevelIds)
    {
        var byParent = allReplies
            .GroupBy(x => x.ParentCommentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        return roots
            .Where(x => topLevelIds.Contains(x.Id))
            .Select(root => MapRecursive(root, byParent))
            .ToList();
    }

    private static CommentTreeDto MapRecursive(Comment comment, IReadOnlyDictionary<Guid, List<Comment>> byParent)
    {
        var replies = byParent.TryGetValue(comment.Id, out var children)
            ? children.Select(child => MapRecursive(child, byParent)).ToList()
            : [];

        return new CommentTreeDto
        {
            Id = comment.Id,
            UserName = comment.UserName,
            Email = comment.Email,
            HomePage = comment.HomePage,
            Text = comment.Text,
            CreatedAtUtc = comment.CreatedAtUtc,
            ParentCommentId = comment.ParentCommentId,
            Attachments = comment.Attachments.Select(a => new CommentAttachmentDto
            {
                OriginalFileName = a.OriginalFileName,
                StoredFileName = a.StoredFileName,
                ContentType = a.ContentType,
                Type = a.Type.ToString()
            }).ToList(),
            Replies = replies
        };
    }
}

public sealed class PreviewRequest
{
    public string Text { get; init; } = string.Empty;
}
