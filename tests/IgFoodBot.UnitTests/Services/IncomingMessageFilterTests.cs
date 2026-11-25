using IgFoodBot.Api.Models;
using IgFoodBot.Api.Options;
using IgFoodBot.Api.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace IgFoodBot.UnitTests.Services;

public class IncomingMessageFilterTests
{
    private const string BotAccountId = "17841400000000000";
    private const string SenderId = "1128000000000000";

    [Test]
    public void ShouldHandle_WithMessageFromUser_ReturnsTrue()
    {
        var filter = CreateFilter();

        Assert.That(filter.ShouldHandle(Entry(), Event(SenderId, Message("m1"))), Is.True);
    }

    [Test]
    public void ShouldHandle_WithEchoOfBotReply_ReturnsFalse()
    {
        var filter = CreateFilter();

        Assert.That(filter.ShouldHandle(Entry(), Event(SenderId, Message("m1", isEcho: true))), Is.False);
    }

    [Test]
    public void ShouldHandle_WhenSenderIsBotAccount_ReturnsFalse()
    {
        var filter = CreateFilter();

        Assert.That(filter.ShouldHandle(Entry(), Event(BotAccountId, Message("m1"))), Is.False);
    }

    [Test]
    public void ShouldHandle_WithoutMessage_ReturnsFalse()
    {
        var filter = CreateFilter();

        Assert.That(filter.ShouldHandle(Entry(), Event(SenderId, null)), Is.False);
    }

    [Test]
    public void ShouldHandle_WithoutEntryId_ReturnsFalse()
    {
        var filter = CreateFilter();

        Assert.That(filter.ShouldHandle(new WebhookEntry(null, []), Event(SenderId, Message("m1"))), Is.False);
    }

    [Test]
    public void ShouldHandle_WithSameMessageIdTwice_HandlesOnlyFirst()
    {
        var filter = CreateFilter();
        var messagingEvent = Event(SenderId, Message("m1"));

        Assert.Multiple(() =>
        {
            Assert.That(filter.ShouldHandle(Entry(), messagingEvent), Is.True);
            Assert.That(filter.ShouldHandle(Entry(), messagingEvent), Is.False);
        });
    }

    [Test]
    public void ShouldHandle_WithoutMessageId_HandlesEveryTime()
    {
        var filter = CreateFilter();
        var messagingEvent = Event(SenderId, Message(null));

        Assert.Multiple(() =>
        {
            Assert.That(filter.ShouldHandle(Entry(), messagingEvent), Is.True);
            Assert.That(filter.ShouldHandle(Entry(), messagingEvent), Is.True);
        });
    }

    [Test]
    public void ShouldHandle_WhenSenderIsInAllowList_ReturnsTrue()
    {
        var filter = CreateFilter(allowedSenderIds: [SenderId]);

        Assert.That(filter.ShouldHandle(Entry(), Event(SenderId, Message("m1"))), Is.True);
    }

    [Test]
    public void ShouldHandle_WhenSenderIsNotInAllowList_ReturnsFalse()
    {
        var filter = CreateFilter(allowedSenderIds: ["someone-else"]);

        Assert.That(filter.ShouldHandle(Entry(), Event(SenderId, Message("m1"))), Is.False);
    }

    private static IncomingMessageFilter CreateFilter(string[]? allowedSenderIds = null) =>
        new(
            new ProcessedMessageTracker(new MemoryCache(new MemoryCacheOptions())),
            Microsoft.Extensions.Options.Options.Create(new InstagramOptions { AllowedSenderIds = allowedSenderIds ?? [] }),
            NullLogger<IncomingMessageFilter>.Instance);

    private static WebhookEntry Entry() => new(BotAccountId, []);

    private static MessagingEvent Event(string senderId, IncomingMessage? message) =>
        new(new Participant(senderId), new Participant(BotAccountId), message);

    private static IncomingMessage Message(string? mid, bool isEcho = false) =>
        new(mid, "鼎泰豐 信義店", isEcho, null);
}
