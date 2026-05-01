using LabBoard.Auth.Api.Entities;
using LabBoard.Auth.Api.Models.Client;

namespace LabBoard.Auth.Api.Services.Client;

public interface IClientAppService
{
    Task<ClientAppResponse> RegisterAsync(ClientAppRequest request);
    Task<ClientAppResponse?> GetByIdAsync(Guid id);
    Task<ClientAppResponse?> GetByClientIdAsync(string clientId);
    Task<IEnumerable<ClientAppResponse>> GetAllAsync();
    Task<bool> DeleteAsync(Guid id);
    Task<ApiPrivilegeResponse?> GetPrivilegesAsync(Guid id);
    Task<ApiPrivilegeResponse?> SetPrivilegesAsync(Guid id, ApiPrivilegeRequest request);
}
