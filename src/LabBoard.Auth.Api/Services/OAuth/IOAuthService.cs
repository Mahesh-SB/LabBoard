using LabBoard.Auth.Api.Models.Client;
using LabBoard.Auth.Api.Models.OAuth;

namespace LabBoard.Auth.Api.Services.OAuth;

public interface IOAuthService
{
    // Used by the Authorize endpoint to validate the OAuth client before showing the login page
    Task<ClientAppResponse> ValidateAuthorizeRequestAsync(string clientId, string redirectUri, string scope);

    // Used by the Login endpoint — validates credentials, generates code, returns redirect URI
    Task<LoginResult> ProcessLoginAsync(LoginFormRequest form);

    // Used by the Token endpoint — dispatches on grant_type, throws OAuthException on failure
    Task<TokenResponse> IssueTokenAsync(TokenRequest request);
}
