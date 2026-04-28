using System.ComponentModel.DataAnnotations;

namespace LabBoard.UserManagement.Api.Models.User;

public class UpdateStatusRequest
{
    [Required]
    public bool IsActive { get; set; }
}
