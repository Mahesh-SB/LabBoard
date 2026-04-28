namespace LabBoard.Redis.Api.Services;

public interface IRedisCacheService
{
    // String operations
    Task<string?> GetStringAsync(string key);
    Task SetStringAsync(string key, string value, TimeSpan? ttl = null);
    Task<bool> DeleteAsync(string key);
    Task<bool> ExistsAsync(string key);
    Task<long> IncrementAsync(string key, long by = 1);

    // Hash operations
    Task SetHashFieldAsync(string key, string field, string value);
    Task<string?> GetHashFieldAsync(string key, string field);
    Task<Dictionary<string, string>> GetAllHashFieldsAsync(string key);

    // List operations
    Task PushToListAsync(string key, string value);
    Task<string?> PopFromListAsync(string key);
    Task<IEnumerable<string>> GetListRangeAsync(string key, long start = 0, long stop = -1);

    // TTL
    Task<TimeSpan?> GetTtlAsync(string key);
}
