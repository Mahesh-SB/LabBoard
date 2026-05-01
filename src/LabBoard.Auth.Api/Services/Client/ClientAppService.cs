using System.Text.Json;
using LabBoard.Auth.Api.Entities;
using LabBoard.Auth.Api.Enums;
using LabBoard.Auth.Api.Models.Client;

namespace LabBoard.Auth.Api.Services.Client;

public class ClientAppService(IWebHostEnvironment env) : IClientAppService
{
    private readonly string _storePath = Path.Combine(env.ContentRootPath, "Database", "clientAppStore.json");
    private readonly string _privilegeStorePath = Path.Combine(env.ContentRootPath, "Database", "privilegeStore.json");
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public async Task<ClientAppResponse> RegisterAsync(ClientAppRequest request)
    {
        var apps = await LoadAsync();

        if (apps.Any(a => a.AppName.Equals(request.AppName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("An app with this name already exists.");

        var allowedGrantTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "authorization_code", "refresh_token", "client_credentials" };

        var unknownGrants = request.GrantTypes.Where(g => !allowedGrantTypes.Contains(g)).ToList();
        if (unknownGrants.Count > 0)
            throw new InvalidOperationException($"Unknown grant type(s): {string.Join(", ", unknownGrants)}.");

        if (request.GrantTypes.Count == 0)
            throw new InvalidOperationException("At least one grant type is required.");

        var hasRefreshToken = request.GrantTypes.Contains("refresh_token", StringComparer.OrdinalIgnoreCase);
        var hasAuthCode = request.GrantTypes.Contains("authorization_code", StringComparer.OrdinalIgnoreCase);
        var hasClientCredentials = request.GrantTypes.Contains("client_credentials", StringComparer.OrdinalIgnoreCase);

        if (hasRefreshToken && !hasAuthCode)
            throw new InvalidOperationException("refresh_token grant requires authorization_code grant to be included.");

        if (hasClientCredentials && (hasAuthCode || hasRefreshToken))
            throw new InvalidOperationException("client_credentials cannot be combined with authorization_code or refresh_token grants.");

        List<OpenIdScope> resolvedScopes;
        if (hasClientCredentials)
        {
            resolvedScopes = [];
        }
        else
        {
            var additionalScopes = request.AdditionalOpenIdScopes;
            if (hasRefreshToken && hasAuthCode && !additionalScopes.Contains(OpenIdScope.OfflineAccess))
                additionalScopes = [.. additionalScopes, OpenIdScope.OfflineAccess];
            resolvedScopes = MergeWithDefaults(additionalScopes);
        }

        var app = new ClientApp
        {
            AppName        = request.AppName,
            AppDescription = request.AppDescription,
            ClientId       = Guid.NewGuid().ToString("N"),
            ClientSecret   = Guid.NewGuid().ToString("N"),
            GrantTypes     = request.GrantTypes,
            RedirectUris   = hasClientCredentials ? [] : request.RedirectUris,
            OpenIdScopes   = resolvedScopes,
            TokenExpiry    = request.TokenExpiry
        };

        apps.Add(app);
        await SaveAsync(apps);

        return ToResponse(app);
    }

    public async Task<ClientAppResponse?> GetByIdAsync(Guid id)
    {
        var apps = await LoadAsync();
        var app = apps.FirstOrDefault(a => a.Id == id);
        return app is null ? null : ToResponse(app);
    }

    public async Task<ClientAppResponse?> GetByClientIdAsync(string clientId)
    {
        var apps = await LoadAsync();
        var app = apps.FirstOrDefault(a => a.ClientId == clientId);
        return app is null ? null : ToResponse(app);
    }

    public async Task<IEnumerable<ClientAppResponse>> GetAllAsync()
    {
        var apps = await LoadAsync();
        return apps.Select(ToResponse);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var apps = await LoadAsync();
        var app = apps.FirstOrDefault(a => a.Id == id);
        if (app is null) return false;

        apps.Remove(app);
        await SaveAsync(apps);
        return true;
    }

    private static readonly List<OpenIdScope> DefaultOpenIdScopes =
        [OpenIdScope.OpenId, OpenIdScope.Profile, OpenIdScope.Email];

    private static List<OpenIdScope> MergeWithDefaults(List<OpenIdScope> requested)
    {
        var merged = new HashSet<OpenIdScope>(DefaultOpenIdScopes);
        merged.UnionWith(requested);
        return [.. merged];
    }

    public async Task<ApiPrivilegeResponse?> GetPrivilegesAsync(Guid id)
    {
        var apps = await LoadAsync();
        var app = apps.FirstOrDefault(a => a.Id == id);
        if (app is null) return null;

        var all = await LoadPrivilegesAsync();
        var entries = all
            .Where(p => p.SourceClientId == app.ClientId)
            .Select(p => new ApiPrivilegeEntryResponse
            {
                TargetClientId = p.TargetClientId,
                TargetAppName  = apps.FirstOrDefault(a => a.ClientId == p.TargetClientId)?.AppName ?? p.TargetClientId,
                CanRead        = p.CanRead,
                CanUpdate      = p.CanUpdate,
                CanDelete      = p.CanDelete
            }).ToList();

        return new ApiPrivilegeResponse { SourceClientId = app.ClientId, Privileges = entries };
    }

    public async Task<ApiPrivilegeResponse?> SetPrivilegesAsync(Guid id, ApiPrivilegeRequest request)
    {
        var apps = await LoadAsync();
        var app = apps.FirstOrDefault(a => a.Id == id);
        if (app is null) return null;

        var all = await LoadPrivilegesAsync();
        all.RemoveAll(p => p.SourceClientId == app.ClientId);

        foreach (var entry in request.Privileges)
        {
            all.Add(new ApiPrivilege
            {
                SourceClientId = app.ClientId,
                TargetClientId = entry.TargetClientId,
                CanRead        = entry.CanRead,
                CanUpdate      = entry.CanUpdate,
                CanDelete      = entry.CanDelete
            });
        }

        await SavePrivilegesAsync(all);
        return await GetPrivilegesAsync(id);
    }

    private async Task<List<ClientApp>> LoadAsync()
    {
        if (!File.Exists(_storePath)) return [];
        var json = await File.ReadAllTextAsync(_storePath);
        return JsonSerializer.Deserialize<List<ClientApp>>(json) ?? [];
    }

    private async Task SaveAsync(List<ClientApp> apps)
    {
        var json = JsonSerializer.Serialize(apps, _jsonOptions);
        await File.WriteAllTextAsync(_storePath, json);
    }

    private async Task<List<ApiPrivilege>> LoadPrivilegesAsync()
    {
        if (!File.Exists(_privilegeStorePath)) return [];
        var json = await File.ReadAllTextAsync(_privilegeStorePath);
        return JsonSerializer.Deserialize<List<ApiPrivilege>>(json) ?? [];
    }

    private async Task SavePrivilegesAsync(List<ApiPrivilege> privileges)
    {
        var json = JsonSerializer.Serialize(privileges, _jsonOptions);
        await File.WriteAllTextAsync(_privilegeStorePath, json);
    }

    private static ClientAppResponse ToResponse(ClientApp app) => new()
    {
        Id             = app.Id,
        AppName        = app.AppName,
        AppDescription = app.AppDescription,
        ClientId       = app.ClientId,
        ClientSecret   = app.ClientSecret,
        GrantTypes     = app.GrantTypes,
        RedirectUris   = app.RedirectUris,
        OpenIdScopes   = app.OpenIdScopes,
        TokenExpiry    = app.TokenExpiry,
        IsActive       = app.IsActive,
        CreatedAt      = app.CreatedAt
    };
}
