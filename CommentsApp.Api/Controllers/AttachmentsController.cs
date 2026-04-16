using CommentsApp.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CommentsApp.Api.Controllers;

[ApiController]
[Route("api/attachments")]
public sealed class AttachmentsController(AppDbContext dbContext, IWebHostEnvironment environment) : ControllerBase
{
    private static readonly System.Text.RegularExpressions.Regex SafeFileName =
        new(@"^[a-f0-9]+\.(jpg|gif|png|txt)$", System.Text.RegularExpressions.RegexOptions.Compiled);

    [HttpGet("{storedFileName}")]
    public async Task<IActionResult> Get(string storedFileName, CancellationToken cancellationToken)
    {
        // Prevent path traversal: only allow safe hex filenames with known extensions
        if (!SafeFileName.IsMatch(storedFileName))
        {
            return BadRequest();
        }

        var attachment = await dbContext.CommentAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.StoredFileName == storedFileName, cancellationToken);

        if (attachment is null)
        {
            return NotFound();
        }

        var filePath = System.IO.Path.Combine(environment.ContentRootPath, "wwwroot", "uploads", storedFileName);
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath, cancellationToken);
        return File(fileBytes, attachment.ContentType, attachment.OriginalFileName);
    }
}
