using LabBoard.Auth.Api.Models.User;

namespace LabBoard.Auth.Api.Services.User;

public interface IUserService
{
    Task<UserResponse?> GetByIdAsync(Guid id);
    Task<UserResponse?> ValidateCredentialsAsync(string email, string password);
}
