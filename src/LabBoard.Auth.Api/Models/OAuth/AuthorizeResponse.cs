namespace LabBoard.Auth.Api.Models.OAuth;

public class AuthorizeResponse
{
    public string Code { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public int ExpiresIn { get; set; } = 600;
}
