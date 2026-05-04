using LabBoard.Auth.Api.Models.Client;
using LabBoard.Auth.Api.Models.OAuth;
using LabBoard.Auth.Api.Models.User;

namespace LabBoard.Auth.Api.Services.OAuth;

public interface ITokenService
{
    TokenResponse Generate(UserResponse user, ClientAppResponse client, List<string> scopes);
    TokenResponse GenerateClientToken(ClientAppResponse client, List<string> scopes);
}
