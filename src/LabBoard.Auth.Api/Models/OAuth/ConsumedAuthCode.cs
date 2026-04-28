namespace LabBoard.Auth.Api.Models.OAuth;

public record ConsumedAuthCode(Guid UserId, string ClientId, List<string> Scopes);
