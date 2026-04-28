using LabBoard.Auth.Api.Helpers;
using LabBoard.Auth.Api.Models.Client;
using LabBoard.Auth.Api.Models.OAuth;
using LabBoard.Auth.Api.Services.Client;
using LabBoard.Auth.Api.Services.OAuth;
using LabBoard.Auth.Api.Services.User;
using Microsoft.AspNetCore.Mvc;

namespace LabBoard.Auth.Api.Controllers;

[Route("oauth")]
public class OAuthController(
    IAuthCodeService authCodeService,
    IClientAppService clientAppService,
    IUserService userService,
    ITokenService tokenService) : ControllerBase
{
    // Step 1 — browser hits this URL, auth server validates client, returns HTML login page
    [HttpGet("authorize")]
    public async Task<ContentResult> Authorize([FromQuery] AuthorizeQueryParams query)
    {
        if (!query.ResponseType.Equals("code", StringComparison.OrdinalIgnoreCase))
            return HtmlError("Unsupported response_type. Only 'code' is supported.");

        if (string.IsNullOrWhiteSpace(query.ClientId) ||
            string.IsNullOrWhiteSpace(query.RedirectUri) ||
            string.IsNullOrWhiteSpace(query.Scope))
            return HtmlError("Missing required parameters: client_id, redirect_uri, scope.");

        try
        {
            var client = await authCodeService.ValidateClientAsync(query.ClientId, query.RedirectUri, query.Scope);
            return Html(LoginPageHtml.Build(client.AppName, query.ClientId, query.RedirectUri, query.Scope, query.State));
        }
        catch (InvalidOperationException ex)
        {
            return HtmlError(ex.Message);
        }
    }

    // Step 2 — login form submits here, auth server validates credentials, issues code, redirects
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromForm] LoginFormRequest form)
    {
        if (string.IsNullOrWhiteSpace(form.ClientId) ||
            string.IsNullOrWhiteSpace(form.RedirectUri) ||
            string.IsNullOrWhiteSpace(form.Scope) ||
            string.IsNullOrWhiteSpace(form.Email) ||
            string.IsNullOrWhiteSpace(form.Password))
            return HtmlError("Missing required form fields.");

        ClientAppResponse client;
        try
        {
            client = await authCodeService.ValidateClientAsync(form.ClientId, form.RedirectUri, form.Scope);
        }
        catch (InvalidOperationException ex)
        {
            return HtmlError(ex.Message);
        }

        var user = await userService.ValidateCredentialsAsync(form.Email, form.Password);
        if (user is null)
        {
            return Html(LoginPageHtml.Build(
                client.AppName, form.ClientId, form.RedirectUri, form.Scope, form.State,
                error: "Invalid email or password."));
        }

        var scopes = form.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var code   = await authCodeService.GenerateCodeAsync(form.ClientId, user.Id, form.RedirectUri, scopes, form.State);

        return Redirect(BuildRedirectUri(form.RedirectUri, code, form.State));
    }

    // Step 3 — Gateway (BFF) exchanges auth code for a JWT server-to-server
    [HttpPost("token")]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Token([FromForm] TokenRequest request)
    {
        if (!request.GrantType.Equals("authorization_code", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "unsupported_grant_type" });

        if (string.IsNullOrWhiteSpace(request.Code) ||
            string.IsNullOrWhiteSpace(request.ClientId) ||
            string.IsNullOrWhiteSpace(request.ClientSecret) ||
            string.IsNullOrWhiteSpace(request.RedirectUri))
            return BadRequest(new { error = "invalid_request", description = "Missing required parameters." });

        // Validate client credentials
        var client = await clientAppService.GetByClientIdAsync(request.ClientId);
        if (client is null || !client.IsActive || client.ClientSecret != request.ClientSecret)
            return Unauthorized(new { error = "invalid_client" });

        // Consume the auth code — validates expiry, single-use, client + redirect match
        ConsumedAuthCode consumed;
        try
        {
            consumed = await authCodeService.ConsumeAsync(request.Code, request.ClientId, request.RedirectUri);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = "invalid_grant", description = ex.Message });
        }

        var user = await userService.GetByIdAsync(consumed.UserId);
        if (user is null)
            return BadRequest(new { error = "invalid_grant", description = "User not found." });

        return Ok(tokenService.Generate(user, client, consumed.Scopes));
    }

    private static string BuildRedirectUri(string redirectUri, string code, string state)
    {
        var sep = redirectUri.Contains('?') ? '&' : '?';
        var uri = $"{redirectUri}{sep}code={Uri.EscapeDataString(code)}";
        if (!string.IsNullOrEmpty(state))
            uri += $"&state={Uri.EscapeDataString(state)}";
        return uri;
    }

    private static ContentResult Html(string html)
        => new() { Content = html, ContentType = "text/html; charset=utf-8", StatusCode = 200 };

    private static ContentResult HtmlError(string message)
        => new() { Content = LoginPageHtml.Error(message), ContentType = "text/html; charset=utf-8", StatusCode = 400 };
}
