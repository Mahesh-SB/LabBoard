using LabBoard.Auth.Api.Helpers;
using LabBoard.Auth.Api.Models.OAuth;
using LabBoard.Auth.Api.Services.OAuth;
using Microsoft.AspNetCore.Mvc;

namespace LabBoard.Auth.Api.Controllers;

[Route("oauth")]
public class OAuthController(IOAuthService oauthService) : ControllerBase
{
    // Step 1 — browser hits this URL, auth server validates client and returns the login page
    [HttpGet("authorize")]
    public async Task<ContentResult> Authorize([FromQuery] AuthorizeQueryParams query)
    {
        if (!query.ResponseType.Equals("code", StringComparison.OrdinalIgnoreCase))
            return HtmlError("Unsupported response_type. Only 'code' is supported.");

        if (string.IsNullOrWhiteSpace(query.ClientId)    ||
            string.IsNullOrWhiteSpace(query.RedirectUri) ||
            string.IsNullOrWhiteSpace(query.Scope))
            return HtmlError("Missing required parameters: client_id, redirect_uri, scope.");

        try
        {
            var client = await oauthService.ValidateAuthorizeRequestAsync(query.ClientId, query.RedirectUri, query.Scope);
            return Html(LoginPageHtml.Build(client.AppName, query.ClientId, query.RedirectUri, query.Scope, query.State));
        }
        catch (InvalidOperationException ex)
        {
            return HtmlError(ex.Message);
        }
    }

    // Step 2 — login form submits here, auth server validates credentials, issues code and redirects
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromForm] LoginFormRequest form)
    {
        if (string.IsNullOrWhiteSpace(form.ClientId)    ||
            string.IsNullOrWhiteSpace(form.RedirectUri) ||
            string.IsNullOrWhiteSpace(form.Scope)       ||
            string.IsNullOrWhiteSpace(form.Email)       ||
            string.IsNullOrWhiteSpace(form.Password))
            return HtmlError("Missing required form fields.");

        try
        {
            var result = await oauthService.ProcessLoginAsync(form);

            if (result.IsInvalidCredentials)
                return Html(LoginPageHtml.Build(
                    result.AppName!, form.ClientId, form.RedirectUri, form.Scope, form.State,
                    error: "Invalid email or password."));

            return Redirect(result.RedirectUri!);
        }
        catch (InvalidOperationException ex)
        {
            return HtmlError(ex.Message);
        }
    }

    // Step 3 — client exchanges auth code or client credentials for a JWT
    [HttpPost("token")]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Token([FromForm] TokenRequest request)
    {
        try
        {
            return Ok(await oauthService.IssueTokenAsync(request));
        }
        catch (OAuthException ex) when (ex.HttpStatus == 401)
        {
            return Unauthorized(new { error = ex.Error });
        }
        catch (OAuthException ex)
        {
            return BadRequest(new { error = ex.Error, description = ex.Description });
        }
    }

    private static ContentResult Html(string html)
        => new() { Content = html, ContentType = "text/html; charset=utf-8", StatusCode = 200 };

    private static ContentResult HtmlError(string message)
        => new() { Content = LoginPageHtml.Error(message), ContentType = "text/html; charset=utf-8", StatusCode = 400 };
}
