namespace LabBoard.Auth.Api.Configuration;

public class JwtOptions
{
    public string PrivateKey         { get; set; } = string.Empty;
    public string Issuer             { get; set; } = "LabBoard";
    public string Audience           { get; set; } = "LabBoard.Gateway";
    public string InternalAudience   { get; set; } = "LabBoard.Internal";
}
