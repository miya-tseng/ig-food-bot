using System.Net;
using System.Text;
using System.Text.Json;
using IgFoodBot.Api.Gemini;
using IgFoodBot.Api.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace IgFoodBot.UnitTests.Gemini;

public class GeminiClientTests
{
    private const string PrimaryModel = "primary-model";
    private const string FallbackModel = "fallback-model";

    [Test]
    public async Task AnalyzeAsync_WhenPrimaryModelSucceeds_DoesNotCallFallback()
    {
        var handler = new StubHandler(_ => SuccessResponse("PARU"));
        var client = CreateClient(handler);

        var insight = await client.AnalyzeAsync("貼文", [], CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(insight?.RestaurantName, Is.EqualTo("PARU"));
            Assert.That(handler.RequestedModels, Is.EqualTo(new[] { PrimaryModel }));
        });
    }

    [Test]
    public async Task AnalyzeAsync_WhenPrimaryModelFails_UsesFallbackModel()
    {
        var handler = new StubHandler(model => model == PrimaryModel
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : SuccessResponse("PARU"));
        var client = CreateClient(handler);

        var insight = await client.AnalyzeAsync("貼文", [], CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(insight?.RestaurantName, Is.EqualTo("PARU"));
            Assert.That(handler.RequestedModels, Is.EqualTo(new[] { PrimaryModel, FallbackModel }));
        });
    }

    [Test]
    public async Task AnalyzeAsync_WhenAllModelsFail_ReturnsNull()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = CreateClient(handler);

        var insight = await client.AnalyzeAsync("貼文", [], CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(insight, Is.Null);
            Assert.That(handler.RequestedModels, Is.EqualTo(new[] { PrimaryModel, FallbackModel }));
        });
    }

    [Test]
    public async Task AnalyzeAsync_WhenPrimaryModelTimesOut_UsesFallbackModel()
    {
        var handler = new StubHandler(model => model == PrimaryModel
            ? throw new TaskCanceledException("timeout")
            : SuccessResponse("PARU"));
        var client = CreateClient(handler);

        var insight = await client.AnalyzeAsync("貼文", [], CancellationToken.None);

        Assert.That(insight?.RestaurantName, Is.EqualTo("PARU"));
    }

    [Test]
    public async Task AnalyzeAsync_WhenPrimaryModelReturnsInvalidJson_UsesFallbackModel()
    {
        var handler = new StubHandler(model => model == PrimaryModel
            ? GeneratedTextResponse("{ not json")
            : SuccessResponse("PARU"));
        var client = CreateClient(handler);

        var insight = await client.AnalyzeAsync("貼文", [], CancellationToken.None);

        Assert.That(insight?.RestaurantName, Is.EqualTo("PARU"));
    }

    [TestCase(HttpStatusCode.BadRequest)]
    [TestCase(HttpStatusCode.PaymentRequired)]
    [TestCase(HttpStatusCode.Forbidden)]
    public async Task AnalyzeAsync_WhenPrimaryModelFailsPermanently_DoesNotCallFallback(HttpStatusCode statusCode)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(statusCode));
        var client = CreateClient(handler);

        var insight = await client.AnalyzeAsync("貼文", [], CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(insight, Is.Null);
            Assert.That(handler.RequestedModels, Is.EqualTo(new[] { PrimaryModel }));
        });
    }

    [Test]
    public void FallbackModels_ByDefault_IsEmpty()
    {
        Assert.That(new GeminiOptions().FallbackModels, Is.Empty);
    }

    private static GeminiClient CreateClient(StubHandler handler) =>
        new(
            new HttpClient(handler),
            Microsoft.Extensions.Options.Options.Create(new GeminiOptions
            {
                ApiKey = "test-key",
                Model = PrimaryModel,
                FallbackModels = [FallbackModel]
            }),
            NullLogger<GeminiClient>.Instance);

    private static HttpResponseMessage SuccessResponse(string restaurantName) =>
        GeneratedTextResponse(JsonSerializer.Serialize(new { isRestaurantPost = true, restaurantName }));

    private static HttpResponseMessage GeneratedTextResponse(string insightJson)
    {
        var body = JsonSerializer.Serialize(new
        {
            candidates = new[] { new { content = new { parts = new[] { new { text = insightJson } } } } }
        });
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    private sealed class StubHandler(Func<string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> RequestedModels { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var model = request.RequestUri!.Segments[^1].Split(':')[0];
            RequestedModels.Add(model);
            return Task.FromResult(respond(model));
        }
    }
}
