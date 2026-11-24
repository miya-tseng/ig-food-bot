using System.Text.Json.Serialization;
using IgFoodBot.Api.Places;
using Microsoft.Extensions.Caching.Memory;

namespace IgFoodBot.Api.Currency;

public sealed class ExchangeRateClient(HttpClient httpClient, IMemoryCache cache, ILogger<ExchangeRateClient> logger)
{
    public const string TargetCurrency = "TWD";

    private const string CacheKey = "exchange-rates-twd";
    private const decimal RoundingUnit = 10m;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(12);

    public async Task<PriceRange?> ConvertToTwdAsync(PriceRange? priceRange, CancellationToken cancellationToken)
    {
        if (priceRange is null || priceRange.CurrencyCode == TargetCurrency)
        {
            return priceRange;
        }

        var rates = await GetRatesAsync(cancellationToken);
        if (rates is null || !rates.TryGetValue(priceRange.CurrencyCode, out var unitsPerTwd) || unitsPerTwd <= 0)
        {
            return null;
        }

        return new PriceRange(
            TargetCurrency,
            ToTwd(priceRange.StartPrice, unitsPerTwd),
            priceRange.EndPrice is { } endPrice ? ToTwd(endPrice, unitsPerTwd) : null);
    }

    private static decimal ToTwd(decimal amount, decimal unitsPerTwd) =>
        Math.Max(RoundingUnit, Math.Round(amount / unitsPerTwd / RoundingUnit) * RoundingUnit);

    private async Task<Dictionary<string, decimal>?> GetRatesAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out Dictionary<string, decimal>? cachedRates))
        {
            return cachedRates;
        }

        try
        {
            var response = await httpClient.GetFromJsonAsync<LatestRatesResponse>(
                $"https://open.er-api.com/v6/latest/{TargetCurrency}", cancellationToken);
            if (response is not { Result: "success", Rates: not null })
            {
                logger.LogWarning("Exchange rate API returned no rates");
                return null;
            }

            cache.Set(CacheKey, response.Rates, CacheDuration);
            return response.Rates;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogWarning(exception, "Failed to fetch exchange rates");
            return null;
        }
    }

    private sealed record LatestRatesResponse(
        [property: JsonPropertyName("result")] string? Result,
        [property: JsonPropertyName("rates")] Dictionary<string, decimal>? Rates);
}
