namespace LabBoard.Auth.Api.Models.OAuth;

public class LoginResult
{
    public bool    IsSuccess            { get; init; }
    public string? RedirectUri          { get; init; }
    public bool    IsInvalidCredentials { get; init; }
    public string? AppName              { get; init; }
}
