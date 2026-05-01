namespace LabBoard.Auth.Api.Entities;

internal class ApiPrivilege
{
    public string SourceClientId { get; set; } = string.Empty;
    public string TargetClientId { get; set; } = string.Empty;
    public bool CanRead { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }
}
