using IgFoodBot.Api.Models;
using IgFoodBot.Api.Options;
using Microsoft.Extensions.Options;

namespace IgFoodBot.Api.Services;

public sealed class IncomingMessageFilter(
    ProcessedMessageTracker processedMessages,
    IOptions<InstagramOptions> instagramOptions,
    ILogger<IncomingMessageFilter> logger)
{
    public bool ShouldHandle(WebhookEntry entry, MessagingEvent messagingEvent)
    {
        var senderId = messagingEvent.Sender?.Id;
        var message = messagingEvent.Message;
        if (entry.Id is null || senderId is null || message is null || message.IsEcho || senderId == entry.Id)
        {
            return false;
        }

        if (!IsAllowedSender(senderId))
        {
            logger.LogWarning("Ignored message from unlisted sender {SenderId}", senderId);
            return false;
        }

        return message.Mid is null || processedMessages.TryMarkAsProcessed(message.Mid);
    }

    private bool IsAllowedSender(string senderId)
    {
        var allowedSenderIds = instagramOptions.Value.AllowedSenderIds;
        return allowedSenderIds.Length == 0 || allowedSenderIds.Contains(senderId);
    }
}
