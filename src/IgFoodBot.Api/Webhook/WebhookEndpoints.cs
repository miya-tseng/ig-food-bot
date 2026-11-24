using System.Text.Json;
using IgFoodBot.Api.Instagram;
using IgFoodBot.Api.Options;
using IgFoodBot.Api.Processing;
using Microsoft.Extensions.Options;

namespace IgFoodBot.Api.Webhook;

public static class WebhookEndpoints
{
    private const string SignatureHeader = "X-Hub-Signature-256";

    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/webhook", VerifySubscription);
        endpoints.MapPost("/webhook", ReceiveEventsAsync);
        return endpoints;
    }

    private static IResult VerifySubscription(HttpRequest request, IOptions<InstagramOptions> options)
    {
        var mode = request.Query["hub.mode"].ToString();
        var verifyToken = request.Query["hub.verify_token"].ToString();
        var challenge = request.Query["hub.challenge"].ToString();

        return mode == "subscribe" && verifyToken == options.Value.VerifyToken
            ? Results.Text(challenge)
            : Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> ReceiveEventsAsync(
        HttpRequest request,
        WebhookSignatureValidator signatureValidator,
        MessageProcessor processor,
        ILogger<MessageProcessor> logger,
        CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync(request, cancellationToken);
        if (!signatureValidator.IsValid(body, request.Headers[SignatureHeader]))
        {
            logger.LogWarning("Rejected webhook with invalid signature");
            return Results.Unauthorized();
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Webhook payload: {Body}", System.Text.Encoding.UTF8.GetString(body));
        }

        if (!TryParsePayload(body, out var payload, out var parseError))
        {
            logger.LogWarning(parseError, "Ignored webhook with malformed payload");
            return Results.Ok();
        }

        await processor.HandleAsync(payload, CancellationToken.None);
        return Results.Ok();
    }

    private static async Task<byte[]> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    private static bool TryParsePayload(byte[] body, out WebhookPayload payload, out JsonException? error)
    {
        try
        {
            payload = JsonSerializer.Deserialize<WebhookPayload>(body) ?? new WebhookPayload(null, null);
            error = null;
            return true;
        }
        catch (JsonException exception)
        {
            payload = new WebhookPayload(null, null);
            error = exception;
            return false;
        }
    }
}
