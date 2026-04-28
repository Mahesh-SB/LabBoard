using Microsoft.AspNetCore.Mvc;
using LabBoard.Redis.Api.Services;

namespace LabBoard.Redis.Api.Controllers;

/// <summary>
/// Practice all core Redis data-type operations directly via REST.
/// Covers: Strings, Hashes, Lists, Counters, TTL inspection.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CacheController : ControllerBase
{
    private readonly IRedisCacheService _cache;

    public CacheController(IRedisCacheService cache) => _cache = cache;

    // ── String ────────────────────────────────────────────────────────────

    /// <summary>GET a string value by key.</summary>
    [HttpGet("string/{key}")]
    public async Task<IActionResult> GetString(string key)
    {
        var value = await _cache.GetStringAsync(key);
        return value is null ? NotFound(new { key, message = "Key not found" })
                             : Ok(new { key, value });
    }

    /// <summary>SET a string value. Optional ttlSeconds query param.</summary>
    [HttpPost("string/{key}")]
    public async Task<IActionResult> SetString(string key, [FromBody] string value,
        [FromQuery] int? ttlSeconds = null)
    {
        var ttl = ttlSeconds.HasValue ? TimeSpan.FromSeconds(ttlSeconds.Value) : (TimeSpan?)null;
        await _cache.SetStringAsync(key, value, ttl);
        return Ok(new { key, value, ttlSeconds });
    }

    /// <summary>DELETE a key.</summary>
    [HttpDelete("{key}")]
    public async Task<IActionResult> Delete(string key)
    {
        var deleted = await _cache.DeleteAsync(key);
        return deleted ? Ok(new { key, deleted = true })
                       : NotFound(new { key, deleted = false });
    }

    /// <summary>Check if a key EXISTS.</summary>
    [HttpGet("{key}/exists")]
    public async Task<IActionResult> Exists(string key)
    {
        var exists = await _cache.ExistsAsync(key);
        return Ok(new { key, exists });
    }

    /// <summary>INCR a counter. Creates the key at 0 if it does not exist.</summary>
    [HttpPost("counter/{key}/incr")]
    public async Task<IActionResult> Increment(string key, [FromQuery] long by = 1)
    {
        var newValue = await _cache.IncrementAsync(key, by);
        return Ok(new { key, value = newValue });
    }

    /// <summary>Get remaining TTL for a key.</summary>
    [HttpGet("{key}/ttl")]
    public async Task<IActionResult> GetTtl(string key)
    {
        var ttl = await _cache.GetTtlAsync(key);
        if (ttl is null)
            return NotFound(new { key, message = "Key not found or has no TTL" });
        return Ok(new { key, ttlSeconds = ttl.Value.TotalSeconds });
    }

    // ── Hash ──────────────────────────────────────────────────────────────

    /// <summary>HSET: set a single field in a hash.</summary>
    [HttpPost("hash/{key}/{field}")]
    public async Task<IActionResult> SetHashField(string key, string field, [FromBody] string value)
    {
        await _cache.SetHashFieldAsync(key, field, value);
        return Ok(new { key, field, value });
    }

    /// <summary>HGET: get a single hash field.</summary>
    [HttpGet("hash/{key}/{field}")]
    public async Task<IActionResult> GetHashField(string key, string field)
    {
        var value = await _cache.GetHashFieldAsync(key, field);
        return value is null ? NotFound(new { key, field })
                             : Ok(new { key, field, value });
    }

    /// <summary>HGETALL: get all fields of a hash.</summary>
    [HttpGet("hash/{key}")]
    public async Task<IActionResult> GetAllHashFields(string key)
    {
        var fields = await _cache.GetAllHashFieldsAsync(key);
        return Ok(new { key, fields });
    }

    // ── List ──────────────────────────────────────────────────────────────

    /// <summary>LPUSH: push a value to the head of a list.</summary>
    [HttpPost("list/{key}")]
    public async Task<IActionResult> PushToList(string key, [FromBody] string value)
    {
        await _cache.PushToListAsync(key, value);
        return Ok(new { key, pushed = value });
    }

    /// <summary>RPOP: pop a value from the tail of a list.</summary>
    [HttpDelete("list/{key}/pop")]
    public async Task<IActionResult> PopFromList(string key)
    {
        var value = await _cache.PopFromListAsync(key);
        return value is null ? NotFound(new { key, message = "List empty or not found" })
                             : Ok(new { key, popped = value });
    }

    /// <summary>LRANGE: get all items in a list.</summary>
    [HttpGet("list/{key}")]
    public async Task<IActionResult> GetList(string key)
    {
        var items = await _cache.GetListRangeAsync(key);
        return Ok(new { key, items });
    }
}
