using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LabBoard.Auth.Api.Models.User;
using UserEntity = LabBoard.Auth.Api.Entities.User;

namespace LabBoard.Auth.Api.Services.User;

public class UserService(IWebHostEnvironment env) : IUserService
{
    private readonly string _storePath = Path.Combine(env.ContentRootPath, "..", "LabBoard.UserManagement.Api", "Database", "userStore.json");

    public async Task<UserResponse?> GetByIdAsync(Guid id)
    {
        var users = await LoadAsync();
        var user = users.FirstOrDefault(u => u.Id == id);
        return user is null ? null : ToResponse(user);
    }

    public async Task<UserResponse?> ValidateCredentialsAsync(string email, string password)
    {
        var users = await LoadAsync();
        var hash = HashPassword(password);
        var user = users.FirstOrDefault(u =>
            u.Email.Equals(email, StringComparison.OrdinalIgnoreCase) &&
            u.PasswordHash == hash);
        return user is null ? null : ToResponse(user);
    }

    private async Task<List<UserEntity>> LoadAsync()
    {
        if (!File.Exists(_storePath)) return [];
        var json = await File.ReadAllTextAsync(_storePath);
        return JsonSerializer.Deserialize<List<UserEntity>>(json) ?? [];
    }

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static UserResponse ToResponse(UserEntity user) => new()
    {
        Id        = user.Id,
        FullName  = user.FullName,
        Gender    = user.Gender,
        Age       = user.Age,
        Email     = user.Email,
        Phone     = user.Phone,
        Role      = user.Role,
        CreatedAt = user.CreatedAt
    };
}
