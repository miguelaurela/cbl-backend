using CBL.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace CBL.Infrastructure.Caching;

/// <summary>
/// Caché de lectura sobre <see cref="IMemoryCache"/> con límite de tamaño y expiración ABSOLUTA de 5 minutos
/// (la expiración deslizante podría servir datos obsoletos indefinidamente).
/// </summary>
public sealed class MemoryReadCache : IReadCache, IDisposable
{
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 10_000 });

    public bool TryGet<T>(string key, out T? value)
    {
        if (_cache.TryGetValue(key, out var cached) && cached is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    public void Set<T>(string key, T value) =>
        _cache.Set(key, value, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl, Size = 1 });

    public void Remove(string key) => _cache.Remove(key);

    public void Dispose() => _cache.Dispose();
}
