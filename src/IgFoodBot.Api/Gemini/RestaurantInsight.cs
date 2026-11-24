namespace IgFoodBot.Api.Gemini;

public sealed record RestaurantInsight(
    bool IsRestaurantPost,
    string? RestaurantName,
    string? Area,
    string? CountryCode,
    List<string>? SearchQueries,
    string? Summary,
    List<string>? RecommendedDishes);
