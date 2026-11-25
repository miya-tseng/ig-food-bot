using System.Net;
using System.Text.Json.Serialization;
using System.Text.Json;
using IgFoodBot.Api.Models;
using IgFoodBot.Api.Options;
using Microsoft.Extensions.Options;

namespace IgFoodBot.Api.Clients;

public sealed class GeminiClient(HttpClient httpClient, IOptions<GeminiOptions> options, ILogger<GeminiClient> logger)
{
    private const string Instruction = """
        你是美食貼文分析助手。輸入是使用者從 Instagram 分享的美食貼文內容（文字、hashtag、可能附圖片）。
        請判斷貼文介紹的餐廳，並以繁體中文輸出：
        - isRestaurantPost：是否能辨識出特定一家餐廳
        - restaurantName：貼文中的餐廳名稱（含分店名，若有），只寫一種語言，不要加括號註解
        - area：城市或區域，例如「台北市大安區」、「首爾鐘路區」
        - countryCode：餐廳所在國家的 ISO 3166-1 alpha-2 代碼，例如 TW、KR、JP
        - searchQueries：3 組用來在 Google Maps 找到這家店的搜尋字串，由最可能找到的排到最不可能。每組都必須包含店名，可搭配附近地標、行政區、招牌料理或料理類型，例如 ["PARU 景福宮 一隻雞", "파루 닭한마리", "PARU 鐘路區 韓式餐廳"]。若店在非中文地區，其中一組請用當地語言的店名。不要只放地址或路名
        - summary：1～2 句的重點摘要（特色、氛圍或注意事項），不超過 60 字
        - recommendedDishes：貼文提到最推薦的餐點，最多 3 個
        不確定的欄位請留空，不要編造。
        """;

    private const int MaxRetries = 2;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly object ResponseSchema = new
    {
        type = "OBJECT",
        properties = new
        {
            isRestaurantPost = new { type = "BOOLEAN" },
            restaurantName = new { type = "STRING" },
            area = new { type = "STRING" },
            countryCode = new { type = "STRING" },
            searchQueries = new { type = "ARRAY", items = new { type = "STRING" } },
            summary = new { type = "STRING" },
            recommendedDishes = new { type = "ARRAY", items = new { type = "STRING" } }
        },
        required = new[] { "isRestaurantPost", "restaurantName", "area", "countryCode", "searchQueries", "summary" }
    };

    public async Task<RestaurantInsight?> AnalyzeAsync(string postText, IReadOnlyList<ImageInput> images, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var requestBody = BuildRequest(postText, images, settings);

        foreach (var model in settings.FallbackModels.Prepend(settings.Model).Distinct())
        {
            var attempt = await TryAnalyzeWithModelAsync(model, requestBody, settings.ApiKey, cancellationToken);
            if (attempt.Insight is not null || !attempt.CanTryNextModel)
            {
                return attempt.Insight;
            }
        }

        return null;
    }

    private async Task<ModelAttempt> TryAnalyzeWithModelAsync(string model, GenerateContentRequest requestBody, string apiKey, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await SendWithRetryAsync(model, requestBody, apiKey, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("Gemini request failed with {Model}: {StatusCode} {Body}", model, (int)response.StatusCode, errorBody);
                return ModelAttempt.Failed(CanFallBackFrom(response.StatusCode));
            }

            var result = await response.Content.ReadFromJsonAsync<GenerateContentResponse>(JsonOptions, cancellationToken);
            var generatedJson = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            if (string.IsNullOrWhiteSpace(generatedJson))
            {
                logger.LogWarning("Gemini returned no content with {Model}", model);
                return ModelAttempt.Failed(canTryNextModel: true);
            }

            return new ModelAttempt(JsonSerializer.Deserialize<RestaurantInsight>(generatedJson, JsonOptions), CanTryNextModel: true);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException
                                          && !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Gemini request failed with {Model}", model);
            return ModelAttempt.Failed(canTryNextModel: true);
        }
    }

    private static bool CanFallBackFrom(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.NotFound or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
        || (int)statusCode >= 500;

    private async Task<HttpResponseMessage> SendWithRetryAsync(string model, GenerateContentRequest requestBody, string apiKey, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent")
            {
                Content = JsonContent.Create(requestBody, options: JsonOptions)
            };
            request.Headers.Add("x-goog-api-key", apiKey);

            var response = await httpClient.SendAsync(request, cancellationToken);
            if (!IsTransientFailure(response.StatusCode) || attempt > MaxRetries)
            {
                return response;
            }

            logger.LogWarning("Gemini {Model} returned {StatusCode}, retrying (attempt {Attempt})", model, (int)response.StatusCode, attempt);
            response.Dispose();
            await Task.Delay(RetryDelay * attempt, cancellationToken);
        }
    }

    private static bool IsTransientFailure(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable;

    private static GenerateContentRequest BuildRequest(string postText, IReadOnlyList<ImageInput> images, GeminiOptions settings)
    {
        var textPart = new Part(Text: $"{Instruction}\n\n貼文內容：\n{postText}");
        var imageParts = images.Select(image =>
            new Part(InlineData: new InlineData(image.MimeType, Convert.ToBase64String(image.Data))));

        var thinkingConfig = string.IsNullOrWhiteSpace(settings.ThinkingLevel) ? null : new ThinkingConfig(settings.ThinkingLevel);

        return new GenerateContentRequest(
            [new Content("user", [textPart, .. imageParts])],
            new GenerationConfig("application/json", ResponseSchema, thinkingConfig));
    }

    private sealed record ModelAttempt(RestaurantInsight? Insight, bool CanTryNextModel)
    {
        public static ModelAttempt Failed(bool canTryNextModel) => new(null, canTryNextModel);
    }

    private sealed record GenerateContentRequest(
        [property: JsonPropertyName("contents")] IReadOnlyList<Content> Contents,
        [property: JsonPropertyName("generationConfig")] GenerationConfig GenerationConfig);

    private sealed record GenerationConfig(
        [property: JsonPropertyName("responseMimeType")] string ResponseMimeType,
        [property: JsonPropertyName("responseSchema")] object ResponseSchema,
        [property: JsonPropertyName("thinkingConfig")] ThinkingConfig? ThinkingConfig);

    private sealed record ThinkingConfig(
        [property: JsonPropertyName("thinkingLevel")] string ThinkingLevel);

    private sealed record Content(
        [property: JsonPropertyName("role")] string? Role,
        [property: JsonPropertyName("parts")] IReadOnlyList<Part>? Parts);

    private sealed record Part(
        [property: JsonPropertyName("text")] string? Text = null,
        [property: JsonPropertyName("inlineData")] InlineData? InlineData = null);

    private sealed record InlineData(
        [property: JsonPropertyName("mimeType")] string MimeType,
        [property: JsonPropertyName("data")] string Data);

    private sealed record GenerateContentResponse(
        [property: JsonPropertyName("candidates")] IReadOnlyList<Candidate>? Candidates);

    private sealed record Candidate(
        [property: JsonPropertyName("content")] Content? Content);
}
