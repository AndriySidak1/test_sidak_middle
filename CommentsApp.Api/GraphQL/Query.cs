using CommentsApp.Api.Data;
using CommentsApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CommentsApp.Api.GraphQL;

public sealed class Query
{
    [UsePaging]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Comment> GetComments([Service] AppDbContext dbContext) =>
        dbContext.Comments.AsNoTracking();
}
