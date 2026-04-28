using LabBoard.Auth.Api.Models.User;

namespace LabBoard.Auth.Api.Services.User;

public interface IUserService
{
    Task<UserResponse> RegisterAsync(UserRegisterRequest request);
    Task<UserResponse?> GetByIdAsync(Guid id);
    Task<UserResponse?> GetByEmailAsync(string email);
    Task<UserResponse?> ValidateCredentialsAsync(string email, string password);
    Task<IEnumerable<UserResponse>> GetAllAsync();
    Task<bool> DeleteAsync(Guid id);
}
