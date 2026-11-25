using System.Net;
using IgFoodBot.Api.Clients;
using IgFoodBot.Api.Options;
using IgFoodBot.Api.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace IgFoodBot.UnitTests.Services;

public class InstagramHealthCheckTests
{
    [Test]
    public async Task IsHealthyAsync_WhenApiResponds_ReturnsTrueAndCachesResult()
    {
        var handler = new CountingHandler(HttpStatusCode.OK);
        var healthCheck = CreateHealthCheck(handler);

        var first = await healthCheck.IsHealthyAsync();
        var second = await healthCheck.IsHealthyAsync();

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.True);
            Assert.That(second, Is.True);
            Assert.That(handler.RequestCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task IsHealthyAsync_WhenApiIsBlocked_ReturnsFalseWithoutCaching()
    {
        var handler = new CountingHandler(HttpStatusCode.BadRequest);
        var healthCheck = CreateHealthCheck(handler);

        var first = await healthCheck.IsHealthyAsync();
        var second = await healthCheck.IsHealthyAsync();

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.False);
            Assert.That(second, Is.False);
            Assert.That(handler.RequestCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task IsHealthyAsync_AfterTransientFailure_RecoversOnNextCheck()
    {
        var handler = new CountingHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);
        var healthCheck = CreateHealthCheck(handler);

        var first = await healthCheck.IsHealthyAsync();
        var second = await healthCheck.IsHealthyAsync();

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.False);
            Assert.That(second, Is.True);
        });
    }

    [Test]
    public void IsConfigured_WithoutAccountId_ReturnsFalse()
    {
        var healthCheck = CreateHealthCheck(new CountingHandler(HttpStatusCode.OK), accountId: null);

        Assert.That(healthCheck.IsConfigured, Is.False);
    }

    private static InstagramHealthCheck CreateHealthCheck(CountingHandler handler, string? accountId = "17841400000000000")
    {
        var options = Microsoft.Extensions.Options.Options.Create(new InstagramOptions
        {
            AccessToken = "test-token",
            AccountId = accountId
        });
        var messenger = new InstagramMessenger(new HttpClient(handler), options, NullLogger<InstagramMessenger>.Instance);

        return new InstagramHealthCheck(
            messenger,
            options,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<InstagramHealthCheck>.Instance);
    }

    private sealed class CountingHandler(params HttpStatusCode[] statusCodes) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var statusCode = statusCodes[Math.Min(RequestCount, statusCodes.Length - 1)];
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(statusCode) { Content = new StringContent("{}") });
        }
    }
}
