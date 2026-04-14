using CommentsApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CommentsApp.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<CommentAttachment> CommentAttachments => Set<CommentAttachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Comment>(entity =>
        {
            entity.HasIndex(x => x.CreatedAtUtc);
            entity.HasIndex(x => x.Email);
            entity.HasIndex(x => x.UserName);

            entity.HasMany(x => x.Replies)
                .WithOne(x => x.ParentComment)
                .HasForeignKey(x => x.ParentCommentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CommentAttachment>(entity =>
        {
            entity.HasOne(x => x.Comment)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.CommentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
