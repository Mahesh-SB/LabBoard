using System.Text.Json;
using LabBoard.UserManagement.Api.Enums;
using LabBoard.UserManagement.Api.Models.User;
using UserEntity = LabBoard.UserManagement.Api.Entities.User;

namespace LabBoard.UserManagement.Api.Services.User;

public class UserManagementService(IWebHostEnvironment env) : IUserManagementService
{
    private readonly string _storePath = Path.Combine(env.ContentRootPath, "Database", "userStore.json");
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public async Task<IEnumerable<UserResponse>> GetAllAsync()
    {
        var users = await LoadAsync();
        return users.Select(ToResponse);
    }

    public async Task<UserResponse?> GetByIdAsync(Guid id)
    {
        var users = await LoadAsync();
        var user = users.FirstOrDefault(u => u.Id == id);
        return user is null ? null : ToResponse(user);
    }

    public async Task<UserResponse?> UpdateRoleAsync(Guid id, UserRole role)
    {
        var users = await LoadAsync();
        var user = users.FirstOrDefault(u => u.Id == id);
        if (user is null) return null;

        user.Role = role;
        await SaveAsync(users);
        return ToResponse(user);
    }

    public async Task<UserResponse?> UpdateStatusAsync(Guid id, bool isActive)
    {
        var users = await LoadAsync();
        var user = users.FirstOrDefault(u => u.Id == id);
        if (user is null) return null;

        user.IsActive = isActive;
        await SaveAsync(users);
        return ToResponse(user);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var users = await LoadAsync();
        var user = users.FirstOrDefault(u => u.Id == id);
        if (user is null) return false;

        users.Remove(user);
        await SaveAsync(users);
        return true;
    }

    private async Task<List<UserEntity>> LoadAsync()
    {
        if (!File.Exists(_storePath)) return [];
        var json = await File.ReadAllTextAsync(_storePath);
        return JsonSerializer.Deserialize<List<UserEntity>>(json, _jsonOptions) ?? [];
    }

    private async Task SaveAsync(List<UserEntity> users)
    {
        var json = JsonSerializer.Serialize(users, _jsonOptions);
        await File.WriteAllTextAsync(_storePath, json);
    }

    private static UserResponse ToResponse(UserEntity u) => new()
    {
        Id        = u.Id,
        FullName  = u.FullName,
        Gender    = u.Gender,
        Age       = u.Age,
        Email     = u.Email,
        Phone     = u.Phone,
        Role      = u.Role,
        IsActive  = u.IsActive,
        CreatedAt = u.CreatedAt
    };
}
