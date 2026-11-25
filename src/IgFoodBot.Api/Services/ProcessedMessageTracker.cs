using Microsoft.Extensions.Caching.Memory;

namespace IgFoodBot.Api.Services;

public sealed class ProcessedMessageTracker(IMemoryCache cache)
{
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromHours(1);

    public bool TryMarkAsProcessed(string messageId)
    {
        if (cache.TryGetValue(messageId, out _))
        {
            return false;
        }

        cache.Set(messageId, true, RetentionPeriod);
        return true;
    }
}
