using System.Security.Cryptography;
using System.Text.Json;
using LabBoard.Auth.Api.Entities;
using LabBoard.Auth.Api.Enums;
using LabBoard.Auth.Api.Models.Client;
using LabBoard.Auth.Api.Models.OAuth;
using LabBoard.Auth.Api.Services.Client;

namespace LabBoard.Auth.Api.Services.OAuth;

public class AuthCodeService(IClientAppService clientAppService, IWebHostEnvironment env) : IAuthCodeService
{
    private readonly string _storePath = Path.Combine(env.ContentRootPath, "Database", "authCodeStore.json");
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private static readonly Dictionary<string, OpenIdScope> OpenIdScopeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["openid"]         = OpenIdScope.OpenId,
        ["profile"]        = OpenIdScope.Profile,
        ["email"]          = OpenIdScope.Email,
        ["phone"]          = OpenIdScope.Phone,
        ["address"]        = OpenIdScope.Address,
        ["offline_access"] = OpenIdScope.OfflineAccess
    };

    public async Task<ClientAppResponse> ValidateClientAsync(string clientId, string redirectUri, string scope)
    {
        var client = await clientAppService.GetByClientIdAsync(clientId)
            ?? throw new InvalidOperationException("Client not found.");

        if (!client.IsActive)
            throw new InvalidOperationException("Client is inactive.");

        if (!client.GrantTypes.Contains("authorization_code", StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Client does not support authorization_code grant.");

        if (!client.RedirectUris.Contains(redirectUri, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Redirect URI is not registered for this client.");

        var requestedScopes = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (requestedScopes.Length == 0)
            throw new InvalidOperationException("At least one scope is required.");

        foreach (var s in requestedScopes)
        {
            if (OpenIdScopeMap.TryGetValue(s, out var openIdScope))
            {
                if (!client.OpenIdScopes.Contains(openIdScope))
                    throw new InvalidOperationException($"Scope '{s}' is not allowed for this client.");
            }
            else
            {
                throw new InvalidOperationException($"Unknown scope '{s}'.");
            }
        }

        return client;
    }

    public async Task<string> GenerateCodeAsync(
        string clientId, Guid userId, string redirectUri, List<string> scopes, string state)
    {
        var code = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        var authCode = new AuthCode
        {
            Code        = code,
            ClientId    = clientId,
            UserId      = userId,
            RedirectUri = redirectUri,
            Scopes      = scopes,
            State       = state,
            ExpiresAt   = DateTime.UtcNow.AddMinutes(10)
        };

        var codes = await LoadAsync();
        codes.Add(authCode);
        await SaveAsync(codes);

        return code;
    }

    public async Task<ConsumedAuthCode> ConsumeAsync(string code, string clientId, string redirectUri)
    {
        var codes = await LoadAsync();
        var authCode = codes.FirstOrDefault(c => c.Code == code);

        if (authCode is null || authCode.IsUsed || authCode.ExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("Authorization code is invalid or expired.");

        if (!authCode.ClientId.Equals(clientId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Client ID mismatch.");

        if (!authCode.RedirectUri.Equals(redirectUri, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Redirect URI mismatch.");

        authCode.IsUsed = true;
        await SaveAsync(codes);

        return new ConsumedAuthCode(authCode.UserId, authCode.ClientId, authCode.Scopes);
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private async Task<List<AuthCode>> LoadAsync()
    {
        if (!File.Exists(_storePath)) return [];
        var json = await File.ReadAllTextAsync(_storePath);
        return JsonSerializer.Deserialize<List<AuthCode>>(json) ?? [];
    }

    private async Task SaveAsync(List<AuthCode> codes)
    {
        var json = JsonSerializer.Serialize(codes, _jsonOptions);
        await File.WriteAllTextAsync(_storePath, json);
    }
}
