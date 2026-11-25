using System.Globalization;
using System.Text.Json.Serialization;
using IgFoodBot.Api.Models;
using IgFoodBot.Api.Options;
using Microsoft.Extensions.Options;

namespace IgFoodBot.Api.Clients;

public sealed class PlacesClient(HttpClient httpClient, IOptions<GooglePlacesOptions> options, ILogger<PlacesClient> logger)
{
    private const string FieldMask = "places.displayName,places.googleMapsUri,places.rating,places.userRatingCount,places.types,places.primaryType,places.currentOpeningHours,places.utcOffsetMinutes,places.priceRange";
    private const int CandidateCount = 5;

    private static readonly HashSet<string> FoodPlaceTypes =
    [
        "restaurant", "food", "cafe", "coffee_shop", "bakery", "bar", "pub", "meal_takeaway", "meal_delivery",
        "dessert_shop", "ice_cream_shop", "tea_house", "food_court", "bagel_shop", "donut_shop", "juice_shop"
    ];

    public async Task<PlaceResult?> FindRestaurantAsync(string query, string? regionCode, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://places.googleapis.com/v1/places:searchText")
        {
            Content = JsonContent.Create(new
            {
                textQuery = query,
                languageCode = settings.LanguageCode,
                regionCode = string.IsNullOrWhiteSpace(regionCode) ? settings.RegionCode : regionCode,
                pageSize = CandidateCount
            })
        };
        request.Headers.Add("X-Goog-Api-Key", settings.ApiKey);
        request.Headers.Add("X-Goog-FieldMask", FieldMask);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Places search failed: {StatusCode} {Body}", (int)response.StatusCode, body);
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<SearchTextResponse>(cancellationToken);
        var place = result?.Places?.FirstOrDefault(candidate => candidate.GoogleMapsUri is not null && IsFoodPlace(candidate));
        if (place is null)
        {
            logger.LogInformation("No food place found for query {Query}", query);
            return null;
        }

        return new PlaceResult(
            place.DisplayName?.Text ?? query,
            place.GoogleMapsUri!,
            place.PrimaryType,
            place.Rating,
            place.UserRatingCount,
            ToOpeningStatus(place),
            ToPriceRange(place.PriceRange));
    }

    public static string BuildSearchUrl(string query) =>
        $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(query)}";

    private static bool IsFoodPlace(PlaceDto place) =>
        place.Types?.Any(type => FoodPlaceTypes.Contains(type) || type.EndsWith("_restaurant", StringComparison.Ordinal)) ?? false;

    private static OpeningStatus? ToOpeningStatus(PlaceDto place)
    {
        if (place.CurrentOpeningHours?.OpenNow is not { } isOpenNow)
        {
            return null;
        }

        var nextChange = isOpenNow ? place.CurrentOpeningHours.NextCloseTime : place.CurrentOpeningHours.NextOpenTime;
        var offset = TimeSpan.FromMinutes(place.UtcOffsetMinutes ?? 0);
        return new OpeningStatus(isOpenNow, nextChange?.ToOffset(offset));
    }

    private static PriceRange? ToPriceRange(PriceRangeDto? priceRange)
    {
        var startPrice = ParseUnits(priceRange?.StartPrice?.Units);
        var currencyCode = priceRange?.StartPrice?.CurrencyCode;
        if (startPrice is null || currencyCode is null)
        {
            return null;
        }

        return new PriceRange(currencyCode, startPrice.Value, ParseUnits(priceRange?.EndPrice?.Units));
    }

    private static decimal? ParseUnits(string? units) =>
        decimal.TryParse(units, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;

    private sealed record SearchTextResponse(
        [property: JsonPropertyName("places")] List<PlaceDto>? Places);

    private sealed record PlaceDto(
        [property: JsonPropertyName("displayName")] LocalizedText? DisplayName,
        [property: JsonPropertyName("googleMapsUri")] string? GoogleMapsUri,
        [property: JsonPropertyName("rating")] double? Rating,
        [property: JsonPropertyName("userRatingCount")] int? UserRatingCount,
        [property: JsonPropertyName("types")] List<string>? Types,
        [property: JsonPropertyName("primaryType")] string? PrimaryType,
        [property: JsonPropertyName("currentOpeningHours")] OpeningHoursDto? CurrentOpeningHours,
        [property: JsonPropertyName("utcOffsetMinutes")] int? UtcOffsetMinutes,
        [property: JsonPropertyName("priceRange")] PriceRangeDto? PriceRange);

    private sealed record LocalizedText(
        [property: JsonPropertyName("text")] string? Text);

    private sealed record OpeningHoursDto(
        [property: JsonPropertyName("openNow")] bool? OpenNow,
        [property: JsonPropertyName("nextOpenTime")] DateTimeOffset? NextOpenTime,
        [property: JsonPropertyName("nextCloseTime")] DateTimeOffset? NextCloseTime);

    private sealed record PriceRangeDto(
        [property: JsonPropertyName("startPrice")] MoneyDto? StartPrice,
        [property: JsonPropertyName("endPrice")] MoneyDto? EndPrice);

    private sealed record MoneyDto(
        [property: JsonPropertyName("currencyCode")] string? CurrencyCode,
        [property: JsonPropertyName("units")] string? Units);
}
