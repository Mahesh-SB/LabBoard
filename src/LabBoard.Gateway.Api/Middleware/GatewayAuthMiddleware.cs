using System.Security.Cryptography;
using LabBoard.Gateway.Api.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace LabBoard.Gateway.Api.Middleware;

public class GatewayAuthMiddleware : IDisposable
{
    private readonly RequestDelegate _next;
    private readonly OAuthClientOptions _oauth;
    private readonly TokenValidationParameters _validationParams;
    private readonly RSACryptoServiceProvider _rsa;

    private const string SessionCookie = "lb_session";

    private static readonly string[] PublicPaths =
    [
        "/oauth/callback",
        "/oauth/logout",
        "/oauth/start",
        "/openapi",
        "/favicon.ico"
    ];

    public GatewayAuthMiddleware(
        RequestDelegate next,
        IOptions<JwtOptions> jwtOptions,
        IOptions<OAuthClientOptions> oauthOptions)
    {
        _next  = next;
        _oauth = oauthOptions.Value;

        var jwt = jwtOptions.Value;
        _rsa = new RSACryptoServiceProvider();
        _rsa.ImportCspBlob(Convert.FromBase64String(jwt.PublicKey));

        _validationParams = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidIssuer              = jwt.Issuer,
            ValidateAudience         = true,
            ValidAudience            = jwt.Audience,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new RsaSecurityKey(_rsa),
            ClockSkew                = TimeSpan.Zero
        };
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (context.Request.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase) ||
            IsPublic(path))
        {
            await _next(context);
            return;
        }

        var token = context.Request.Cookies[SessionCookie];
        if (!string.IsNullOrWhiteSpace(token) && await IsValidTokenAsync(token))
        {
            await _next(context);
            return;
        }

        // XHR / API call → return 401 so Angular interceptor handles it
            if (context.Request.Headers.ContainsKey("Origin"))
            {
                context.Response.StatusCode  = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\":\"unauthorized\"}");
                return;
            }

        // Browser navigation → redirect to Auth.Api login page
        var currentPath  = context.Request.Path + context.Request.QueryString;
        var authorizeUrl =
            $"{_oauth.AuthApiBaseUrl}/oauth/authorize" +
            $"?response_type=code" +
            $"&client_id={Uri.EscapeDataString(_oauth.ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(_oauth.RedirectUri)}" +
            $"&scope={Uri.EscapeDataString(_oauth.Scope)}" +
            $"&state={Uri.EscapeDataString(currentPath)}";

        context.Response.Redirect(authorizeUrl);
    }

    private async Task<bool> IsValidTokenAsync(string token)
    {
        try
        {
            var result = await new JsonWebTokenHandler()
                .ValidateTokenAsync(token, _validationParams);
            return result.IsValid;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsPublic(string path)
        => PublicPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    public void Dispose() => _rsa.Dispose();
}
