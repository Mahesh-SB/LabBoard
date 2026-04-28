using LabBoard.Auth.Api.Enums;

namespace LabBoard.Auth.Api.Entities;

internal class ClientApp
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AppName { get; set; } = string.Empty;
    public string AppDescription { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public List<string> GrantTypes { get; set; } = [];
    public List<string> RedirectUris { get; set; } = [];
    public List<OpenIdScope> OpenIdScopes { get; set; } = [];
    public List<ApiScope> ApiScopes { get; set; } = [];
    public int TokenExpiry { get; set; } = 3600;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
