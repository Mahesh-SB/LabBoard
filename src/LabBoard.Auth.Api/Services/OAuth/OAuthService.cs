using LabBoard.Auth.Api.Models.Client;
using LabBoard.Auth.Api.Models.OAuth;
using LabBoard.Auth.Api.Services.Client;
using LabBoard.Auth.Api.Services.User;

namespace LabBoard.Auth.Api.Services.OAuth;

public class OAuthService(
    IAuthCodeService  authCodeService,
    IClientAppService clientAppService,
    IUserService      userService,
    ITokenService     tokenService) : IOAuthService
{
    // ── Authorize ─────────────────────────────────────────────────────────────
    public Task<ClientAppResponse> ValidateAuthorizeRequestAsync(string clientId, string redirectUri, string scope)
        => authCodeService.ValidateClientAsync(clientId, redirectUri, scope);

    // ── Login ─────────────────────────────────────────────────────────────────
    public async Task<LoginResult> ProcessLoginAsync(LoginFormRequest form)
    {
        // Throws InvalidOperationException if client is invalid — controller turns it into HTML error
        var client = await authCodeService.ValidateClientAsync(form.ClientId, form.RedirectUri, form.Scope);

        var user = await userService.ValidateCredentialsAsync(form.Email, form.Password);
        if (user is null)
            return new LoginResult { IsInvalidCredentials = true, AppName = client.AppName };

        var scopes = form.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var code   = await authCodeService.GenerateCodeAsync(form.ClientId, user.Id, form.RedirectUri, scopes, form.State);

        return new LoginResult { IsSuccess = true, RedirectUri = BuildRedirectUri(form.RedirectUri, code, form.State) };
    }

    // ── Token ─────────────────────────────────────────────────────────────────
    public Task<TokenResponse> IssueTokenAsync(TokenRequest request)
    {
        if (request.GrantType.Equals("authorization_code", StringComparison.OrdinalIgnoreCase))
            return HandleAuthorizationCodeAsync(request);

        if (request.GrantType.Equals("client_credentials", StringComparison.OrdinalIgnoreCase))
            return HandleClientCredentialsAsync(request);

        throw new OAuthException("unsupported_grant_type");
    }

    private async Task<TokenResponse> HandleAuthorizationCodeAsync(TokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code)         ||
            string.IsNullOrWhiteSpace(request.ClientId)     ||
            string.IsNullOrWhiteSpace(request.ClientSecret) ||
            string.IsNullOrWhiteSpace(request.RedirectUri))
            throw new OAuthException("invalid_request", "Missing required parameters.");

        var client = await clientAppService.GetByClientIdAsync(request.ClientId);
        if (client is null || !client.IsActive || client.ClientSecret != request.ClientSecret)
            throw new OAuthException("invalid_client", httpStatus: 401);

        ConsumedAuthCode consumed;
        try
        {
            consumed = await authCodeService.ConsumeAsync(request.Code, request.ClientId, request.RedirectUri);
        }
        catch (InvalidOperationException ex)
        {
            throw new OAuthException("invalid_grant", ex.Message);
        }

        var user = await userService.GetByIdAsync(consumed.UserId);
        if (user is null)
            throw new OAuthException("invalid_grant", "User not found.");

        return tokenService.Generate(user, client, consumed.Scopes);
    }

    private async Task<TokenResponse> HandleClientCredentialsAsync(TokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId) ||
            string.IsNullOrWhiteSpace(request.ClientSecret))
            throw new OAuthException("invalid_request", "Missing client_id or client_secret.");

        var client = await clientAppService.GetByClientIdAsync(request.ClientId);
        if (client is null || !client.IsActive || client.ClientSecret != request.ClientSecret)
            throw new OAuthException("invalid_client", httpStatus: 401);

        if (!client.GrantTypes.Contains("client_credentials", StringComparer.OrdinalIgnoreCase))
            throw new OAuthException("unauthorized_client", "This client is not authorized for client_credentials grant.");

        var privileges = await clientAppService.GetPrivilegesAsync(client.Id);
        var scopes     = BuildScopesFromPrivileges(privileges);

        return tokenService.GenerateClientToken(client, scopes);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static List<string> BuildScopesFromPrivileges(ApiPrivilegeResponse? privileges)
    {
        if (privileges?.Privileges is not { Count: > 0 }) return [];

        var scopes = new List<string>();
        foreach (var p in privileges.Privileges)
        {
            var target = p.TargetAppName.ToLowerInvariant().Replace(" ", "_");
            if (p.CanRead)   scopes.Add($"{target}:read");
            if (p.CanUpdate) scopes.Add($"{target}:update");
            if (p.CanDelete) scopes.Add($"{target}:delete");
        }
        return scopes;
    }

    private static string BuildRedirectUri(string redirectUri, string code, string? state)
    {
        var sep = redirectUri.Contains('?') ? '&' : '?';
        var uri = $"{redirectUri}{sep}code={Uri.EscapeDataString(code)}";
        if (!string.IsNullOrEmpty(state))
            uri += $"&state={Uri.EscapeDataString(state)}";
        return uri;
    }
}
