namespace IgFoodBot.Api.Places;

public sealed record PlaceResult(
    string Name,
    string MapsUrl,
    string? PrimaryType,
    double? Rating,
    int? UserRatingCount,
    OpeningStatus? OpeningStatus,
    PriceRange? PriceRange);

public sealed record OpeningStatus(bool IsOpenNow, DateTimeOffset? NextChangeTime);

public sealed record PriceRange(string CurrencyCode, decimal StartPrice, decimal? EndPrice);
