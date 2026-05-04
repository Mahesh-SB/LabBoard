using LabBoard.UserManagement.Api.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LabBoard.UserManagement.Api.Infrastructure;

public class JwksFetcher(
    IHttpClientFactory httpClientFactory,
    IOptions<JwtOptions> options,
    ILogger<JwksFetcher> logger)
{
    private readonly string _jwksUri = options.Value.JwksUri;
    private readonly TimeSpan _ttl   = TimeSpan.FromHours(1);
    private readonly object _lock    = new();

    private List<SecurityKey>? _cached;
    private DateTime _fetchedAt;

    public IEnumerable<SecurityKey> GetKeys()
    {
        // Fast path — return cache while still fresh
        if (_cached is not null && DateTime.UtcNow - _fetchedAt < _ttl)
            return _cached;

        lock (_lock)
        {
            // Double-check after acquiring lock
            if (_cached is not null && DateTime.UtcNow - _fetchedAt < _ttl)
                return _cached;

            try
            {
                var http = httpClientFactory.CreateClient();
                var json = http.GetStringAsync(_jwksUri).GetAwaiter().GetResult();
                var jwks = new JsonWebKeySet(json);

                _cached    = [.. jwks.GetSigningKeys()];
                _fetchedAt = DateTime.UtcNow;

                logger.LogInformation("JWKS keys refreshed from {Uri}", _jwksUri);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fetch JWKS from {Uri}. Ensure Auth API is running.", _jwksUri);
            }

            return _cached ?? [];
        }
    }
}
