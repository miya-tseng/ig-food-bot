using IgFoodBot.Api.Currency;
using IgFoodBot.Api.Gemini;
using IgFoodBot.Api.Instagram;
using IgFoodBot.Api.Places;

namespace IgFoodBot.Api.Processing;

public sealed class MessageProcessor(
    InstagramMessenger messenger,
    GeminiClient gemini,
    PlacesClient places,
    ExchangeRateClient exchangeRates,
    IncomingMessageFilter messageFilter,
    TimeProvider timeProvider,
    ILogger<MessageProcessor> logger)
{
    private const int MaxImagesPerMessage = 3;

    public async Task HandleAsync(WebhookPayload payload, CancellationToken cancellationToken)
    {
        var incomingMessages = payload.Entry?
            .SelectMany(entry => (entry.Messaging ?? []).Select(messagingEvent => (entry, messagingEvent)))
            .Where(pair => messageFilter.ShouldHandle(pair.entry, pair.messagingEvent))
            .Select(pair => (Participants: new ConversationParticipants(pair.entry.Id!, pair.messagingEvent.Sender!.Id), Message: pair.messagingEvent.Message!))
            .ToList() ?? [];

        foreach (var (participants, message) in incomingMessages)
        {
            await HandleMessageAsync(participants, message, cancellationToken);
        }
    }

    private async Task HandleMessageAsync(ConversationParticipants participants, IncomingMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling message {Mid} from {SenderId} with attachments [{AttachmentTypes}]",
            message.Mid, participants.SenderId, string.Join(", ", message.Attachments?.Select(attachment => attachment.Type) ?? []));

        try
        {
            var replies = await BuildRepliesAsync(message, cancellationToken);
            await SendRepliesAsync(participants, replies, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Failed to handle message {Mid}", message.Mid);
            await TrySendFailureNoticeAsync(participants, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<OutgoingMessage>> BuildRepliesAsync(IncomingMessage message, CancellationToken cancellationToken)
    {
        var postText = CollectText(message);
        var images = await CollectImagesAsync(message, cancellationToken);
        if (string.IsNullOrWhiteSpace(postText) && images.Count == 0)
        {
            return [new TextMessage(ReplyFormatter.NoContent)];
        }

        var insight = await gemini.AnalyzeAsync(postText, images, cancellationToken);
        var searchQueries = SearchQueryBuilder.Build(insight);
        if (insight is not { IsRestaurantPost: true } || searchQueries.Count == 0)
        {
            return [new TextMessage(ReplyFormatter.NotRecognized)];
        }

        var place = await FindFirstRestaurantAsync(searchQueries, insight.CountryCode, cancellationToken);
        logger.LogInformation("Resolved {RestaurantName} with queries [{Queries}] to {PlaceName}",
            insight.RestaurantName, string.Join(" | ", searchQueries), place?.Name ?? "(not found)");
        var priceInTwd = await exchangeRates.ConvertToTwdAsync(place?.PriceRange, cancellationToken);
        return ReplyFormatter.Format(insight, place, searchQueries[0], priceInTwd, timeProvider.GetUtcNow());
    }

    private async Task<PlaceResult?> FindFirstRestaurantAsync(IReadOnlyList<string> searchQueries, string? countryCode, CancellationToken cancellationToken)
    {
        foreach (var query in searchQueries)
        {
            var place = await places.FindRestaurantAsync(query, countryCode, cancellationToken);
            if (place is not null)
            {
                return place;
            }
        }

        return null;
    }

    private async Task SendRepliesAsync(ConversationParticipants participants, IReadOnlyList<OutgoingMessage> replies, CancellationToken cancellationToken)
    {
        foreach (var reply in replies)
        {
            logger.LogDebug("Sending reply to {SenderId}: {Reply}", participants.SenderId, reply.ToPlainText());
            await messenger.SendAsync(participants, reply, cancellationToken);
        }
    }

    private async Task TrySendFailureNoticeAsync(ConversationParticipants participants, CancellationToken cancellationToken)
    {
        try
        {
            await messenger.SendTextAsync(participants, ReplyFormatter.Failed, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Failed to send failure notice to {SenderId}", participants.SenderId);
        }
    }

    private static string CollectText(IncomingMessage message)
    {
        var attachmentTitles = message.Attachments?.Select(attachment => attachment.Payload?.Title) ?? [];
        var texts = attachmentTitles.Prepend(message.Text).Where(text => !string.IsNullOrWhiteSpace(text));
        return string.Join('\n', texts).Trim();
    }

    private async Task<List<ImageInput>> CollectImagesAsync(IncomingMessage message, CancellationToken cancellationToken)
    {
        var urls = message.Attachments?
            .Select(attachment => attachment.Payload?.Url)
            .OfType<string>()
            .Take(MaxImagesPerMessage) ?? [];

        var downloads = await Task.WhenAll(urls.Select(url => messenger.TryDownloadImageAsync(url, cancellationToken)));
        return downloads.OfType<ImageInput>().ToList();
    }
}
