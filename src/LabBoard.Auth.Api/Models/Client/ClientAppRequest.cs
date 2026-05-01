using System.ComponentModel.DataAnnotations;
using LabBoard.Auth.Api.Enums;

namespace LabBoard.Auth.Api.Models.Client;

public class ClientAppRequest
{
    [Required]
    public string AppName { get; set; } = string.Empty;

    public string AppDescription { get; set; } = string.Empty;

    [Required]
    public List<string> GrantTypes { get; set; } = [];

    public List<string> RedirectUris { get; set; } = [];

    public List<OpenIdScope> AdditionalOpenIdScopes { get; set; } = [];

    public int TokenExpiry { get; set; } = 3600;
}
