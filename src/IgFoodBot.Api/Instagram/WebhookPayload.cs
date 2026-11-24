using System.Text.Json.Serialization;

namespace IgFoodBot.Api.Instagram;

public sealed record WebhookPayload(
    [property: JsonPropertyName("object")] string? Object,
    [property: JsonPropertyName("entry")] List<WebhookEntry>? Entry);

public sealed record WebhookEntry(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("messaging")] List<MessagingEvent>? Messaging);

public sealed record MessagingEvent(
    [property: JsonPropertyName("sender")] Participant? Sender,
    [property: JsonPropertyName("recipient")] Participant? Recipient,
    [property: JsonPropertyName("message")] IncomingMessage? Message);

public sealed record Participant(
    [property: JsonPropertyName("id")] string Id);

public sealed record IncomingMessage(
    [property: JsonPropertyName("mid")] string? Mid,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("is_echo")] bool IsEcho,
    [property: JsonPropertyName("attachments")] List<MessageAttachment>? Attachments);

public sealed record MessageAttachment(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("payload")] AttachmentPayload? Payload);

public sealed record AttachmentPayload(
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("title")] string? Title);
