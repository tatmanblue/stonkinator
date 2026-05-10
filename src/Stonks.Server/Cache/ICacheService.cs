namespace Stonks.Server.Cache;

public interface ICacheService
{
    bool TryGet<T>(string key, out T? value);
    void Set<T>(string key, T value, TimeSpan? ttl = null);
    void Invalidate(string key);
}
