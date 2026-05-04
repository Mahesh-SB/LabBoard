namespace LabBoard.Auth.Api.Models.OAuth;

public class OAuthException(
    string  error,
    string? description = null,
    int     httpStatus  = 400) : Exception(description ?? error)
{
    public string  Error       { get; } = error;
    public string? Description { get; } = description;
    public int     HttpStatus  { get; } = httpStatus;
}
