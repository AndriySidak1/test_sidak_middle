using CommentsApp.Api.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace CommentsApp.Api.Services;

public interface IFileProcessor
{
    Task<CommentAttachment?> ProcessAsync(IFormFile? file, CancellationToken cancellationToken = default);
}

public sealed class FileProcessor(IWebHostEnvironment environment) : IFileProcessor
{
    private static readonly HashSet<string> ImageExtensions = [".jpg", ".jpeg", ".png", ".gif"];

    public async Task<CommentAttachment?> ProcessAsync(IFormFile? file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return null;
        }

        var uploads = System.IO.Path.Combine(environment.ContentRootPath, "wwwroot", "uploads");
        Directory.CreateDirectory(uploads);

        var extension = System.IO.Path.GetExtension(file.FileName).ToLowerInvariant();

        if (ImageExtensions.Contains(extension))
        {
            var storedName = $"{Guid.NewGuid():N}.jpg";
            var storedPath = System.IO.Path.Combine(uploads, storedName);
            await using var imageStream = file.OpenReadStream();
            using var image = await Image.LoadAsync(imageStream, cancellationToken);
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(320, 240)
            }));
            await image.SaveAsJpegAsync(storedPath, new JpegEncoder { Quality = 85 }, cancellationToken);

            return new CommentAttachment
            {
                OriginalFileName = file.FileName,
                StoredFileName = storedName,
                ContentType = "image/jpeg",
                Type = AttachmentType.Image,
                SizeBytes = new FileInfo(storedPath).Length
            };
        }

        if (extension == ".txt")
        {
            if (file.Length > 100 * 1024)
            {
                throw new InvalidOperationException("Text file cannot be larger than 100kb.");
            }

            var storedName = $"{Guid.NewGuid():N}.txt";
            var storedPath = System.IO.Path.Combine(uploads, storedName);
            await using var output = File.Create(storedPath);
            await file.CopyToAsync(output, cancellationToken);

            return new CommentAttachment
            {
                OriginalFileName = file.FileName,
                StoredFileName = storedName,
                ContentType = "text/plain",
                Type = AttachmentType.Text,
                SizeBytes = file.Length
            };
        }

        throw new InvalidOperationException("Unsupported attachment type.");
    }
}
