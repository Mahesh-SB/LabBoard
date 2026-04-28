namespace LabBoard.Auth.Api.Entities;

internal class AuthCode
{
    public string Code { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string RedirectUri { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = [];
    public string State { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
