namespace LabBoard.UserManagement.Api.Configuration;

public class JwtOptions
{
    public string Issuer   { get; set; } = "LabBoard";
    public string Audience { get; set; } = "LabBoard.Internal";
    public string JwksUri  { get; set; } = string.Empty;
}
