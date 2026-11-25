using System.Text.Json;
using IgFoodBot.Api.Models;
using IgFoodBot.Api.Options;
using IgFoodBot.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IgFoodBot.Api.Controllers;

[ApiController]
[Route("webhook")]
public sealed class WebhookController(
    WebhookSignatureValidator signatureValidator,
    MessageProcessor processor,
    IOptions<InstagramOptions> options,
    ILogger<WebhookController> logger) : ControllerBase
{
    [HttpGet]
    public IActionResult VerifySubscription(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        return mode == "subscribe" && verifyToken == options.Value.VerifyToken
            ? Content(challenge ?? string.Empty)
            : StatusCode(StatusCodes.Status403Forbidden);
    }

    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> ReceiveEvents(CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, cancellationToken);
        var body = buffer.ToArray();

        if (!signatureValidator.IsValid(body, Request.Headers["X-Hub-Signature-256"]))
        {
            logger.LogWarning("Rejected webhook with invalid signature");
            return Unauthorized();
        }

        var payload = JsonSerializer.Deserialize<WebhookPayload>(body);
        if (payload is not null)
        {
            await processor.HandleAsync(payload, CancellationToken.None);
        }

        return Ok();
    }
}
