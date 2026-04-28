using Microsoft.AspNetCore.Mvc;
using LabBoard.Redis.Api.Models;
using LabBoard.Redis.Api.Services;
using System.Text.Json;

namespace LabBoard.Redis.Api.Controllers;

/// <summary>
/// Real-world caching pattern:
///   GET  → try cache first, fall back to "database" (in-memory seed data)
///   PUT  → update "database" and invalidate cache (cache-aside pattern)
///   DELETE → remove from cache only
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProductController : ControllerBase
{
    private readonly IRedisCacheService _cache;
    private const string CacheKeyPrefix = "product:";
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(2);

    // Simulates a database — in real code this would be EF Core / Dapper etc.
    private static readonly Dictionary<int, Product> FakeDb = new()
    {
        [1] = new Product(1, "Keyboard",  79.99m,  150),
        [2] = new Product(2, "Mouse",     39.99m,  300),
        [3] = new Product(3, "Monitor",  349.99m,   45),
        [4] = new Product(4, "Headset",   89.99m,  200),
        [5] = new Product(5, "Webcam",    59.99m,  175),
    };

    public ProductController(IRedisCacheService cache) => _cache = cache;

    /// <summary>
    /// GET /api/product/{id}
    /// Cache-aside read: hit cache → return. Miss → query DB → populate cache → return.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetProduct(int id)
    {
        var cacheKey = $"{CacheKeyPrefix}{id}";

        // 1. Try cache
        var cached = await _cache.GetStringAsync(cacheKey);
        if (cached is not null)
        {
            var cachedProduct = JsonSerializer.Deserialize<Product>(cached);
            return Ok(new { source = "cache", product = cachedProduct });
        }

        // 2. Miss → go to "database"
        if (!FakeDb.TryGetValue(id, out var product))
            return NotFound(new { id, message = "Product not found" });

        // 3. Populate cache
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(product), DefaultTtl);

        return Ok(new { source = "database", product });
    }

    /// <summary>
    /// GET /api/product
    /// Returns all products (each individually cached).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var results = new List<object>();

        foreach (var (id, product) in FakeDb)
        {
            var cacheKey = $"{CacheKeyPrefix}{id}";
            var cached   = await _cache.GetStringAsync(cacheKey);
            results.Add(new
            {
                source  = cached is not null ? "cache" : "database",
                product = cached is not null
                    ? JsonSerializer.Deserialize<Product>(cached)
                    : product
            });
        }

        return Ok(results);
    }

    /// <summary>
    /// PUT /api/product/{id}
    /// Updates the product in the "DB" and invalidates the cache entry.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateProduct(int id, [FromBody] Product updated)
    {
        if (!FakeDb.ContainsKey(id))
            return NotFound(new { id });

        FakeDb[id] = updated with { Id = id };

        // Invalidate cache so next GET re-fetches from DB
        var deleted = await _cache.DeleteAsync($"{CacheKeyPrefix}{id}");

        return Ok(new { updated = FakeDb[id], cacheInvalidated = deleted });
    }

    /// <summary>
    /// DELETE /api/product/{id}/cache
    /// Evict a single product from cache without touching the "DB".
    /// </summary>
    [HttpDelete("{id:int}/cache")]
    public async Task<IActionResult> EvictCache(int id)
    {
        var deleted = await _cache.DeleteAsync($"{CacheKeyPrefix}{id}");
        return Ok(new { id, cacheEvicted = deleted });
    }

    /// <summary>
    /// POST /api/product/{id}/views
    /// Increment a view counter — demonstrates INCR without TTL.
    /// </summary>
    [HttpPost("{id:int}/views")]
    public async Task<IActionResult> IncrementViews(int id)
    {
        var count = await _cache.IncrementAsync($"product:views:{id}");
        return Ok(new { productId = id, totalViews = count });
    }
}
