using System.ComponentModel.DataAnnotations;
using LabBoard.UserManagement.Api.Enums;

namespace LabBoard.UserManagement.Api.Models.User;

public class UpdateRoleRequest
{
    [Required]
    public UserRole Role { get; set; }
}
