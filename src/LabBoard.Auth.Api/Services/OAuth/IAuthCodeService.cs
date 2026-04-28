using LabBoard.Auth.Api.Models.Client;
using LabBoard.Auth.Api.Models.OAuth;

namespace LabBoard.Auth.Api.Services.OAuth;

public interface IAuthCodeService
{
    Task<ClientAppResponse> ValidateClientAsync(string clientId, string redirectUri, string scope);
    Task<string> GenerateCodeAsync(string clientId, Guid userId, string redirectUri, List<string> scopes, string state);
    Task<ConsumedAuthCode> ConsumeAsync(string code, string clientId, string redirectUri);
}
