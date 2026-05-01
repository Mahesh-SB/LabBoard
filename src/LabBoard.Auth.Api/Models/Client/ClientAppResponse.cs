using LabBoard.Auth.Api.Enums;

namespace LabBoard.Auth.Api.Models.Client;

public class ClientAppResponse
{
    public Guid Id { get; set; }
    public string AppName { get; set; } = string.Empty;
    public string AppDescription { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public List<string> GrantTypes { get; set; } = [];
    public List<string> RedirectUris { get; set; } = [];
    public List<OpenIdScope> OpenIdScopes { get; set; } = [];
    public int TokenExpiry { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
