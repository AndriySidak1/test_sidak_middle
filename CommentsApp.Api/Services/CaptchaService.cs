using CommentsApp.Api.Models;
using StackExchange.Redis;

namespace CommentsApp.Api.Services;

public interface ICaptchaService
{
    Task<CaptchaChallenge> CreateChallengeAsync(CancellationToken cancellationToken = default);
    Task<bool> ValidateAsync(string challengeId, string code, CancellationToken cancellationToken = default);
}

public sealed class CaptchaService(IConnectionMultiplexer redis) : ICaptchaService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    private const string KeyPrefix = "captcha:";

    public async Task<CaptchaChallenge> CreateChallengeAsync(CancellationToken cancellationToken = default)
    {
        var challengeId = Guid.NewGuid().ToString("N");
        var code = GenerateCode(6);
        var svg = $$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="60">
              <rect width="100%" height="100%" fill="#f7f7f7"/>
              <line x1="0" y1="20" x2="180" y2="55" stroke="#c8cdd8" stroke-width="1"/>
              <line x1="10" y1="50" x2="175" y2="10" stroke="#c8cdd8" stroke-width="1"/>
              <text x="15" y="40" font-size="28" font-family="monospace" fill="#263238" letter-spacing="4">{{code}}</text>
            </svg>
            """;
        var imageBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg));

        var db = redis.GetDatabase();
        await db.StringSetAsync($"{KeyPrefix}{challengeId}", code, Ttl);

        return new CaptchaChallenge
        {
            ChallengeId = challengeId,
            Code = string.Empty,
            ImageBase64 = imageBase64
        };
    }

    public async Task<bool> ValidateAsync(string challengeId, string code, CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var key = $"{KeyPrefix}{challengeId}";
        var expected = await db.StringGetAsync(key);

        if (expected.IsNullOrEmpty)
        {
            return false;
        }

        var isValid = string.Equals(expected.ToString(), code, StringComparison.OrdinalIgnoreCase);
        if (isValid)
        {
            await db.KeyDeleteAsync(key);
        }

        return isValid;
    }

    private static string GenerateCode(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Range(0, length)
            .Select(_ => chars[Random.Shared.Next(chars.Length)])
            .ToArray());
    }
}
