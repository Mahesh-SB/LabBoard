using LabBoard.TicketMaster.Api.Configuration;
using LabBoard.TicketMaster.Api.Models;
using Microsoft.Extensions.Options;

namespace LabBoard.TicketMaster.Api.Services;

// Singleton — caches the service token until near-expiry so we don't hit Auth API on every request
public class ServiceTokenService(
    IHttpClientFactory httpClientFactory,
    IOptions<AuthApiOptions> options,
    ILogger<ServiceTokenService> logger) : IServiceTokenService
{
    private readonly AuthApiOptions _options = options.Value;
    private readonly SemaphoreSlim  _lock    = new(1, 1);

    private string?  _cachedToken;
    private DateTime _tokenExpiry;

    public async Task<string?> GetTokenAsync()
    {
        // Fast path — return cached token if still valid (with 30 s safety buffer)
        if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiry.AddSeconds(-30))
            return _cachedToken;

        await _lock.WaitAsync();
        try
        {
            // Double-check inside lock — another thread may have already refreshed
            if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiry.AddSeconds(-30))
                return _cachedToken;

            return await RefreshAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<string?> RefreshAsync()
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"]    = "client_credentials",
            ["client_id"]     = _options.ClientId,
            ["client_secret"] = _options.ClientSecret
        };

        try
        {
            var http     = httpClientFactory.CreateClient();
            var response = await http.PostAsync(
                $"{_options.BaseUrl}/oauth/token",
                new FormUrlEncodedContent(form));

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Auth API returned {Status} when fetching service token.", response.StatusCode);
                return null;
            }

            var token = await response.Content.ReadFromJsonAsync<AuthTokenResponse>();
            if (token is null) return null;

            _cachedToken = token.AccessToken;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(token.ExpiresIn);

            logger.LogInformation("Service token refreshed. Expires in {ExpiresIn}s.", token.ExpiresIn);
            return _cachedToken;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch service token from Auth API at {BaseUrl}.", _options.BaseUrl);
            return null;
        }
    }
}
