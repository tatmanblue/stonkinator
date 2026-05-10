using System.Text.Json;
using System.Text.RegularExpressions;

namespace Stonks.Server.Cache;

public class FileCacheService : ICacheService
{
    private readonly string baseDir;

    public FileCacheService()
    {
        baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Stonks", "cache");
        Directory.CreateDirectory(baseDir);
    }

    public bool TryGet<T>(string key, out T? value)
    {
        var path = FilePath(key);
        value = default;

        if (!File.Exists(path))
            return false;

        try
        {
            var json = File.ReadAllText(path);
            var entry = JsonSerializer.Deserialize<CacheEntry<T>>(json);
            if (entry is null)
                return false;

            if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value < DateTimeOffset.UtcNow)
            {
                File.Delete(path);
                return false;
            }

            value = entry.Value;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Set<T>(string key, T value, TimeSpan? ttl = null)
    {
        var entry = new CacheEntry<T>
        {
            Value = value,
            ExpiresAt = ttl.HasValue ? DateTimeOffset.UtcNow.Add(ttl.Value) : null
        };
        var json = JsonSerializer.Serialize(entry);
        File.WriteAllText(FilePath(key), json);
    }

    public void Invalidate(string key)
    {
        var path = FilePath(key);
        if (File.Exists(path))
            File.Delete(path);
    }

    private string FilePath(string key)
    {
        var safe = Regex.Replace(key, @"[^a-zA-Z0-9]", "_");
        return Path.Combine(baseDir, safe + ".json");
    }

    private sealed class CacheEntry<T>
    {
        public T? Value { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
    }
}
