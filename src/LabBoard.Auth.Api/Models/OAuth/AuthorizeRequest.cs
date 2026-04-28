using System.ComponentModel.DataAnnotations;

namespace LabBoard.Auth.Api.Models.OAuth;

public class AuthorizeRequest
{
    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public string RedirectUri { get; set; } = string.Empty;

    [Required]
    public string Scope { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    [Required]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
