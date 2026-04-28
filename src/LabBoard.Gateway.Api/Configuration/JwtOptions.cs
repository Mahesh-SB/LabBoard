namespace LabBoard.Gateway.Api.Configuration;

public class JwtOptions
{
    public string PublicKey { get; set; } = string.Empty;
    public string Issuer    { get; set; } = "LabBoard";
    public string Audience  { get; set; } = "LabBoard.Gateway";
}
