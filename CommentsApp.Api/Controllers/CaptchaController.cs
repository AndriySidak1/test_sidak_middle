using CommentsApp.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CommentsApp.Api.Controllers;

[ApiController]
[Route("api/captcha")]
public sealed class CaptchaController(ICaptchaService captchaService) : ControllerBase
{
    [HttpGet("new")]
    public async Task<IActionResult> New(CancellationToken cancellationToken)
    {
        var challenge = await captchaService.CreateChallengeAsync(cancellationToken);
        return Ok(new
        {
            challenge.ChallengeId,
            challenge.ImageBase64
        });
    }
}
