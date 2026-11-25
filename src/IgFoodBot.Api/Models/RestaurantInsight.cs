using System.Text.Json.Serialization;

namespace IgFoodBot.Api.Models;

public sealed record RestaurantInsight(
    [property: JsonPropertyName("isRestaurantPost")] bool IsRestaurantPost,
    [property: JsonPropertyName("restaurantName")] string? RestaurantName,
    [property: JsonPropertyName("area")] string? Area,
    [property: JsonPropertyName("countryCode")] string? CountryCode,
    [property: JsonPropertyName("searchQueries")] List<string>? SearchQueries,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("recommendedDishes")] List<string>? RecommendedDishes);
