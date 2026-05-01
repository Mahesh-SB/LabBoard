using System.ComponentModel.DataAnnotations;
using LabBoard.UserManagement.Api.Enums;

namespace LabBoard.UserManagement.Api.Models.User;

public class UserRegisterRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(Male|Female|Other)$", ErrorMessage = "Gender must be Male, Female, or Other.")]
    public string Gender { get; set; } = string.Empty;

    [Required]
    [Range(1, 120)]
    public int Age { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Phone]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Viewer;
}
