namespace LabBoard.Auth.Api.Models.Client;

public class ApiPrivilegeResponse
{
    public string SourceClientId { get; set; } = string.Empty;
    public List<ApiPrivilegeEntryResponse> Privileges { get; set; } = [];
}

public class ApiPrivilegeEntryResponse
{
    public string TargetClientId { get; set; } = string.Empty;
    public string TargetAppName { get; set; } = string.Empty;
    public bool CanRead { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }
}
