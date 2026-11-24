using Microsoft.Extensions.Caching.Memory;

namespace IgFoodBot.Api.Processing;

public sealed class ProcessedMessageTracker(IMemoryCache cache)
{
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromHours(1);
    private readonly Lock _lock = new();

    public bool TryMarkAsProcessed(string messageId)
    {
        lock (_lock)
        {
            if (cache.TryGetValue(messageId, out _))
            {
                return false;
            }

            cache.Set(messageId, true, RetentionPeriod);
            return true;
        }
    }
}
