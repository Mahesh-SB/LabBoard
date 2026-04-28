using Microsoft.AspNetCore.Mvc;

namespace LabBoard.Auth.Api.Models.OAuth;

public class AuthorizeQueryParams
{
    [FromQuery(Name = "response_type")]
    public string ResponseType { get; set; } = string.Empty;

    [FromQuery(Name = "client_id")]
    public string ClientId { get; set; } = string.Empty;

    [FromQuery(Name = "redirect_uri")]
    public string RedirectUri { get; set; } = string.Empty;

    [FromQuery(Name = "scope")]
    public string Scope { get; set; } = string.Empty;

    [FromQuery(Name = "state")]
    public string State { get; set; } = string.Empty;
}
