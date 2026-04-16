using CommentsApp.Api.Data;
using CommentsApp.Api.GraphQL;
using CommentsApp.Api.Hubs;
using CommentsApp.Api.Integrations;
using CommentsApp.Api.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Web", policy =>
        policy.WithOrigins(
                builder.Configuration["Cors:AllowedOrigin"] ?? "http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "redis:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(redisConnection));

builder.Services.AddScoped<ICaptchaService, CaptchaService>();
builder.Services.AddScoped<ICommentSanitizer, CommentSanitizer>();
builder.Services.AddScoped<IFileProcessor, FileProcessor>();
builder.Services.AddSingleton<IMessageBrokerPublisher, RabbitMqPublisher>();
builder.Services.AddSingleton<ISearchIndexer, ElasticsearchIndexer>();
builder.Services.AddHostedService<CommentCreatedConsumer>();

builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .AddSubscriptionType<Subscription>()
    .AddInMemorySubscriptions()
    .AddProjections()
    .AddFiltering()
    .AddSorting();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "Comments API";
    options.Theme = ScalarTheme.Purple;
});

app.UseHttpsRedirection();
app.UseCors("Web");
app.UseWebSockets();
app.UseStaticFiles();
app.MapControllers();
app.MapGraphQL("/graphql");
app.MapHub<CommentsHub>("/hubs/comments");

app.Run();
