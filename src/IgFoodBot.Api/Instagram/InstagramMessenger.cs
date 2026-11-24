using System.Net.Http.Headers;
using IgFoodBot.Api.Gemini;
using IgFoodBot.Api.Options;
using Microsoft.Extensions.Options;

namespace IgFoodBot.Api.Instagram;

public sealed class InstagramMessenger(HttpClient httpClient, IOptions<InstagramOptions> options, ILogger<InstagramMessenger> logger)
{
    private const int MaxMessageLength = 1000;
    private const int MaxCardTextLength = 80;
    private const int MaxButtonTitleLength = 20;
    private const long MaxImageBytes = 8 * 1024 * 1024;

    public Task SendTextAsync(ConversationParticipants participants, string text, CancellationToken cancellationToken) =>
        SendAsync(participants, new TextMessage(text), cancellationToken);

    public async Task SendAsync(ConversationParticipants participants, OutgoingMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await PostMessageAsync(participants, BuildPayload(message), cancellationToken);
        }
        catch (HttpRequestException exception) when (message is LinkCardMessage)
        {
            logger.LogWarning(exception, "Card message rejected, falling back to plain text");
            await PostMessageAsync(participants, BuildPayload(new TextMessage(message.ToPlainText())), cancellationToken);
        }
    }

    private async Task PostMessageAsync(ConversationParticipants participants, object messagePayload, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.GraphApiBaseUrl.TrimEnd('/')}/{settings.GraphApiVersion}/{participants.AccountId}/messages")
        {
            Content = JsonContent.Create(new
            {
                recipient = new { id = participants.SenderId },
                message = messagePayload
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Instagram send failed: {(int)response.StatusCode} {body}");
        }
    }

    private static object BuildPayload(OutgoingMessage message) => message switch
    {
        LinkCardMessage card => new
        {
            attachment = new
            {
                type = "template",
                payload = new
                {
                    template_type = "generic",
                    elements = new[]
                    {
                        new
                        {
                            title = Truncate(card.Title, MaxCardTextLength),
                            subtitle = Truncate(card.Subtitle, MaxCardTextLength),
                            default_action = new { type = "web_url", url = card.Url },
                            buttons = new[] { new { type = "web_url", url = card.Url, title = Truncate(card.ButtonTitle, MaxButtonTitleLength) } }
                        }
                    }
                }
            }
        },
        _ => new { text = Truncate(message.ToPlainText(), MaxMessageLength) }
    };

    public async Task<ImageInput?> TryDownloadImageAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var mimeType = response.Content.Headers.ContentType?.MediaType;
            if (!response.IsSuccessStatusCode || !IsImage(mimeType) || response.Content.Headers.ContentLength > MaxImageBytes)
            {
                return null;
            }

            var data = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return data.Length > MaxImageBytes ? null : new ImageInput(data, mimeType!);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Failed to download attachment");
            return null;
        }
    }

    private static bool IsImage(string? mimeType) =>
        mimeType is not null && mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..(maxLength - 1)] + "…";
}
