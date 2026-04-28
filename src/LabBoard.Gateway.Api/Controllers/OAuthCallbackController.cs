using LabBoard.Gateway.Api.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace LabBoard.Gateway.Api.Controllers;

[ApiController]
[Route("oauth")]
public class OAuthCallbackController(
    IHttpClientFactory httpClientFactory,
    IOptions<OAuthClientOptions> oauthOptions) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    // Auth server redirects here after user login:
    // GET /oauth/callback?code=xxx&state=xxx
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { error = "invalid_request", description = "Authorization code missing." });

        var opts = oauthOptions.Value;

        // Exchange the authorization code for a JWT server-to-server
        var http = httpClientFactory.CreateClient("AuthApi");
        var form = new Dictionary<string, string>
        {
            ["grant_type"]    = "authorization_code",
            ["code"]          = code,
            ["client_id"]     = opts.ClientId,
            ["client_secret"] = opts.ClientSecret,
            ["redirect_uri"]  = opts.RedirectUri
        };

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync("/oauth/token", new FormUrlEncodedContent(form));
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new { error = "auth_unreachable", description = ex.Message });
        }

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, new { error = "token_exchange_failed", description = err });
        }

        var json = await response.Content.ReadAsStringAsync();
        var token = JsonSerializer.Deserialize<TokenResult>(json, JsonOpts);

        if (string.IsNullOrWhiteSpace(token?.AccessToken))
            return StatusCode(502, new { error = "invalid_token_response" });

        // Store JWT in HTTP-only cookie — browser never touches this token directly
        Response.Cookies.Append("lb_session", token.AccessToken, new CookieOptions
        {
            HttpOnly  = true,
            Secure    = false, // set true behind HTTPS in production
            SameSite  = SameSiteMode.Lax,
            Path      = "/",
            MaxAge    = TimeSpan.FromSeconds(token.ExpiresIn > 0 ? token.ExpiresIn : 3600)
        });

        // Redirect back to where the user was trying to go, or home
        var returnUrl = string.IsNullOrWhiteSpace(state) ? "/" : Uri.UnescapeDataString(state);
        return Redirect(returnUrl);
    }

    // Angular calls this (full browser redirect) when it receives a 401.
    // Sets state = returnUrl (an Angular URL) so after login the user lands back in the SPA.
    [HttpGet("start")]
    public IActionResult Start([FromQuery] string? returnUrl)
    {
        var opts  = oauthOptions.Value;
        var state = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl;
        var authorizeUrl =
            $"{opts.AuthApiBaseUrl}/oauth/authorize" +
            $"?response_type=code" +
            $"&client_id={Uri.EscapeDataString(opts.ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(opts.RedirectUri)}" +
            $"&scope={Uri.EscapeDataString(opts.Scope)}" +
            $"&state={Uri.EscapeDataString(state)}";
        return Redirect(authorizeUrl);
    }

    // Sign-out: clears the lb_session cookie and redirects to Auth.Api login
    [HttpGet("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("lb_session", new CookieOptions { Path = "/" });
        var opts       = oauthOptions.Value;
        var authorizeUrl =
            $"{opts.AuthApiBaseUrl}/oauth/authorize" +
            $"?response_type=code" +
            $"&client_id={Uri.EscapeDataString(opts.ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(opts.RedirectUri)}" +
            $"&scope={Uri.EscapeDataString(opts.Scope)}" +
            $"&state=%2F";
        return Redirect(authorizeUrl);
    }

    private sealed class TokenResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
