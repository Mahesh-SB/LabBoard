namespace LabBoard.Gateway.Api.Configuration;

public class OAuthClientOptions
{
    public string ClientId      { get; set; } = string.Empty;
    public string ClientSecret  { get; set; } = string.Empty;
    public string RedirectUri   { get; set; } = string.Empty;
    public string Scope         { get; set; } = string.Empty;
    public string AuthApiBaseUrl { get; set; } = string.Empty;
}
