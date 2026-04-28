using Prometheus;
using StackExchange.Redis;
using System.Diagnostics;

namespace LabBoard.Redis.Api.Services;

/// <summary>
/// Wraps StackExchange.Redis operations and records Prometheus metrics for
/// cache hits/misses and per-operation latency.
/// </summary>
public class RedisCacheService : IRedisCacheService
{
    private readonly IDatabase _db;

    // ── Prometheus metrics ────────────────────────────────────────────────

    // How many times we found a key in Redis (GET returned a value)
    private static readonly Counter CacheHits = Metrics.CreateCounter(
        "labboard_redis_cache_hits_total",
        "Number of cache hits (key found in Redis)",
        new CounterConfiguration { LabelNames = ["operation"] });

    // How many times the key was missing
    private static readonly Counter CacheMisses = Metrics.CreateCounter(
        "labboard_redis_cache_misses_total",
        "Number of cache misses (key not found in Redis)",
        new CounterConfiguration { LabelNames = ["operation"] });

    // Latency histogram for every Redis call, labelled by operation type
    private static readonly Histogram OperationDuration = Metrics.CreateHistogram(
        "labboard_redis_op_duration_seconds",
        "Duration of Redis operations in seconds",
        new HistogramConfiguration
        {
            LabelNames = ["operation"],
            Buckets = Histogram.ExponentialBuckets(start: 0.0001, factor: 2, count: 10)
        });

    public RedisCacheService(IConnectionMultiplexer mux)
    {
        _db = mux.GetDatabase();
    }

    // ── String operations ─────────────────────────────────────────────────

    public async Task<string?> GetStringAsync(string key)
    {
        using var timer = OperationDuration.WithLabels("GET").NewTimer();
        var value = await _db.StringGetAsync(key);

        if (value.HasValue)
            CacheHits.WithLabels("GET").Inc();
        else
            CacheMisses.WithLabels("GET").Inc();

        return value;
    }

    public async Task SetStringAsync(string key, string value, TimeSpan? ttl = null)
    {
        using var timer = OperationDuration.WithLabels("SET").NewTimer();
        await _db.StringSetAsync(key, value, ttl);
    }

    public async Task<bool> DeleteAsync(string key)
    {
        using var timer = OperationDuration.WithLabels("DEL").NewTimer();
        return await _db.KeyDeleteAsync(key);
    }

    public async Task<bool> ExistsAsync(string key)
    {
        using var timer = OperationDuration.WithLabels("EXISTS").NewTimer();
        return await _db.KeyExistsAsync(key);
    }

    public async Task<long> IncrementAsync(string key, long by = 1)
    {
        using var timer = OperationDuration.WithLabels("INCR").NewTimer();
        return await _db.StringIncrementAsync(key, by);
    }

    // ── Hash operations ───────────────────────────────────────────────────

    public async Task SetHashFieldAsync(string key, string field, string value)
    {
        using var timer = OperationDuration.WithLabels("HSET").NewTimer();
        await _db.HashSetAsync(key, field, value);
    }

    public async Task<string?> GetHashFieldAsync(string key, string field)
    {
        using var timer = OperationDuration.WithLabels("HGET").NewTimer();
        var value = await _db.HashGetAsync(key, field);

        if (value.HasValue)
            CacheHits.WithLabels("HGET").Inc();
        else
            CacheMisses.WithLabels("HGET").Inc();

        return value;
    }

    public async Task<Dictionary<string, string>> GetAllHashFieldsAsync(string key)
    {
        using var timer = OperationDuration.WithLabels("HGETALL").NewTimer();
        var entries = await _db.HashGetAllAsync(key);
        return entries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());
    }

    // ── List operations ───────────────────────────────────────────────────

    public async Task PushToListAsync(string key, string value)
    {
        using var timer = OperationDuration.WithLabels("LPUSH").NewTimer();
        await _db.ListLeftPushAsync(key, value);
    }

    public async Task<string?> PopFromListAsync(string key)
    {
        using var timer = OperationDuration.WithLabels("RPOP").NewTimer();
        var value = await _db.ListRightPopAsync(key);
        return value;
    }

    public async Task<IEnumerable<string>> GetListRangeAsync(string key, long start = 0, long stop = -1)
    {
        using var timer = OperationDuration.WithLabels("LRANGE").NewTimer();
        var values = await _db.ListRangeAsync(key, start, stop);
        return values.Select(v => v.ToString());
    }

    // ── TTL ───────────────────────────────────────────────────────────────

    public async Task<TimeSpan?> GetTtlAsync(string key)
    {
        using var timer = OperationDuration.WithLabels("TTL").NewTimer();
        return await _db.KeyTimeToLiveAsync(key);
    }
}
