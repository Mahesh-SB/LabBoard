namespace LabBoard.Auth.Api.Models.Client;

public class ApiPrivilegeRequest
{
    public List<ApiPrivilegeEntryRequest> Privileges { get; set; } = [];
}

public class ApiPrivilegeEntryRequest
{
    public string TargetClientId { get; set; } = string.Empty;
    public bool CanRead { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }
}
