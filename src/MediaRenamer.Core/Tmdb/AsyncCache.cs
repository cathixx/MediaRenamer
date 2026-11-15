using System.Collections.Concurrent;

namespace MediaRenamer.Core.Tmdb;

/// <summary>
/// In-memory cache of asynchronously loaded values. Concurrent requests for the same key share one load.
/// A failed load is removed from the cache, so the next request loads again (T-17, T-20).
/// </summary>
internal sealed class AsyncCache<TKey, TValue>
    where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, Lazy<Task<TValue>>> _entries = new();

    public Task<TValue> GetAsync(TKey key, Func<TKey, Task<TValue>> load)
    {
        var entry = _entries.GetOrAdd(key, static (k, l) => new Lazy<Task<TValue>>(() => l(k)), load);
        return AwaitAsync(key, entry);
    }

    private async Task<TValue> AwaitAsync(TKey key, Lazy<Task<TValue>> entry)
    {
        try
        {
            return await entry.Value.ConfigureAwait(false);
        }
        catch
        {
            // remove only this entry, not one added by a later request
            _entries.TryRemove(new KeyValuePair<TKey, Lazy<Task<TValue>>>(key, entry));
            throw;
        }
    }
}
