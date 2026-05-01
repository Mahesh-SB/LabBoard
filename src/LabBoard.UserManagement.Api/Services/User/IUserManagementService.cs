using LabBoard.UserManagement.Api.Enums;
using LabBoard.UserManagement.Api.Models.User;

namespace LabBoard.UserManagement.Api.Services.User;

public interface IUserManagementService
{
    Task<UserResponse> RegisterAsync(UserRegisterRequest request);
    Task<IEnumerable<UserResponse>> GetAllAsync();
    Task<UserResponse?> GetByIdAsync(Guid id);
    Task<UserResponse?> UpdateRoleAsync(Guid id, UserRole role);
    Task<UserResponse?> UpdateStatusAsync(Guid id, bool isActive);
    Task<bool> DeleteAsync(Guid id);
}
