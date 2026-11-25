using IgFoodBot.Api.Instagram;
using IgFoodBot.Api.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace IgFoodBot.Api.Health;

public sealed class InstagramHealthCheck(
    InstagramMessenger messenger,
    IOptions<InstagramOptions> options,
    IMemoryCache cache,
    ILogger<InstagramHealthCheck> logger)
{
    private const string HealthyCacheKey = "health-instagram-healthy";
    private static readonly TimeSpan HealthyCacheDuration = TimeSpan.FromMinutes(5);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.AccountId);

    public async Task<bool> IsHealthyAsync()
    {
        if (cache.TryGetValue(HealthyCacheKey, out _))
        {
            return true;
        }

        var error = await messenger.CheckAccountAccessAsync(options.Value.AccountId!, CancellationToken.None);
        if (error is not null)
        {
            logger.LogWarning("Instagram health check failed: {Error}", error);
            return false;
        }

        cache.Set(HealthyCacheKey, true, HealthyCacheDuration);
        return true;
    }
}
